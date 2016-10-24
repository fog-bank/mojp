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
		/// カードの各情報をまとめた文字列を生成します。
		/// </summary>
		public string Summary
		{
			get
			{
				var sb = new StringBuilder();

				if (Name != null || JapaneseName != null)
				{
					// 日本語名がない場合は英語名だけを使用
					if (string.IsNullOrEmpty(JapaneseName) || Name == JapaneseName)
						sb.AppendLine(Name);
					else
						sb.Append(JapaneseName).Append(" / ").AppendLine(Name);
				}

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

		public bool Equals(Card other)
		{
			return !string.IsNullOrWhiteSpace(Name) && string.Equals(Name, other?.Name);
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

			while (!sr.EndOfStream)
			{
				// 各行をコロンで区切り、各項目を探す
				string line = sr.ReadLine();
				var tokens = line.Split('：');

				if (tokens.Length == 1)
				{
					if (!string.IsNullOrWhiteSpace(line))
						texts.Add(line);
				}
				else if (tokens.Length > 1)
				{
					switch (tokens[0])
					{
						case "　英語名":
							if (card.Name != null)
							{
								card.Text = string.Join(Environment.NewLine, texts);
								yield return card;
							}
							texts.Clear();

							card = new Card();
							card.Name = tokens[1].Trim();
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
							card.PT = tokens[1].Replace("/", " / ");
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
							if (!string.IsNullOrWhiteSpace(line))
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