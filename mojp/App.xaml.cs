using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

			foreach (var node in doc.Root.Element("add").Elements("card"))
			{
				var card = Card.FromXml(node);

				if (cards.ContainsKey(card.Name))
				{
					Debug.WriteLineIf(!card.EqualsStrict(cards[card.Name]), card.Name + " には既に別種の同名カードが存在します。");
					continue;
				}
				cards.Add(card.Name, card);
			}

			foreach (var node in doc.Root.Elements("remove").Elements("card"))
				cards.Remove((string)node.Attribute("name"));
			
			var replacedNodes = new XElement("replaced");
			var identicalNodes = new XElement("identical");

			foreach (var node in doc.Root.Element("replace").Elements("card"))
			{
				var card = Card.FromXml(node);

				if (cards.ContainsKey(card.Name))
				{
					if (card.EqualsStrict(cards[card.Name]))
					{
						// WHISPER が対応した場合に appendix.xml から外したい
						identicalNodes.Add(node);
						Debug.WriteLine(card.Name + " は置換する必要がありません。");
					}
					else
					{
						// 置換後と置換前で diff できるようにしたい
						replacedNodes.Add(cards[card.Name].ToXml());
						cards[card.Name] = card;
					}
				}
			}

			var root = new XElement("mojp", doc.Root.Element("add"), replacedNodes, identicalNodes, doc.Root.Element("remove"));
			var result = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
			result.Save("appendix_result.xml");
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
	}
}