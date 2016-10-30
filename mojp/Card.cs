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
		public string Name { get; set; }

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
		/// 日本語カード名 / 英語カード名、のような表記でカード名を取得します。
		/// </summary>
		public string FullName => JapaneseName == null || Name == JapaneseName ? Name : JapaneseName + " / " + Name;

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
			return Equals(other) && JapaneseName == other.JapaneseName && Type == other.Type && Text == other.Text && PT == other.PT && RelatedCardName == other.RelatedCardName;
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
					texts.Add(line);
				}
				else
				{
					switch (tokens[0])
					{
						case "　英語名":
							// 新しいカードの行に入った
							if (card.Name != null)
							{
								card.Text = string.Join(Environment.NewLine, texts);
								yield return card;
							}
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
							var sb = new StringBuilder(tokens[1].Length);
							bool parenthesis = false;

							foreach (char c in tokens[1])
							{
								switch (c)
								{
									case '(':
										parenthesis = true;
										break;

									case ')':
										parenthesis = false;
										break;

									default:
										// 読みがついているなら、取り除く
										if (!parenthesis)
											sb.Append(c);
										break;
								}
							}
							sb.Replace("---", "―");
							card.Type = sb.ToString();
							break;

						case "　コスト":
						case "　色指標":
						case "イラスト":
						case "　セット":
						case "　稀少度":
							break;

						default:
							texts.Add(line);
							break;
					}
				}
			}

			if (card.Name != null)
			{
				card.Text = string.Join(Environment.NewLine, texts);
				yield return card;
			}
		}
	}
}