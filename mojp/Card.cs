using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace Mojp
{
	public class Card
	{
		public Card()
		{
		}

		public string Name
		{
			get;
			set;
		}

		public string JapaneseName
		{
			get;
			set;
		}

		public string Type
		{
			get;
			set;
		}

		public string Text
		{
			get;
			set;
		}

		public string PT
		{
			get;
			set;
		}

		public string Summary
		{
			get
			{
				var sb = new StringBuilder();

				if (Name != null || JapaneseName != null)
				{
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

		public override string ToString()
		{
			return Name;
		}

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
		/// WHISPER の検索結果テキストを XML に変換します。
		/// </summary>
		public static IEnumerable<Card> ParseWhisper(StreamReader sr)
		{
			var card = new Card();
			var texts = new List<string>();

			while (!sr.EndOfStream)
			{
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
										if (!parenthesis)
											sb.Append(c);
										break;
								}
							}
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