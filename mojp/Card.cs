using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace Mojp
{
	/// <summary>
	/// MTG のカードを表します。
	/// </summary>
	public class Card : IEquatable<Card>
	{
		public Card()
		{
		}

		/// <summary>
		/// カードの英語名を取得または設定します。
		/// </summary>
		public string Name { get; private set; }

		/// <summary>
		/// カードの日本語名を取得または設定します。
		/// </summary>
		public string JapaneseName { get; set; }

		/// <summary>
		/// カード・タイプを取得または設定します。
		/// </summary>
		public string Type { get; set; }

		/// <summary>
		/// カードのテキストを取得または設定します。
		/// </summary>
		public string Text { get; set; }

		/// <summary>
		/// カードの P/T を取得または設定します。
		/// </summary>
		public string PT { get; set; }

		/// <summary>
		/// 両面カードのもう一方の面など、関連するカードの名前を取得または設定します。
		/// </summary>
		public string RelatedCardName { get; set; }

		/// <summary>
		/// 日本語名/英語名 で MTG Wiki に移動できない場合の、http://mtgwiki.com/wiki/ 以下の代替リンクを取得または設定します。
		/// </summary>
		public string WikiLink { get; set; }

		/// <summary>
		/// 日本語カード名 / 英語カード名、のような表記でカード名を取得します。
		/// </summary>
		public string FullName => HasJapaneseName ? JapaneseName + " / " + Name : Name;

		/// <summary>
		/// カードの各情報をまとめた文字列を生成します。
		/// </summary>
		public string Summary
		{
			get
			{
				var sb = new StringBuilder();

				if (Name != null)
					sb.AppendLine(FullName);

				if (!string.IsNullOrEmpty(Type))
					sb.AppendLine(Type);

				if (!string.IsNullOrEmpty(Text))
					sb.AppendLine(Text);

				if (!string.IsNullOrEmpty(PT))
					sb.AppendLine(PT);

				if (sb.Length > 0)
					sb.Length -= Environment.NewLine.Length;

				return sb.ToString();
			}
		}

		/// <summary>
		/// 公式のカード名日本語訳があるかどうかを示す値を取得します。
		/// </summary>
		public bool HasJapaneseName => JapaneseName != null && Name != JapaneseName && Type != "ヴァンガード";

		/// <summary>
		/// 空のオブジェクトを取得します。オブジェクト自身は読み取り専用になっていませんが、変更しないでください。
		/// </summary>
		public static Card Empty { get; } = new Card();

		/// <summary>
		/// カードの英語名が一致しているかどうかを調べます。
		/// </summary>
		public bool Equals(Card other)
		{
			return !string.IsNullOrWhiteSpace(Name) && string.Equals(Name, other?.Name);
		}

		/// <summary>
		/// <see cref="Card"/> オブジェクトの各メンバの値が一致しているかどうかを調べます。
		/// </summary>
		public bool EqualsStrict(Card other)
		{
			return Equals(other) && JapaneseName == other.JapaneseName && Type == other.Type && Text == other.Text && 
				PT == other.PT && RelatedCardName == other.RelatedCardName && WikiLink == other.WikiLink;
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as Card);
		}

		public override int GetHashCode()
		{
			return Name == null ? 0 : Name.GetHashCode();
		}

		public override string ToString()
		{
			return Name;
		}

		/// <summary>
		/// 後で復元できるように XML ノードに変換します。
		/// </summary>
		/// <returns></returns>
		public XElement ToXml()
		{
			var xml = new XElement("card");

			xml.Add(new XAttribute("name", Name));

			if (JapaneseName != null)
				xml.Add(new XAttribute("jaName", JapaneseName));

			if (Type != null)
				xml.Add(new XAttribute("type", Type));

			if (PT != null)
				xml.Add(new XAttribute("pt", PT));

			if (RelatedCardName != null)
				xml.Add(new XAttribute("related", RelatedCardName));

			if (WikiLink != null)
				xml.Add(new XAttribute("wikilink", WikiLink));

			xml.Add(Text);

			return xml;
		}

		/// <summary>
		/// XML ノードからカード情報を復元します。
		/// </summary>
		public static Card FromXml(XElement cardElement)
		{
			var card = new Card();

			card.Name = (string)cardElement.Attribute("name");
			card.JapaneseName = (string)cardElement.Attribute("jaName");
			card.Type = (string)cardElement.Attribute("type");
			card.PT = (string)cardElement.Attribute("pt");
			card.RelatedCardName = (string)cardElement.Attribute("related");
			card.WikiLink = (string)cardElement.Attribute("wikilink");
			card.Text = cardElement.Value;

			return card;
		}

		/// <summary>
		/// WHISPER の検索結果テキストを解析し、カード情報を列挙します。
		/// </summary>
		/// <remarks>
		/// WHISPER の検索結果の例
		/// 
		/// 　英語名：Fog Bank
		/// 日本語名：濃霧の層（のうむのそう）
		/// 　コスト：(１)(青)
		/// 　タイプ：クリーチャー --- 壁(Wall)
		/// 防衛、飛行
		/// 濃霧の層が与える戦闘ダメージと濃霧の層に与えられるすべての戦闘ダメージを軽減する。
		/// 　Ｐ／Ｔ：0/2
		/// イラスト：Howard Lyon
		/// 　セット：Magic 2013
		/// 　稀少度：アンコモン
		/// 
		/// </remarks>
		public static IEnumerable<Card> ParseWhisper(StreamReader sr)
		{
			var card = new Card();
			var texts = new List<string>();
			int emptyLines = 0;
			Card prevCard = null;

			while (!sr.EndOfStream)
			{
				string line = sr.ReadLine();

				// カードの区切りを認識
				if (string.IsNullOrWhiteSpace(line))
				{
					emptyLines++;
					continue;
				}
				else if (emptyLines > 0)
				{
					// 空白行が入ったので別のカード
					emptyLines = 0;
					prevCard = null;
				}

				// 各行をコロンで区切り、各項目を探す
				var tokens = line.Split('：');

				if (tokens.Length == 1)
				{
					texts.Add(RemoveParenthesis(line));
				}
				else
				{
					switch (tokens[0])
					{
						case "　英語名":
							// 新しいカードの行に入った
							if (card.Name != null)
								yield return ProcessCard(card, texts);

							card = new Card();
							card.Name = tokens[1].Trim();
							texts.Clear();

							if (prevCard != null)
							{
								// 直前に空白行が無く、関連カードである
								card.RelatedCardName = prevCard.Name;
								prevCard.RelatedCardName = card.Name;
							}
							prevCard = card;
							break;

						case "日本語名":
							string jaName = tokens[1];

							// 読みがついているなら、取り除く
							int hiragana = jaName.IndexOf('（');
							if (hiragana >= 0)
								jaName = jaName.Substring(0, hiragana);

							card.JapaneseName = jaName.Trim();
							break;

						case "　Ｐ／Ｔ":
						case "　忠誠度":
							// 両面 PW カードの裏の忠誠度が空白の場合があるので、そのときは設定しない
							if (!string.IsNullOrWhiteSpace(tokens[1]))
							{
								// Lv アップクリーチャーは P/T 行が複数あるので、Lv アップ後の P/T は通常テキストに加える
								if (card.PT == null)
									card.PT = tokens[1];
								else
									texts.Add(tokens[1]);
							}
							break;

						case "　タイプ":
							card.Type = RemoveParenthesis(tokens[1]).Replace("---", "―");
							break;

						case "　コスト":
						case "　色指標":
						case "イラスト":
						case "　セット":
						case "　稀少度":
							break;

						default:
							texts.Add(RemoveParenthesis(line));
							break;
					}
				}
			}

			if (card.Name != null)
				yield return ProcessCard(card, texts);
		}

		private static Card ProcessCard(Card card, IEnumerable<string> texts)
		{
			card.Text = string.Join(Environment.NewLine, texts);

			string wikilink = card.HasJapaneseName ? card.JapaneseName + "/" + card.Name : card.Name;

			// AE 合字処理
			string processed = card.Name.Replace("AE", "Ae");
			if (card.Name != processed)
			{
				card.WikiLink = wikilink;
				card.Name = processed;
			}

			// 次元
			if (card.Type.StartsWith("次元"))
			{
				if (card.WikiLink == null)
					card.WikiLink = wikilink + " (次元カード)";
				else
					card.WikiLink += " (次元カード)";
			}

			return card;
		}

		/// <summary>
		/// サブタイプの英語名を削除した文字列にします。
		/// </summary>
		private static string RemoveParenthesis(string line)
		{
			var text = new StringBuilder(line.Length);
			var parenthesis = new StringBuilder();
			var sb = text;

			for (int i = 0; i < line.Length; i++)
			{
				char c = line[i];
				switch (c)
				{
					case '(':
						sb = parenthesis;
						break;

					case ')':
						bool english = parenthesis.Length >= 2;

						for (int j = 0; j < parenthesis.Length; j++)
						{
							char parChr = parenthesis[j];

							if (parChr >= 'a' && parChr <= 'z')
								continue;

							if (parChr >= 'A' && parChr <= 'Z')
								continue;

							// 「Bolas’s Meditation Realm」「Urza’s」「Power-Plant」
							if (parChr == ' ' || parChr == '\'' || parChr == '-')
								continue;

							english = false;
							break;
						}
						sb = text;

						if (english)
						{
							// サブタイプとタイプの間にあるべき中点が抜けている場合を修正
							if (i + 1 < line.Length && line[i + 1] != '・')
							{
								string follow = line.Substring(i + 1);

								if (follow.StartsWith("クリーチャー") || follow.StartsWith("アーティファクト") || follow.StartsWith("土地") ||
									follow.StartsWith("呪文") || follow.StartsWith("パーマネント") || follow.StartsWith("カード"))
									sb.Append('・');
							}
						}
						else
							sb.Append('(').Append(parenthesis.ToString()).Append(')');

						parenthesis.Clear();
						break;

					default:
						sb.Append(c);
						break;
				}
			}
			return text.ToString();
		}
	}
}