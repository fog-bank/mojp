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
            private CacheRequest eventCacheReq = new CacheRequest();
            private CacheRequest cacheReq = new CacheRequest();

            // Preview Window を探す
            private Condition previewWndCondition1 = new PropertyCondition(AutomationElement.ClassNameProperty, "Window");
            private Condition previewWndCondition2 = new PropertyCondition(AutomationElement.NameProperty, "Preview");

            // テキストが空でなく、特定の UI 要素でない TextBlock をすべて拾う
            private Condition textBlockCondition = new AndCondition(
                Automation.ContentViewCondition,
                new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock"),
                new PropertyCondition(AutomationElement.AutomationIdProperty, string.Empty),
                new NotCondition(new PropertyCondition(AutomationElement.NameProperty, string.Empty)));

            // 英雄譚のカードタイプを表す要素だけ拾う
            private Condition cardTypeCondition = new PropertyCondition(AutomationElement.AutomationIdProperty, "CardType");

            public AutomationHandler(MainViewModel viewModel)
            {
                ViewModel = viewModel;

                // UI 要素の名前だけキャッシュする
                eventCacheReq.Add(AutomationElement.NameProperty);
                eventCacheReq.AutomationElementMode = AutomationElementMode.None;
                eventCacheReq.TreeFilter = textBlockCondition;

                // FindAll や FindFirst のときに TreeFilter を設定すると、なぜかうまくいかない (カード名をもつ要素が取得できない)
                cacheReq.Add(AutomationElement.NameProperty);
                cacheReq.AutomationElementMode = AutomationElementMode.None;
            }

            private MainViewModel ViewModel { get; }

            /// <summary>
            /// MO の Preview Pane を表すオートメーション要素を検索し、プロパティ変更イベントをサブスクライブします。
            /// </summary>
            public void CapturePreviewPane()
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
                            previewWndCondition1, previewWndCondition2));
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
                using (eventCacheReq.Activate())
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
            /// 現在の Preview Pane に含まれる全テキストからカード名を取得し、表示します。
            /// </summary>
            public void SearchCardName()
            {
                bool isToken = false;

                foreach (string value in IterateTextBlocks())
                {
                    if (TryFetchCard(value))
                        return;

                    if (!isToken && value.StartsWith("Token"))
                        isToken = true;
                }

                // 一通り探して、カード名が見つからず、トークンであることが分かった場合
                if (isToken)
                    ViewModel.InvokeSetMessage("トークン");
            }

            /// <summary>
            /// 各種リソースを解放します。
            /// </summary>
            public void Release()
            {
                eventCacheReq = null;
                cacheReq = null;
                previewWndCondition1 = null;
                previewWndCondition2 = null;
                textBlockCondition = null;
                cardTypeCondition = null;
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
#if DEBUG
                Debug.WriteLine("Automation event handlers (NameChanged) = " +
                    (GetListeners()?.Count).GetValueOrDefault() + " @ T" + Thread.CurrentThread.ManagedThreadId);
#endif

                // キャッシュ無効化時
                if (name == null)
                    return;

                Debug.WriteLine("NameChanged: " + name);

                if (TryFetchCard(name))
                    return;

                // コピートークンでない普通のトークンである可能性があるので、全体走査する
                if (name.StartsWith("Token"))
                    SearchCardName();
            }

            /// <summary>
            /// Preview Pane に含まれる TextBlock 要素の値を列挙します。
            /// </summary>
            private IEnumerable<string> IterateTextBlocks()
            {
                AutomationElementCollection elements = null;
                try
                {
                    using (cacheReq.Activate())
                        elements = previewWnd?.FindAll(TreeScope.Descendants, textBlockCondition);
                }
                catch { Debug.WriteLine("TextBlock 要素の全取得に失敗しました。"); }

                if (elements == null)
                    yield break;

                // 一連のテキストからカード名を探す
                for (int i = 0; i < elements.Count; i++)
                {
                    string name = GetNamePropertyValue(elements[i]);

                    // キャッシュ無効化時
                    if (name == null)
                        yield break;

                    Debug.WriteLine("FindAll: " + name);
                    yield return name;
                }
            }

            /// <summary>
            /// 指定した文字列がカード名を指定しているかどうかを調べ、そうならばカードを表示します。
            /// </summary>
            private bool TryFetchCard(string value)
            {
                // WHISPER データベースからカード情報を取得
                if (App.Cards.TryGetValue(value, out var card))
                {
                    Debug.WriteLine(value);

                    // 設定によっては基本土地 5 種の場合に表示を変えないようにする
                    if (!ViewModel.ShowBasicLands)
                    {
                        switch (value)
                        {
                            case "Plains":
                            case "Island":
                            case "Swamp":
                            case "Mountain":
                            case "Forest":
                                return true;
                        }
                    }

                    // 見つかったカードが現在表示されているカードコレクションに含まれていないなら置き換える
                    var currentCards = ViewModel.Cards;

                    if (currentCards == null)
                    {
                        ViewModel.InvokeSetCard(card);
                    }
                    else if (!currentCards.Contains(card))
                    {
                        // 一部の Lv カードで、キーワード能力と同名のカードの名前が検出される問題を回避
                        if ((card.Name == "Lifelink" && IsKeywordName("Transcendent Master")) ||
                            (card.Name == "Vigilance" && IsKeywordName("Ikiral Outrider")))
                        {
                            return true;
                        }
                        ViewModel.InvokeSetCard(card);
                    }
                    return true;
                }

                // アルティメットマスターズのフルアート版
                if (Card.CheckIfUltimateBoxToppers(value, out string umaCardName))
                {
                    Debug.WriteLine(value + " => " + umaCardName);

                    // アルティメットマスターズのフルアート版もカード名で探せないので、カード番号で区別する
                    // HACK: U01 / 40 みたいな文字が他に出ない前提。念のために UMA のセット記号を確認するかどうか
                    if (App.Cards.TryGetValue(umaCardName, out var umaCard))
                    {
                        ViewModel.InvokeSetCard(umaCard);
                        return true;
                    }
                }

                // 英雄譚
                if (Card.GetSagaByArtist(value, out string saga))
                {
                    Debug.WriteLine(value + " => " + saga);

                    // 英雄譚はカード名を Automation で探せないため、アーティスト名で 1:1 対応で探す
                    if (ValidateAndViewSaga(saga))
                        return true;
                }

                // 英雄譚の誘発型能力
                const string triggerPrefix = "Triggered ability from ";
                if (value.StartsWith(triggerPrefix))
                {
                    value = value.Substring(triggerPrefix.Length);
                    Debug.WriteLine(value);

                    if (App.Cards.TryGetValue(value, out var sagaCard))
                    {
                        ViewModel.InvokeSetCard(sagaCard);
                        return true;
                    }
                }

                // 紋章
                if (value.StartsWith("Emblem") && value != "Emblem - ")
                {
                    ViewModel.InvokeSetMessage("紋章");
                    Debug.WriteLine(value);
                    return true;
                }

                // ヴァンガード
                if (value.StartsWith("Avatar - "))
                {
                    ViewModel.InvokeSetMessage("ヴァンガード");
                    Debug.WriteLine(value);
                    return true;
                }
                return false;
            }

            /// <summary>
            /// 見つかったカード名が、Lv カードに含まれるキーワード能力と同じかどうかを調べます。
            /// </summary>
            private bool IsKeywordName(string lvCardName)
            {
                foreach (string value in IterateTextBlocks())
                {
                    if (value == lvCardName)
                        return true;
                }
                return false;
            }

            /// <summary>
            /// 現在のカードのカードタイプが英雄譚であるかどうかをチェックし、そうであるなら、指定したカード名のカードを表示します。
            /// </summary>
            private bool ValidateAndViewSaga(string cardName)
            {
                if (App.Cards.TryGetValue(cardName, out var foundCard))
                {
                    // Automation ID が CardType の値を調べて、英雄譚かどうかをチェックする
                    AutomationElement element = null;
                    try
                    {
                        using (cacheReq.Activate())
                        {
                            element = previewWnd?.FindFirst(TreeScope.Descendants, cardTypeCondition);
                        }
                    }
                    catch { Debug.WriteLine("CardType 要素の取得に失敗しました。"); }

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