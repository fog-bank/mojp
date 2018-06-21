using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows.Automation;

namespace Mojp
{
    partial class MainViewModel
    {
        /// <summary>
        /// オートメーション関連の操作を行います。
        /// </summary>
        private class AutomationHandler
        {
            private AutomationElement previewWnd;
            private CacheRequest cacheReq = new CacheRequest();
            private Condition condition;

            public AutomationHandler(MainViewModel viewModel)
            {
                ViewModel = viewModel;

                // UI 要素の名前だけキャッシュする
                cacheReq.Add(AutomationElement.NameProperty);
                cacheReq.AutomationElementMode = AutomationElementMode.None;

                // テキストが空でなく、特定の UI 要素でない TextBlock をすべて拾う
                condition = new AndCondition(
                    new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock"),
                    new NotCondition(new PropertyCondition(AutomationElement.NameProperty, string.Empty)),
                    new PropertyCondition(AutomationElement.AutomationIdProperty, string.Empty));
            }

            private MainViewModel ViewModel { get; }

            /// <summary>
            /// MO の Preview Pane を表すオートメーション要素を検索し、プロパティ変更イベントをサブスクライブします。
            /// </summary>
            public void CaptureMtgo()
            {
                var currentPreviewWnd = previewWnd;

                // MO のプロセス ID を取得する
                if (!App.GetProcessIDByName("mtgo", out int mtgoProcessID))
                {
                    ReleaseAutomationElement();
                    ViewModel.InvokeSetMessage("起動中のプロセスの中に MO が見つかりません。");
                    return;
                }

                // "Preview" という名前のウィンドウを探す (なぜかルートの子で見つかる)
                AutomationElement newPreviewWnd = null;
                try
                {
                    newPreviewWnd = AutomationElement.RootElement.FindFirst(TreeScope.Children,
                        new AndCondition(
                            new PropertyCondition(AutomationElement.ProcessIdProperty, mtgoProcessID),
                            new PropertyCondition(AutomationElement.ClassNameProperty, "Window"),
                            new PropertyCondition(AutomationElement.NameProperty, "Preview")));
                }
                catch { Debug.WriteLine("Preview Pane の取得に失敗しました。"); }

                // 他のスレッドで処理が先行した
                if (previewWnd != currentPreviewWnd)
                    return;
                
                if (newPreviewWnd == null)
                {
                    ReleaseAutomationElement();
                    ViewModel.InvokeSetMessage("MO の Preview Pane が見つかりません。");
                    return;
                }

                if (newPreviewWnd == currentPreviewWnd)
                    return;
                
                // 新しい Preview Pane が見つかった
                ReleaseAutomationElement();
                previewWnd = newPreviewWnd;

                // とりあえずカード名を探す
                SearchCardName();

                // UI テキストの変化を追う
                // 対戦中、カード情報は Preview Pane の直接の子ではなく、
                // ZoomCard_View というカスタムコントロールの下にくるので、スコープは子孫にしないとダメ
                using (cacheReq.Activate())
                {
                    Automation.AddAutomationPropertyChangedEventHandler(newPreviewWnd,
                        TreeScope.Descendants, OnAutomaionNamePropertyChanged, AutomationElement.NameProperty);
                }

                if (ViewModel.SelectedCard == null)
                    ViewModel.InvokeSetMessage("準備完了");
#if DEBUG
                Debug.WriteLine("Automation event handlers (after add) = " +
                    (GetListeners()?.Count).GetValueOrDefault() + " @ T" + Thread.CurrentThread.ManagedThreadId);
#endif          
            }

            /// <summary>
            /// 現在の Preview Pane 内のテキストからカード名を取得し、表示します。
            /// </summary>
            public void SearchCardName()
            {
                AutomationElementCollection elements = null;
                try
                {
                    using (cacheReq.Activate())
                        elements = previewWnd?.FindAll(TreeScope.Descendants, condition);
                }
                catch { Debug.WriteLine("TextBlock 要素の全取得に失敗しました。"); }

                if (elements == null)
                    return;

                // 一連のテキストからカード名を探す (合体カードなど複数のカード名にヒットする場合があるので一通り探し直す必要がある)
                var foundCards = new List<Card>();
                bool isToken = false;

                foreach (AutomationElement element in elements)
                {
                    string name = GetNamePropertyValue(element);

                    // キャッシュ無効化時
                    if (name == null)
                        return;

                    // 英雄譚の絵師かもしれない場合
                    if (Card.GetSagaByArtist(name, out string saga))
                    {
                        if (CheckAndViewSaga(saga))
                            return;
                    }

                    // WHISPER データベースからカード情報を取得
                    if (App.Cards.TryGetValue(name, out var card))
                    {
                        if (!foundCards.Contains(card))
                        {
                            foundCards.Add(card);

                            // 両面カードの場合に、Preview Pane に片面だけ表示されていても、もう一方の面を表示するようにする
                            if (card.RelatedCardName != null)
                            {
                                foreach (string relatedName in card.RelatedCardNames)
                                {
                                    App.Cards.TryGetValue(relatedName, out var card2);

                                    if (!foundCards.Contains(card2))
                                        foundCards.Add(card2);
                                }
                            }
                        }
                    }
                    else if (!isToken && name.StartsWith("Token"))
                        isToken = true;
                }

                // 設定によっては基本土地 5 種の場合に表示を変えないようにする
                if (!ViewModel.ShowBasicLands && foundCards.Count == 1)
                {
                    switch (foundCards[0].Name)
                    {
                        case "Plains":
                        case "Island":
                        case "Swamp":
                        case "Mountain":
                        case "Forest":
                            return;
                    }
                }

                // 汎用のトークン
                if (foundCards.Count == 0 && isToken)
                {
                    ViewModel.InvokeSetMessage("トークン");
                    return;
                }
                ViewModel.InvokeSetCards(foundCards);
            }

            /// <summary>
            /// 各種リソースを解放します。
            /// </summary>
            public void Release()
            {
                cacheReq = null;
                condition = null;
                ReleaseAutomationElement();
            }

            /// <summary>
            /// 変更された要素名がカード名かどうかを調べます。
            /// </summary>
            /// <remarks>AutomationPropertyChangedEventHandler は UI スレッドとは別スレッドで動いている</remarks>
            private void OnAutomaionNamePropertyChanged(object sender, AutomationPropertyChangedEventArgs e)
            {
                if (previewWnd == null)
                    return;

                string name = GetNamePropertyValue(sender as AutomationElement);
                //Debug.WriteLineIf(!string.IsNullOrWhiteSpace(name), name);

                // キャッシュ無効化時
                if (name == null)
                    return;

                // 新しいテキストがカード名かどうかを調べ、そうでないなら不必要な全体検索をしないようにする
                // トークンの場合は、カード名を含むとき (= コピートークン) と含まないとき (→ 空表示にする) とがあるので検索を続行する
                if (!App.Cards.ContainsKey(name) && !name.StartsWith("Token"))
                {
                    string cardType = null;
                    const string triggerPrefix = "Triggered ability from ";

                    // 統治者以外の紋章やテキストレスのヴァンガードの場合は、確定で空表示にする
                    if (name.StartsWith("Emblem") && name != "Emblem - ")
                        cardType = "紋章";
                    else if (name.StartsWith("Avatar - "))
                        cardType = "ヴァンガード";
                    else if (name.StartsWith(triggerPrefix))
                    {
                        // スタックにのった英雄譚からの誘発型能力
                        name = name.Substring(triggerPrefix.Length);

                        if (App.Cards.TryGetValue(name, out var card))
                        {
                            ViewModel.InvokeSetCard(card);
                            return;
                        }
                    }
                    else if (Card.GetSagaByArtist(name, out string saga))
                    {
                        // 英雄譚はカード名を Automation で探せないため、アーティスト名で 1:1 対応で探す
                        if (CheckAndViewSaga(saga))
                            return;
                    }

                    if (cardType != null)
                        ViewModel.InvokeSetMessage(cardType);

                    return;
                }
                Debug.WriteLine(name);
#if DEBUG
                Debug.WriteLine("Automation event handlers (NameChanged) = " +
                    (GetListeners()?.Count).GetValueOrDefault() + " @ T" + Thread.CurrentThread.ManagedThreadId);
#endif

                SearchCardName();
            }

            /// <summary>
            /// 現在のカードのカードタイプが英雄譚であるかどうかをチェックし、そうであるなら、指定したカード名のカードを表示します。
            /// </summary>
            private bool CheckAndViewSaga(string cardName)
            {
                if (App.Cards.TryGetValue(cardName, out var foundCard))
                {
                    // 英雄譚かどうかをチェックする
                    AutomationElement element = null;
                    using (cacheReq.Activate())
                    {
                        element = previewWnd?.FindFirst(TreeScope.Descendants,
                            new PropertyCondition(AutomationElement.AutomationIdProperty, "CardType"));
                    }
                    string cardType = GetNamePropertyValue(element);

                    if (cardType != null && cardType.EndsWith("Saga"))
                    {
                        ViewModel.InvokeSetCard(foundCard);
                        return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// UI テキストからカード名の候補となる文字列を取得します。
            /// </summary>
            private static string GetNamePropertyValue(AutomationElement element)
            {
                string name = null;
                try
                {
                    name = element?.Cached.Name;
                }
                catch
                {
                    Debug.WriteLine("キャッシュされた Name プロパティ値の取得に失敗しました。");
                    return null;
                }

                if (name == null)
                    return string.Empty;

                // 特殊文字を置き換える (アキュート・アクセントつきの文字など)
                return Card.NormalizeName(name);
            }

            /// <summary>
            /// UI Automation イベントハンドラーを削除し、<see cref="AutomationElement"/> への参照を解放します。
            /// </summary>
            private void ReleaseAutomationElement()
            {
                if (previewWnd != null)
                {
                    previewWnd = null;
                    Automation.RemoveAllEventHandlers();
#if DEBUG
                    Debug.WriteLine("Automation event handlers (after remove) = " +
                        (GetListeners()?.Count).GetValueOrDefault() + " @ T" + Thread.CurrentThread.ManagedThreadId);
#endif
                }
            }

#if DEBUG
            private System.Collections.ArrayList GetListeners()
            {
                var assembly = Assembly.GetAssembly(typeof(Automation));
                var type = assembly.GetType("MS.Internal.Automation.ClientEventManager");
                var field = type.GetField("_listeners", BindingFlags.Static | BindingFlags.NonPublic);
                var listeners = field.GetValue(null) as System.Collections.ArrayList;
                return listeners;
            }
#endif
        }
    }
}