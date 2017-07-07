using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace Mojp
{
    /// <summary>
    /// WPF アプリケーションをカプセル化し、カードテキストデータを管理します。
    /// </summary>
    public partial class App : Application
    {
        private static readonly Dictionary<string, Card> cards = new Dictionary<string, Card>();
        private static readonly Lazy<HttpClient> httpClient = new Lazy<HttpClient>();

        /// <summary>
        /// カードの英語名から、英語カード名・日本語カード名・日本語カードテキストを検索します。
        /// </summary>
        public static Dictionary<string, Card> Cards => cards;

        /// <summary>
        /// カードテキストデータを XML に保存します。
        /// </summary>
        public static void SaveAsXml(string path)
        {
            var cardsElem = new XElement("cards");

            foreach (var card in cards.Values)
                cardsElem.Add(card.ToXml());

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), cardsElem);
            doc.Save(path);
        }

        /// <summary>
        /// WHISPER の検索結果を格納したテキストファイルからカードテキストデータを構築します。
        /// </summary>
        public static void SetCardInfosFromWhisper(StreamReader sr)
        {
            cards.Clear();

            foreach (var card in Card.ParseWhisper(sr))
                cards.Add(card.Name, card);
        }

        /// <summary>
        /// XML オブジェクトからカードテキストデータを構築します。
        /// </summary>
        public static void SetCardInfosFromXml(XDocument doc)
        {
            cards.Clear();

            foreach (var xml in doc.Descendants("card"))
            {
                var card = Card.FromXml(xml);
                cards.Add(card.Name, card);
            }
        }

        /// <summary>
        /// XML ファイルからカードテキストデータを構築します。
        /// </summary>
        public static void SetCardInfosFromXml(string file)
        {
            SetCardInfosFromXml(XDocument.Load(file));
        }

        /// <summary>
        /// このアプリのバージョンが最新かどうかを確認します。
        /// </summary>
        /// <param name="acceptsPrerelease">開発版も含めて確認する場合は true 。</param>
        /// <returns>これより上のバージョンが無い場合は true 。</returns>
        public static async Task<bool> IsLatestRelease(bool acceptsPrerelease)
        {
            string response = null;
            try
            {
                response = await httpClient.Value.GetStringAsync("https://fog-bank.github.io/mojp/version.txt");
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

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (Settings.Default.UpgradeRequired)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpgradeRequired = false;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Settings.Default.Save();

            if (httpClient.IsValueCreated)
                httpClient.Value.Dispose();

            base.OnExit(e);
        }
    }
}