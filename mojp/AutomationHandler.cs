using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace Mojp;

partial class MainViewModel
{
    /// <summary>
    /// オートメーション関連の操作を行います。
    /// </summary>
    private class AutomationHandler
    {
        private Process mtgoProc;
        private CacheRequest eventCacheReq = new();
        private CacheRequest cacheReq = new();

        // テキストが空でなく、特定の UI 要素でない TextBlock をすべて拾う
        private Condition textBlockCondition = new AndCondition(
            new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock"),
            new PropertyCondition(AutomationElement.AutomationIdProperty, string.Empty),
            new NotCondition(new PropertyCondition(AutomationElement.NameProperty, string.Empty)));

        public AutomationHandler(MainViewModel viewModel)
        {
            Debug.WriteLine("AutomationHandler .ctor @ T" + Thread.CurrentThread.ManagedThreadId);
            ViewModel = viewModel;

            eventCacheReq.Add(AutomationElement.ProcessIdProperty);

            // FindAll や FindFirst のときに TreeFilter を設定すると、なぜかうまくいかない (カード名をもつ要素が取得できない)
            cacheReq.Add(AutomationElement.NameProperty);
            cacheReq.AutomationElementMode = AutomationElementMode.None;
        }

        private MainViewModel ViewModel { get; }

        /// <summary>
        /// メニューが開いたイベントをサブスクライブします。
        /// </summary>
        public async Task RegisterEventHandler()
        {
            await Task.Run(() =>
            {
                using (eventCacheReq.Activate())
                {
                    Automation.AddAutomationEventHandler(
                        AutomationElement.MenuOpenedEvent, AutomationElement.RootElement, TreeScope.Descendants, OnMenuOpened);
                }
#if DEBUG
                Debug.WriteLine("Automation event handlers (after add) = " +
                    GetListenersCount() + " @ T" + Thread.CurrentThread.ManagedThreadId);
#endif
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 実行中の MO を検索し、その後はプロセスが終了したかを調べます。
        /// </summary>
        public async Task FindMagicOnline()
        {
            var proc = mtgoProc;

            if (proc != null && await Task.Run(() => proc.HasExited).ConfigureAwait(false))
            {
                // mtgoProc = null;
                if (Interlocked.CompareExchange(ref mtgoProc, null, proc) == proc)
                {
                    proc.Dispose();
                    Debug.WriteLine("MO プロセス終了 @ T" + Thread.CurrentThread.ManagedThreadId);
                }
            }

            if (mtgoProc == null)
            {
                // MO のプロセスを取得する
                proc = await Task.Run(static () => App.GetProcessByName()).ConfigureAwait(false);

                if (proc == null)
                {
                    ViewModel.InvokeSetMessage("起動中のプロセスの中に MO が見つかりません。");
                    return;
                }
                Debug.WriteLine("MO プロセス検出 @ T" + Thread.CurrentThread.ManagedThreadId);

                // mtgoProc = proc;
                var oldProc = Interlocked.CompareExchange(ref mtgoProc, proc, null);

                if (oldProc == null)
                {
                    //proc.EnableRaisingEvents = true;
                    //proc.Exited += (_, _) =>
                    //{
                    //    mtgoProc?.Dispose();
                    //    mtgoProc = null;
                    //    Debug.WriteLine("MO プロセス終了 @ T" + Thread.CurrentThread.ManagedThreadId);

                    //    ViewModel.RestartRefreshTimer();
                    //};
                    //ViewModel.StopRefreshTimer();

                    ViewModel.InvokeSetMessage("カードを右クリックするとカードテキストを表示します。");
                }
                else
                    proc.Dispose();
            }
        }

        /// <summary>
        /// 各種リソースを解放します。
        /// </summary>
        public async Task Dispose()
        {
            mtgoProc?.Dispose();
            mtgoProc = null;
            eventCacheReq = null;
            cacheReq = null;
            textBlockCondition = null;

            await UnregisterEventHandler();
        }

        // Automation イベント ハンドラーは非 UI スレッドで呼び出される
        private void OnMenuOpened(object sender, AutomationEventArgs e)
        {
            if (mtgoProc == null || sender is not AutomationElement menu)
                return;

            string name = SearchFirstText(menu);

            if (name == null)
                return;

            Debug.WriteLine("[Menu] " + name.Replace(Environment.NewLine, "\\n"));

            // 事前特殊処理（同名カード回避）
            switch (name)
            {
                case "Undo":
                    if (SearchSecondText(menu) == "Concede Game")
                        return;
                    break;

                case "Day":
                case "Night":
                    if (SearchSecondText(menu) == "The Token Set")
                    {
                        TryFetchCard("昼／夜");
                        return;
                    }
                    break;
            }

            if (TryFetchCard(name))
                return;

            // 事後特殊処理
            OnMenuOpendAfter(menu, name);

            // 右クリックメニューの最初の UI テキストを取得する
            string SearchFirstText(AutomationElement menu)
            {
                AutomationElement element = null;
                try
                {
                    Debug.WriteLine(
                        "[MenuOpendEvent] Proc = " + menu.Cached.ProcessId + " @ T" + Thread.CurrentThread.ManagedThreadId);

                    if (menu.Cached.ProcessId != mtgoProc.Id)
                        return null;

                    //long time = Stopwatch.GetTimestamp();

                    using (cacheReq.Activate())
                        element = menu.FindFirst(TreeScope.Descendants, textBlockCondition);

                    //Debug.WriteLine(new TimeSpan((long)((Stopwatch.GetTimestamp() - time) * 10000000.0 / Stopwatch.Frequency)));
                    Debug.WriteLineIf(element is null, "TextBlock 要素の取得に失敗しました。");
                }
                catch { Debug.WriteLine("TextBlock 要素の取得中にエラーが起きました。"); }

                return GetNamePropertyValue(element);
            }

            // メニュー内を全検索しなおし、2 番目の UI テキストを取得する
            string SearchSecondText(AutomationElement menu)
            {
                AutomationElementCollection elements = null;
                try
                {
                    using (cacheReq.Activate())
                        elements = menu.FindAll(TreeScope.Descendants, textBlockCondition);
                }
                catch { Debug.WriteLine("TextBlock 要素の全取得中にエラーが起きました。"); }

                if (elements == null || elements.Count < 2)
                    return null;

                return GetNamePropertyValue(elements[1]);
            }
        }

        /// <summary>
        /// 指定した文字列がカード名を意味しているかどうかを調べ、そうならばカードを表示します。
        /// </summary>
        /// <returns>処理が完了した場合は true 。Automation を使って再検索すべきか、空表示にすべき場合は false 。</returns>
        private bool TryFetchCard(string value)
        {
            if (App.TryGetCard(value, out var card))
            {
                Debug.WriteLine(card.Name);
                ViewModel.InvokeSetCard(card);
                return true;
            }

            // 誘発型能力
            const string triggerPrefix = "Triggered ability from ";
            if (value.StartsWith(triggerPrefix, StringComparison.Ordinal))
            {
                value = value.Substring(triggerPrefix.Length);
                Debug.WriteLine("誘発型能力 => " + value);

                if (ViewCardDirectly(value))
                    return true;
            }

            // 分割カード系（P/T のスラッシュの場合ははじく）
            int slashIndex = value.IndexOf('/');
            if (slashIndex > 0)
            {
                string split = null;
                char prevSlash = value[slashIndex - 1];

                if (char.IsLetter(prevSlash))
                {
                    split = value.Substring(0, slashIndex);
                }
                else if (prevSlash == ' ' && value.Length > slashIndex + 1 && value[slashIndex + 1] == '/')
                {
                    split = value.Substring(0, slashIndex - 1);
                }

                if (split != null)
                {
                    Debug.WriteLine("分割カード => " + split);

                    if (ViewCardDirectly(split))
                        return true;
                }
            }

            // 汎用トークン
            if (value.EndsWith(" Token", StringComparison.Ordinal))
            {
                ViewModel.InvokeSetMessage("トークン");
                Debug.WriteLine("トークン => " + value);
                return true;
            }

            // 紋章
            if (value.StartsWith("Emblem", StringComparison.Ordinal))
            {
                ViewModel.InvokeSetMessage("紋章");
                Debug.WriteLine("紋章 => " + value);
                return true;
            }

            // ヴァンガード
            if (value.StartsWith("Avatar - ", StringComparison.Ordinal))
            {
                ViewModel.InvokeSetMessage("ヴァンガード");
                Debug.WriteLine("ヴァンガード => " + value);
                return true;
            }
            return false;

            bool ViewCardDirectly(string name)
            {
                if (App.TryGetCard(name, out var card2))
                {
                    ViewModel.InvokeSetCard(card2);
                    return true;
                }
                return false;
            }
        }

        // できるだけ左クリックメニューによる表示変化を避ける
        private void OnMenuOpendAfter(AutomationElement menu, string value)
        {
            switch (value)
            {
                case "Face-down card.":
                    SearchAsFaceDownCard(menu);
                    return;

                case "Event Ticket":
                case "Play Point":
                case "Treasure Chest Booster":
                    return;

                case "Open a new copy of this deck":
                case "Select All":
                case "Sort":
                case "Start a New Deck":
                    // 通常の右クリックメニューは分かる範囲でスルー
                    return;
            }

            if (value.EndsWith(".", StringComparison.Ordinal))
                return;

            if (value.EndsWith("Cast", StringComparison.Ordinal) ||
                value.EndsWith("Play", StringComparison.Ordinal) ||
                value.StartsWith("Put ", StringComparison.Ordinal) ||
                value.StartsWith("Attack ", StringComparison.Ordinal))
                return;

            // 部屋
            if (value.StartsWith("Unlock ", StringComparison.Ordinal))
                return;

            // 機体
            if (value.StartsWith("Crew ", StringComparison.Ordinal))
                return;

            // 宇宙船
            if (value.StartsWith("Station (", StringComparison.Ordinal))
                return;

            // カード名を判別できなかったときに強制的に空表示にする
            ViewModel.InvokeSetCard(null);

            // 裏向きのカードの正体探し
            void SearchAsFaceDownCard(AutomationElement menu)
            {
                AutomationElementCollection elements = null;
                try
                {
                    using (cacheReq.Activate())
                        elements = menu.FindAll(TreeScope.Descendants, textBlockCondition);
                }
                catch { Debug.WriteLine("TextBlock 要素の全取得中にエラーが起きました。"); }

                if (elements == null)
                    return;

                for (int i = 1; i < elements.Count; i++)
                {
                    string name = GetNamePropertyValue(elements[i]);

                    if (name == null)
                        return;

                    Debug.WriteLine("[Menu] " + name.Replace(Environment.NewLine, "\\n"));

                    if (App.TryGetCard(name, out var card))
                    {
                        Debug.WriteLine(card.Name);
                        ViewModel.InvokeSetCard(card);
                        return;
                    }
                }
                // 対戦相手の裏向きクリーチャー
                ViewModel.InvokeSetMessage("裏向きのカード");
            }
        }

        /// <summary>
        /// UI Automation イベントハンドラーを別スレッドで削除します。
        /// </summary>
        private async Task UnregisterEventHandler()
        {
            await Task.Run(() =>
            {
#if DEBUG
                Debug.WriteLine("Automation event handlers (before remove) = " +
                    GetListenersCount() + " @ T" + Thread.CurrentThread.ManagedThreadId);
#endif
                // OpenFileDialog を開いた後だと、別スレッドにしないとすごく遅くなる現象あり
                Automation.RemoveAllEventHandlers();
#if DEBUG
                Debug.WriteLine("Automation event handlers (after remove) = " +
                    GetListenersCount() + " @ T" + Thread.CurrentThread.ManagedThreadId);
#endif
            }).ConfigureAwait(false);
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

            // 特殊文字を置き換える (アキュート・アクセントつきの文字など)
            return Card.NormalizeName(name);
        }

#if DEBUG
        private System.Collections.ArrayList GetListeners()
        {
            var assembly = System.Reflection.Assembly.GetAssembly(typeof(Automation));
            var type = assembly.GetType("MS.Internal.Automation.ClientEventManager");
            var field = type.GetField("_listeners", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var listeners = field.GetValue(null) as System.Collections.ArrayList;
            return listeners;
        }

        private int GetListenersCount() => (GetListeners()?.Count).GetValueOrDefault();
#endif
    }
}
