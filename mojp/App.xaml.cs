using System.Collections.Generic;
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
		public static Dictionary<string, Card> Cards
		{
			get { return cards; }
		}

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
	}
}