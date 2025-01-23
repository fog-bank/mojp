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
    private class AutomationHandler : IDisposable
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

            RegisterEventHandler();
        }

        private MainViewModel ViewModel { get; }

        /// <summary>
        /// 実行中の MO を検索します。
        /// </summary>
        public async Task CaptureMagicOnline()
        {
            // MO のプロセス ID を取得する
            if (mtgoProc == null || mtgoProc.HasExited)
            {
                Debug.WriteLine("MO 検索開始 @ T" + Thread.CurrentThread.ManagedThreadId);

                mtgoProc?.Dispose();
                mtgoProc = await Task.Run(() => App.GetProcessByName("mtgo")).ConfigureAwait(false);

                Debug.WriteLine("MO 検索完了 @ T" + Thread.CurrentThread.ManagedThreadId);

                if (mtgoProc == null)
                {
                    ViewModel.InvokeSetMessage("起動中のプロセスの中に MO が見つかりません。");
                    return;
                }
                ViewModel.InvokeSetMessage("カードを右クリックするとカードテキストを表示します。");
            }
#if DEBUG
            int handlers = GetListenersCount();
            Debug.WriteLineIf(handlers > 1, "Automation event handlers = " + handlers);
#endif
        }

        /// <summary>
        /// 各種リソースを解放します。
        /// </summary>
        public void Dispose()
        {
            UnregisterEventHandler();

            mtgoProc?.Dispose();
            mtgoProc = null;
            eventCacheReq = null;
            cacheReq = null;
            textBlockCondition = null;
        }

        private void OnMenuOpened(object sender, AutomationEventArgs e)
        {
            if (sender is not AutomationElement menu || mtgoProc == null)
                return;

            // 右クリックメニューの最初の TextBlock 要素を取得する
            AutomationElement element = null;
            try
            {
                Debug.WriteLine(
                    "[MenuOpendEvent] Proc = " + menu.Cached.ProcessId + " @ T" + Thread.CurrentThread.ManagedThreadId);

                if (menu.Cached.ProcessId != mtgoProc.Id)
                    return;

                using (cacheReq.Activate())
                    element = menu?.FindFirst(TreeScope.Descendants, textBlockCondition);
            }
            catch { Debug.WriteLine("TextBlock 要素の取得中にエラーが起きました。"); }

            if (element == null)
            {
                Debug.WriteLine("TextBlock 要素の取得に失敗しました。");
                return;
            }
            string name = GetNamePropertyValue(element);

            if (name == null)
                return;

            Debug.WriteLine("[Menu] " + name.Replace(Environment.NewLine, "\\n"));

            if (TryFetchCard(name))
            {
                return;
            }
            else if (name == "Face-down card.")
            {
                // 裏向き
                SearchAll();
            }
            else
            {
                // カード名ではなかったときに強制的に空表示にする
                ViewModel.InvokeSetCard(null);
            }

            void SearchAll()
            {
                AutomationElementCollection elements = null;
                try
                {
                    using (cacheReq.Activate())
                        elements = menu?.FindAll(TreeScope.Descendants, textBlockCondition);
                }
                catch { Debug.WriteLine("TextBlock 要素の全取得中にエラーが起きました。"); }

                if (elements == null)
                    return;

                for (int i = 0; i < elements.Count; i++)
                {
                    string name = GetNamePropertyValue(elements[i]);

                    // キャッシュ無効化時
                    if (name == null)
                        return;

                    Debug.WriteLineIf(i > 0, "[Menu] " + name.Replace(Environment.NewLine, "\\n"));

                    if (App.TryGetCard(name, out var card))
                    {
                        Debug.WriteLine(card.Name);
                        ViewModel.InvokeSetCard(card);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 指定した文字列がカード名を意味しているかどうかを調べ、そうならばカードを表示します。
        /// </summary>
        /// <returns>指定した文字列がカード名を指すものだった場合は true 。</returns>
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
            if (value.StartsWith(triggerPrefix))
            {
                value = value.Substring(triggerPrefix.Length);
                Debug.WriteLine("Triggered ability => " + value);
                return ViewCardDirectly(value);
            }

            // 分割カード系
            int slashIndex = value.IndexOf('/');
            if (slashIndex > 0 && char.IsLetter(value[slashIndex - 1]))
            {
                // "/" があるので、index が 0 のときは除く
                value = value.Substring(0, slashIndex);
                Debug.WriteLine("Split card => " + value);

                if (ViewCardDirectly(value))
                    return true;
            }

            // できるだけ左クリックメニューによる表示変化を避ける
            if (value.EndsWith("Cast") || value.EndsWith(".") || value.StartsWith(" "))
                return !value.StartsWith("Face-down card.");

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

            // 汎用トークン
            if (value.EndsWith(" Token"))
            {
                ViewModel.InvokeSetMessage("トークン");
                Debug.WriteLine(value);
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

        private void RegisterEventHandler()
        {
            Task.Run(() =>
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
            }).Wait();
        }

        /// <summary>
        /// UI Automation イベントハンドラーを削除します。
        /// </summary>
        private void UnregisterEventHandler()
        {
            Task.Run(() =>
            {
                Automation.RemoveAllEventHandlers();
#if DEBUG
                Debug.WriteLine("Automation event handlers (after remove) = " +
                    GetListenersCount() + " @ T" + Thread.CurrentThread.ManagedThreadId);
#endif
            }).Wait();
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