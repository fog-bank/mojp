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
        /// カードテキストデータを指定した XML ファイルで修正します。
        /// </summary>
        public static void FixCardInfo(string file)
        {
            var doc = XDocument.Load(file);

            // カードの追加
            foreach (var node in doc.Root.Element("add").Elements("card"))
            {
                var card = Card.FromXml(node);
                Debug.WriteLineIf(card.RelatedCardName != null && !cards.ContainsKey(card.RelatedCardName), card.Name + " の関連カードが見つかりません。");

                if (cards.ContainsKey(card.Name))
                {
                    if (card.EqualsStrict(cards[card.Name]))
                        Debug.WriteLine(card.Name + " には既に同名カードが存在します。");
                    else
                        Debug.WriteLine(card.Name + " には既に同名カードが存在しますが、カード情報が一致しません。");
                }
                else
                    cards.Add(card.Name, card);
            }

            // P/T だけ追加
            foreach (var node in doc.Root.Element("add").Elements("pt"))
            {
                string name = (string)node.Attribute("name");

                if (cards.TryGetValue(name, out var card))
                {
                    Debug.WriteLineIf(card.PT != null, card.Name + " には既に P/T の情報があります。");
                    card.PT = (string)node.Attribute("pt");
                }
                else
                    Debug.WriteLine("P/T 情報の追加先となる " + name + " のカード情報がありません。");
            }

            // Wiki へのリンクだけ追加
            foreach (var node in doc.Root.Element("add").Elements("wikilink"))
            {
                string name = (string)node.Attribute("name");

                if (cards.TryGetValue(name, out var card))
                {
                    Debug.WriteLineIf(card.WikiLink != null, card.Name + " には既に wiki へのリンク情報があります。");
                    card.WikiLink = (string)node.Attribute("wikilink");
                }
                else
                    Debug.WriteLine("リンク情報の追加先となる " + name + " のカード情報がありません。");
            }

            // カードデータの差し替え
            var replacedNodes = new XElement("replace");
            var identicalNodes = new XElement("identical");

            foreach (var node in doc.Root.Element("replace").Elements("card"))
            {
                var card = Card.FromXml(node);
                Debug.WriteLineIf(card.RelatedCardName != null && !cards.ContainsKey(card.RelatedCardName), card.Name + " の関連カードが見つかりません。");

                if (cards.ContainsKey(card.Name))
                {
                    if (card.EqualsStrict(cards[card.Name]))
                    {
                        // WHISPER が対応した場合に appendix.xml から外したい
                        identicalNodes.Add(new XElement("card", new XAttribute("name", card.Name)));
                        Debug.WriteLine(card.Name + " は置換する必要がありません。");
                    }
                    else
                    {
                        // 置換後と置換前で diff できるようにしたい
                        replacedNodes.Add(cards[card.Name].ToXml());
                        cards[card.Name] = card;
                    }
                }
                else
                    Debug.WriteLine("置換先となる " + card.Name + " のカード情報がありません。");
            }

            // カードタイプだけ書き換え
            foreach (var node in doc.Root.Element("replace").Elements("type"))
            {
                string name = (string)node.Attribute("name");

                if (cards.TryGetValue(name, out var card))
                {
                    string type = (string)node.Attribute("type");

                    if (card.Type == type)
                    {
                        identicalNodes.Add(node);
                        Debug.WriteLine(card.Name + " のカードタイプは置換する必要がありません。");
                    }
                    else
                    {
                        replacedNodes.Add(new XElement("type", new XAttribute("name", name), new XAttribute("type", cards[card.Name].Type)));
                        cards[card.Name].Type = type;
                    }
                }
                else
                    Debug.WriteLine("カードタイプの置換先となる " + name + " のカード情報がありません。");
            }

            var root = new XElement("mojp", replacedNodes, identicalNodes);
            var result = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
            result.Save("appendix_result.xml");

            // カードの削除
            foreach (var node in doc.Root.Elements("remove").Elements("card"))
            {
                string name = (string)node.Attribute("name");
                Debug.WriteLineIf(!cards.ContainsKey(name), name + " は既にカードリストに含まれていません。");
                cards.Remove(name);
            }

            // 暫定日本語カード名の削除
            foreach (var node in doc.Root.Elements("remove").Elements("jaName"))
            {
                string name = (string)node.Attribute("name");

                if (cards.TryGetValue(name, out var card))
                {
                    Debug.WriteLineIf(card.Name == card.JapaneseName, name + " には既に日本語名がありません。");
                    card.JapaneseName = card.Name;
                }
                else
                    Debug.WriteLine(name + " は既にカードリストに含まれていません。");
            }
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