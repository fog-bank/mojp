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
        private static volatile bool usingScryfall = false;
        private static DateTime requestTime = DateTime.Now - TimeSpan.FromMilliseconds(100);
        private static HashSet<string> pdLegalCards = null;

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
        public static void SetCardInfosFromXml(string file) => SetCardInfosFromXml(XDocument.Load(file));

        /// <summary>
        /// このアプリのバージョンが最新かどうかを確認します。
        /// </summary>
        /// <param name="acceptsPrerelease">開発版も含めて確認する場合は true 。</param>
        /// <returns>これより上のバージョンが無い場合は true 。</returns>
        public static async Task<bool> IsOutdatedRelease(bool acceptsPrerelease)
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

        /// <summary>
        /// scryfall.com からカード価格を取得します。
        /// </summary>
        /// <returns>取得前のエラーは <see langword="null"/> 。取得時や取得後のエラーは <see cref="string.Empty"/> 。</returns>
        public static async Task<string> GetCardPrice(string cardName)
        {
            const string NoPrice = "― tix";

            if (usingScryfall || DateTime.Now - requestTime < TimeSpan.FromMilliseconds(200) || usingScryfall)
            {
                Debug.WriteLine("× " + cardName + " (" + DateTime.Now.TimeOfDay + ")");
                return null;
            }
            usingScryfall = true;

            string uri = "https://api.scryfall.com/cards/search?order=tix&q=" + Uri.EscapeUriString(cardName.Replace("'", null));
            Debug.WriteLine(uri + " (" + DateTime.Now.TimeOfDay + ")");

            string response = null;
            try
            {
                response = await httpClient.Value.GetStringAsync(uri);
            }
            catch { Debug.WriteLine("HTTPS アクセスに失敗しました。"); }

            requestTime = DateTime.Now;
            usingScryfall = false;

            if (response == null)
                return NoPrice;

            // exact サーチじゃないので、複数ヒットする可能性がある
            const string TotalCardsTag = "\"total_cards\":";
            int startIndex = response.IndexOf(TotalCardsTag);

            if (startIndex == -1)
                return NoPrice;

            if (response.Substring(startIndex + TotalCardsTag.Length, 2) != "1,")
            {
                const string CardTag = "\"name\":";
                startIndex = response.IndexOf(CardTag + "\"" + cardName.Replace("+", " // ") + "\"");

                if (startIndex == -1)
                    return NoPrice;
            }
            const string RelatedTag = "\"related_uris\":";
            int endIndex = response.IndexOf(RelatedTag, startIndex);

            //const string PDLegalityTag = "\"penny\":";
            //const string LegalValue = "\"legal\"";
            //int regalityIndex = response.IndexOf(PDLegalityTag, startIndex, endIndex - startIndex);
            //string isPDRegal = response.IndexOf(LegalValue, regalityIndex, PDLegalityTag.Length + LegalValue.Length) != -1 ? "[PD] " : string.Empty;

            const string TixTag = "\"tix\":";
            startIndex = response.IndexOf(TixTag, startIndex, endIndex - startIndex);

            if (startIndex == -1)
                return NoPrice;

            startIndex = response.IndexOf('"', startIndex + TixTag.Length) + 1;
            endIndex = response.IndexOf('"', startIndex);
            return response.Substring(startIndex, endIndex - startIndex) + " tix";
        }

        public static async void GetPDLegalFile()
        {
            const string PDLegalFileName = "legal_cards.txt";

            if (!File.Exists(PDLegalFileName) || DateTime.Now - File.GetLastWriteTime(PDLegalFileName) > TimeSpan.FromDays(1))
            {
                Stream response = null;
                try
                {
                    response = await httpClient.Value.GetStreamAsync("http://pdmtgo.com/legal_cards.txt");
                }
                catch { Debug.WriteLine("HTTPS アクセスに失敗しました。"); }

                using (var file = File.Create(PDLegalFileName))
                    await response.CopyToAsync(file);
            }

            pdLegalCards = new HashSet<string>();
            var split = new[] { " // " };

            foreach (string line in File.ReadLines(PDLegalFileName))
            {
                if (line.Contains(" // "))
                    pdLegalCards.UnionWith(line.Split(split, StringSplitOptions.RemoveEmptyEntries));
                else
                    pdLegalCards.Add(line);
            }
        }

        public static bool IsPDLegal(Card card) => pdLegalCards != null && pdLegalCards.Contains(card?.Name);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (Settings.Default.UpgradeRequired)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpgradeRequired = false;
            }
            CardPrice.OpenCacheData();

            GetPDLegalFile();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            CardPrice.SaveCacheData();

            Settings.Default.Save();

            if (httpClient.IsValueCreated)
                httpClient.Value.Dispose();

            base.OnExit(e);
        }
    }
}