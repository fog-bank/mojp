using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Linq;
#if !OFFLINE
using System.Net.Http;
#endif

namespace Mojp
{
    /// <summary>
    /// WPF アプリケーションをカプセル化し、カードテキストデータを管理します。
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// カードの英語名から、英語カード名・日本語カード名・日本語カードテキストを検索します。
        /// </summary>
        public static Dictionary<string, Card> Cards { get; } = new Dictionary<string, Card>(23199);

        /// <summary>
        /// 代替テキスト検索の主キーです。
        /// </summary>
        public static HashSet<string> AltCardKeys { get; } = new HashSet<string>();

        /// <summary>
        /// 代替テキスト検索のサブキーです。
        /// </summary>
        public static HashSet<string> AltCardSubKeys { get; } = new HashSet<string>();

        /// <summary>
        /// 代替テキストによるカード検索を行います。
        /// </summary>
        public static Dictionary<string, AltCard> AltCards { get; } = new Dictionary<string, AltCard>(1034);

        /// <summary>
        /// このアプリの設定を取得します。
        /// </summary>
        public static SettingsCache SettingsCache { get; } = new SettingsCache();

#if !OFFLINE
        /// <summary>
        /// このアプリで共有する <see cref="System.Net.Http.HttpClient"/> を取得します。
        /// </summary>
        public static Lazy<HttpClient> HttpClient { get; } = new Lazy<HttpClient>();
#endif

        /// <summary>
        /// <see cref="App"/> に関連付けられている <see cref="Dispatcher"/> を取得します。
        /// </summary>
        public static Dispatcher CurrentDispatcher => Current.Dispatcher;

        /// <summary>
        /// このアプリのメインウィンドウを取得します。
        /// </summary>
        public static MainWindow CurrentMainWindow => Current.MainWindow as MainWindow;

        /// <summary>
        /// このアプリが ClickOnce で実行されているかどうかを示す値を取得します。
        /// </summary>
        public static bool IsClickOnce { get; private set; }

#if !OFFLINE
        /// <summary>
        /// このアプリが ClickOnce で実行されている場合のデータディレクトリへのパスを取得します。
        /// </summary>
        private static string DataDirectory { get; set; }
#endif

        /// <summary>
        /// Application Entry Point.
        /// </summary>
        [STAThread]
        public static void Main()
        {
            const string AppGuid = "{B99B5E4E-C7AA-4D4C-8674-DBAC723D29D5}";

            // 多重起動防止
            using (var sema = new Semaphore(0, 1, AppGuid, out bool createdNew))
            {
                if (createdNew)
                {
#if !OFFLINE
                    var domain = AppDomain.CurrentDomain;
                    IsClickOnce = domain.ActivationContext?.Identity.FullName != null;

                    if (IsClickOnce)
                        DataDirectory = domain.GetData("DataDirectory") as string;
#endif

                    var app = new App();
                    app.InitializeComponent();
                    app.Run();
                }
            }
        }

#if !OFFLINE
        /// <summary>
        /// ClickOnce アプリの場合はデータディレクトリ下のパスになるようにパスを調整します。
        /// </summary>
        public static string GetPath(string filename) => IsClickOnce ? Path.Combine(DataDirectory, filename) : filename;
#endif

#if !OFFLINE
        /// <summary>
        /// カードテキストデータを XML に保存します。
        /// </summary>
        public static void SaveAsXml(string path)
        {
            var cardsElem = new XElement("cards");

            foreach (var card in Cards.Values)
                cardsElem.Add(card.ToXml());

            foreach (var alt in AltCards.Values)
                cardsElem.Add(alt.ToXml());

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), cardsElem);
            doc.Save(path);
        }
#endif

#if !OFFLINE
        /// <summary>
        /// WHISPER の検索結果を格納したテキストファイルからカードテキストデータを構築します。
        /// </summary>
        public static void SetCardInfosFromWhisper(StreamReader sr)
        {
            Cards.Clear();

            foreach (var card in Card.ParseWhisper(sr))
            {
                if (Cards.ContainsKey(card.Name))
                {
                    Debug.WriteLine(card.Name + " を二重登録しようとしています。");
                    continue;
                }
                Cards.Add(card.Name, card);
            }
        }
#endif

        /// <summary>
        /// XML オブジェクトからカードテキストデータを構築します。
        /// </summary>
        public static void SetCardInfosFromXml(XDocument doc)
        {
            Cards.Clear();
            AltCardKeys.Clear();
            AltCardSubKeys.Clear();
            AltCards.Clear();

            var node = doc.Element("cards");

            if (node != null)
            {
                foreach (var element in node.Elements())
                {
                    switch (element.Name.LocalName)
                    {
                        case "card":
                            var card = Card.FromXml(element);
                            Cards.Add(card.Name, card);
                            break;

                        case "alt":
                            string key = (string)element.Attribute("key");
                            string sub = (string)element.Attribute("sub");
                            AltCardKeys.Add(key);
                            AltCardSubKeys.Add(sub);
                            AltCards.Add(key + sub, new AltCard(key, sub, (string)element.Attribute("name")));
                            break;
                    }
                }
            }
        }

#if OFFLINE
        /// <summary>
        /// 埋め込まれた XML ファイルからカードテキストデータを構築します。
        /// </summary>
        public static void SetCardInfosFromResource()
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Mojp.cards.xml");

            if (stream != null)
            {
                using (stream)
                {
                    var xml = XDocument.Load(stream);
                    SetCardInfosFromXml(xml);
                }
            }
        }
#endif

#if !OFFLINE
        /// <summary>
        /// XML ファイルからカードテキストデータを構築します。
        /// </summary>
        public static void SetCardInfosFromXml(string file) => SetCardInfosFromXml(XDocument.Load(file));
#endif

#if !OFFLINE
        /// <summary>
        /// このアプリのバージョンが最新かどうかを確認します。
        /// </summary>
        /// <param name="acceptsPrerelease">開発版も含めて確認する場合は true 。</param>
        /// <returns>これより上のバージョンがあった場合は true 。</returns>
        public static async Task<bool> IsOutdatedRelease(bool acceptsPrerelease)
        {
            if (IsClickOnce)
                return false;

            string response = null;
            try
            {
                response = await HttpClient.Value.GetStringAsync("https://fog-bank.github.io/mojp/version.txt");
            }
            catch { Debug.WriteLine("HTTPS アクセスに失敗しました。"); }

            if (response == null)
                return false;

            var attr = typeof(App).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
            var current = new Version(attr.Version);

            // 行ごとにバージョン番号を記載し、第一行は安定版の番号にしておく
            var versions = response.Split(Environment.NewLine.ToCharArray(), 3, StringSplitOptions.RemoveEmptyEntries);
            string version = null;

            if (versions.Length >= 1)
                version = versions[0];

            if (acceptsPrerelease && versions.Length >= 2)
                version = versions[1];

            return Version.TryParse(version, out var latest) && current < latest;
        }
#endif

        /// <summary>
        /// 指定した名前のプロセスを探し、最初に見つかった <see cref="Process"/> オブジェクトを返します。
        /// </summary>
        /// <param name="processName">プロセスの名前。</param>
        public static Process GetProcessByName(string processName)
        {
            Process targetProc = null;

            foreach (var proc in Process.GetProcesses())
            {
                if (targetProc == null && string.Equals(proc.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                    targetProc = proc;
                else
                    proc.Dispose();
            }
            return targetProc;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (Settings.Default.UpgradeRequired)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpgradeRequired = false;
            }
            SettingsCache.Read();
        }

        protected override void OnExit(ExitEventArgs e)
        {
#if !OFFLINE
            if (HttpClient.IsValueCreated)
                HttpClient.Value.Dispose();
#endif

            SettingsCache.Write();
            Settings.Default.Save();

#if !OFFLINE
            if (SettingsCache.GetCardPrice)
                CardPrice.SaveCacheData();
#endif
            Debug.WriteLine("Card.PropertyChanged = { " + string.Join(", ", Cards.Values.Where(card => card.IsObserved)) + " }");

            base.OnExit(e);
        }
    }
}