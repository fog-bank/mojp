using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            private readonly string PromoCollectorNumber = Settings.Default.PromoCodeNumber;
            private Process mtgoProc;
            private AutomationElement previewWnd;
            private CacheRequest eventCacheReq = new CacheRequest();
            private CacheRequest cacheReq = new CacheRequest();

            // Preview Window を探す
            private Condition prevWndCondition = new AndCondition(
                new PropertyCondition(AutomationElement.ClassNameProperty, "Window"),
                new PropertyCondition(AutomationElement.AutomationIdProperty, string.Empty),
                new OrCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "Preview"),
                    new PropertyCondition(AutomationElement.NameProperty, string.Empty)));

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
                if (App.Cards.Count == 0)
                {
                    ViewModel.InvokeSetMessage(
                        "同梱のカードテキストデータ (cards.xml) を取得できません。" +
                        "セキュリティ対策ソフトによってブロックされている可能性があります。" +
                        Environment.NewLine + "[場所] " + 
                        Path.Combine(Path.GetDirectoryName(typeof(App).Assembly.Location), "cards.xml"));
                    return;
                }

                var currentPreviewWnd = previewWnd;

                // MO のプロセス ID を取得する
                if (mtgoProc == null || mtgoProc.HasExited)
                {
                    mtgoProc?.Dispose();
                    mtgoProc = App.GetProcessByName("mtgo");

                    if (mtgoProc == null)
                    {
                        ReleaseAutomationElement();
                        ViewModel.InvokeSetMessage("起動中のプロセスの中に MO が見つかりません。");
                        return;
                    }
                }

                // "Preview" という名前のウィンドウを探す (なぜかルートの子で見つかる)
                AutomationElement newPreviewWnd = null;
                try
                {
                    // Name が "" のときがある？ため、AutomationID で保険をかける
                    newPreviewWnd = AutomationElement.RootElement.FindFirst(TreeScope.Children,
                        new AndCondition(
                            new PropertyCondition(AutomationElement.ProcessIdProperty, mtgoProc.Id),
                            prevWndCondition));
                }
                catch { Debug.WriteLine("Preview Pane の取得に失敗しました。"); }

                // 他のスレッドで処理が先行した
                if (previewWnd != currentPreviewWnd)
                    return;

                if (newPreviewWnd is null)
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
                bool isPromo = false;

                foreach (string value in IterateTextBlocks())
                {
                    if (TryFetchCard(value))
                        return;

                    if (!isPromo && value.Length == PromoCollectorNumber.Length)
                    {
                        isPromo = true;

                        // "0***  /  1158 " のような形式なので、5文字目以降が一致するかどうかで判定
                        for (int i = 4; i < value.Length; i++)
                        {
                            if (value[i] != PromoCollectorNumber[i])
                            {
                                isPromo = false;
                                break;
                            }
                        }
                    }

                    if (!isToken && value.StartsWith("Token"))
                        isToken = true;
                }

                // 一通り探して、カード名が見つからず、プロモやトークンであることが分かった場合
                if (isPromo && App.Cards.TryGetValue("プロモ版のカード", out var prm))
                {
                    ViewModel.InvokeSetCard(prm);
                }
                else if (isToken)
                {
                    ViewModel.InvokeSetMessage("トークン");
                }
                else
                    ViewModel.InvokeSetMessage(string.Empty);
            }

            /// <summary>
            /// 各種リソースを解放します。
            /// </summary>
            public void Release()
            {
                mtgoProc?.Dispose();
                mtgoProc = null;
                eventCacheReq = null;
                cacheReq = null;
                prevWndCondition = null;
                textBlockCondition = null;
                cardTypeCondition = null;
                ReleaseAutomationElement();
            }

            /// <summary>
            /// 変更された要素名がカード名かどうかを調べます。
            /// </summary>
            /// <remarks>AutomationPropertyChangedEventHandler は UI スレッドとは別スレッドで動いている。</remarks>
            private void OnAutomaionNamePropertyChanged(object sender, AutomationPropertyChangedEventArgs e)
            {
#if DEBUG
                int listenerCount = (GetListeners()?.Count).GetValueOrDefault();
                if (listenerCount != 1)
                {
                    Debug.WriteLine("Automation event handlers (NameChanged) = " +
                        listenerCount + " @ T" + Thread.CurrentThread.ManagedThreadId);
                }
#endif
                // CacheRequest で TreeFilter を設定しても、イベントは発生するらしく、代わりに sender が null になっている模様
                if (previewWnd is null || sender == null)
                    return;

                // sender が null でなければ、non-empty な文字列が取得できるはずだが、キャッシュがこける場合もあり。
                // 加えて、e.NewValue と一致するとも限らない。
                //string name = GetNamePropertyValue(sender as AutomationElement);
                //Debug.Assert(!string.IsNullOrEmpty(GetNamePropertyValue(sender as AutomationElement)));

                string name = Card.NormalizeName(e.NewValue as string);

                if (string.IsNullOrEmpty(name))
                    return;

                Debug.WriteLine("[NameChanged] " + name.Replace(Environment.NewLine, "\\n"));

                //if (TryFetchCard(name))
                //    return;

                // コピートークンでない普通のトークンである可能性があるので、全体走査する
                // プロモ版を示唆するテキストの場合も全体走査
                // （KHM以降、プロモにもコレクター番号が振られるようになったが、STXと被り捲りなので、廃止）
                //if (name.StartsWith("Token") || name == "PRM" || name == PromoCollectorNumber)
                //    SearchCardName();

                // 拡張アート枠のコレクター番号問題のため、常時全体走査に変更 (since VOW)
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

                    Debug.WriteLine("[FindAll] " + name.Replace(Environment.NewLine, "\\n"));
                    yield return name;
                }
            }

            /// <summary>
            /// 指定した文字列がカード名を意味しているかどうかを調べ、そうならばカードを表示します。
            /// </summary>
            /// <returns>指定した文字列がカード名を指すものだった場合は true 。</returns>
            /// <remarks>無限ループになる可能性があるので、<see cref="SearchCardName"/> メソッドは呼ばないこと。</remarks>
            private bool TryFetchCard(string value)
            {
                if (App.Cards.TryGetValue(value, out var card) && !IsKeywordText(value))
                {
                    ViewCard(card);
                    return true;
                }

                // 代替テキストによる検索
                if (App.AltCardKeys.Contains(value) && SearchAltSubKey(value))
                    return true;

                // 英雄譚の誘発型能力 (これだけカードではなく、スタック上にあるのと同じ誘発能力が Preview に表示される)
                const string triggerPrefix = "Triggered ability from ";
                if (value.StartsWith(triggerPrefix))
                {
                    value = value.Substring(triggerPrefix.Length);
                    Debug.WriteLine(value);

                    return ViewCardDirectly(value);
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

                bool SearchAltSubKey(string altKey)
                {
                    foreach (string text in IterateTextBlocks())
                    {
                        // コレクター番号かぶり対策
                        if (App.Cards.TryGetValue(text, out var card) && !IsKeywordText(text))
                        {
                            ViewCard(card);
                            return true;
                        }

                        // 両面カード対策
                        if (App.AltCardKeys.Contains(text))
                        {
                            Debug.WriteLineIf(altKey != text, altKey + " -> " + text);
                            altKey = text;
                        }

                        // 第 2 段階
                        if (App.AltCardSubKeys.Contains(text) && App.AltCards.TryGetValue(altKey + text, out var alt))
                        {
                            Debug.WriteLine(altKey + " " + alt.SubKey + " => " + alt.CardName);

                            if (App.Cards.TryGetValue(alt.CardName, out card))
                            {
                                // エルドレインの王権のカードだったとき、カードタイプが出来事の場合、呪文側を手前に表示する
                                if (alt.SubKey == "ELD" && IsAdventure())
                                {
                                    Debug.WriteLine(altKey + " " + alt.SubKey + " => " + alt.CardName + " => " + card.RelatedCardName);

                                    return ViewCardDirectly(card.RelatedCardName);
                                }
                                ViewModel.InvokeSetCard(card);
                                return true;
                            }
                        }
                    }
                    return false;
                }

                bool ViewCardDirectly(string name)
                {
                    if (App.Cards.TryGetValue(name, out var card2))
                    {
                        ViewModel.InvokeSetCard(card2);
                        return true;
                    }
                    return false;
                }
            }

            /// <summary>
            /// 見つかったカードに基づき、いくつかの例外をチェックしたのち、<see cref="Cards"/> コレクションを更新するようにします。
            /// </summary>
            private void ViewCard(Card card)
            {
                Debug.WriteLine(card.Name);

                // 設定によっては基本土地 5 種の場合に表示を変えないようにする
                if (!ViewModel.ShowBasicLands)
                {
                    switch (card.Name)
                    {
                        case "Plains":
                        case "Island":
                        case "Swamp":
                        case "Mountain":
                        case "Forest":
                            return;
                    }
                }

                // 見つかったカードが現在表示されているカードコレクションに含まれているかどうかを調べる
                var currentCards = ViewModel.Cards;
                if (currentCards != null)
                {
                    // 現在表示されているカードコレクションに含まれている場合、どれをマウスオーバーしているかをはっきりさせるため、
                    // Preview Pane 内で最初に出現するカード名を取得する
                    if (currentCards.Contains(card))
                    {
                        SearchForeground(currentCards[0]);
                        return;
                    }
                }

                // このメソッドの呼び出し前にチェックするようにしたため、コメントアウト
                //if (IsKeywordText(card.Name))
                //    return;

                // ふつうのカード
                ViewModel.InvokeSetCard(card);

                void SearchForeground(Card currentForeground)
                {
                    foreach (string value in IterateTextBlocks())
                    {
                        if (App.Cards.TryGetValue(value, out var card))
                        {
                            if (card != currentForeground)
                                ViewModel.InvokeSetCard(card);

                            return;
                        }
                    }
                }
            }

            /// <summary>
            /// このテキストがカード名ではなく、キーワード能力（あるいはウギンの運命プロモコード）であるかどうかを調べます。
            /// </summary>
            /// <remarks>
            /// 一部の Lv カードや変容カードで、キーワード能力と同名のカードの名前が検出される問題や、
            /// ウギンの運命プロモカードで Catch+Release が表示されてしまう問題を回避
            /// </remarks>
            private bool IsKeywordText(string cardName)
            {
                return cardName switch
                {
                    "Flash" => ContainsText("IKO"),
                    "Lifelink" => ContainsText("Transcendent Master", "Sorin, Vengeful Bloodlord", "IKO"),
                    "Release" => IsUginFatePromo(),
                    "Oubliette" => ContainsText("Trapped Entry"),
                    "Vigilance" => ContainsText("Ikiral Outrider", "IKO"),
                    _ => false,
                };

                // 指定したテキストが Preview Pane に含まれているどうかを調べます。
                bool ContainsText(string text, string text2 = null, string text3 = null)
                {
                    foreach (string value in IterateTextBlocks())
                    {
                        if (value == text)
                            return true;

                        if (text2 != null && value == text2)
                            return true;

                        if (text3 != null && value == text3)
                            return true;
                    }
                    return false;
                }

                // 現在のカードがウギンの運命プロモカードであるかどうかを調べます。
                bool IsUginFatePromo()
                {
                    bool isPromo = false;
                    bool containsCatch = false;

                    foreach (string value in IterateTextBlocks())
                    {
                        switch (value)
                        {
                            case "PRM":
                                isPromo = true;
                                break;

                            // Catch があれば、"Release" はプロモを指すテキストではなく、分割カード Catch+Release である
                            case "Catch":
                                containsCatch = true;
                                break;
                        }
                    }
                    return isPromo && !containsCatch;
                }
            }

            /// <summary>
            /// 現在のカードのカードタイプが英雄譚であるかどうかをチェックし、そうであるなら、指定したカード名のカードを表示します。
            /// </summary>
            //private bool ValidateAndViewSaga(string cardName)
            //{
            //    if (App.Cards.TryGetValue(cardName, out var foundCard))
            //    {
            //        // Automation ID が CardType の値を調べて、英雄譚かどうかをチェックする
            //        AutomationElement element = null;
            //        try
            //        {
            //            using (cacheReq.Activate())
            //            {
            //                element = previewWnd?.FindFirst(TreeScope.Descendants, cardTypeCondition);
            //            }
            //        }
            //        catch { Debug.WriteLine("CardType 要素の取得に失敗しました。"); }

            //        string cardType = GetNamePropertyValue(element);

            //        if (cardType != null && cardType.EndsWith("Saga"))
            //        {
            //            ViewModel.InvokeSetCard(foundCard);
            //            return true;
            //        }
            //    }
            //    return false;
            //}

            /// <summary>
            /// Automation ID が CardType の値を調べて、出来事かどうかをチェックする
            /// </summary>
            private bool IsAdventure()
            {
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

                return cardType != null && (cardType.EndsWith("Adventure") || cardType.Contains("Adventure"));
            }

            /// <summary>
            /// UI テキストからカード名の候補となる文字列を取得します。
            /// </summary>
            private static string GetNamePropertyValue(AutomationElement element)
            {
                string name;
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
                if (previewWnd is null)
                    return;

                previewWnd = null;
                Automation.RemoveAllEventHandlers();
#if DEBUG
                Debug.WriteLine("Automation event handlers (after remove) = " +
                    (GetListeners()?.Count).GetValueOrDefault() + " @ T" + Thread.CurrentThread.ManagedThreadId);
#endif
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