using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Linq;

namespace Mojp
{
    /// <summary>
    /// MTG のカードを表します。
    /// </summary>
    public partial class Card : IEquatable<Card>, INotifyPropertyChanged
    {
        private string[] lines;

        public Card()
        { }

        public Card(string message)
        {
            Text = message;
            lines = new[] { message };
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
        /// カードのテキストを行で分割したものを取得します。
        /// </summary>
        public string[] TextLines
        {
            get
            {
                if (lines == null)
                    lines = Text?.Split('\n');

                return lines;
            }
        }

        /// <summary>
        /// カードの P/T を取得または設定します。
        /// </summary>
        public string PT { get; set; }

        /// <summary>
        /// 両面カードのもう一方の面など、関連するカードの名前を取得または設定します。
        /// </summary>
        public string RelatedCardName { get; set; }

        /// <summary>
        /// 関連するカードの名前のリストを取得します。
        /// </summary>
        public IEnumerable<string> RelatedCardNames => RelatedCardName?.Split('|');

        /// <summary>
        /// 日本語名/英語名 で MTG Wiki に移動できない場合の、http://mtgwiki.com/wiki/ 以下の代替リンクを取得または設定します。
        /// </summary>
        public string WikiLink { get; set; }

        /// <summary>
        /// 日本語カード名 / 英語カード名、のような表記でカード名を取得します。
        /// </summary>
        public string FullName => HasJapaneseName ? JapaneseName + " / " + Name : Name;

        /// <summary>
        /// 公式のカード名日本語訳があるかどうかを示す値を取得します。
        /// </summary>
        public bool HasJapaneseName => JapaneseName != null && Name != JapaneseName && Type != "ヴァンガード";

        /// <summary>
        /// このカードの価格情報を取得します。
        /// </summary>
        public string Price => CardPrice.GetPrice(this);

        public bool IsObserved => PropertyChanged != null;

        /// <summary>
        /// 空のオブジェクトを取得します。オブジェクト自身は読み取り専用になっていませんが、変更しないでください。
        /// </summary>
        public static Card Empty { get; } = new Card();

        /// <summary>
        /// カードの英語名が一致しているかどうかを調べます。
        /// </summary>
        public bool Equals(Card other) => !string.IsNullOrWhiteSpace(Name) && string.Equals(Name, other?.Name);

        /// <summary>
        /// <see cref="Card"/> オブジェクトの各メンバの値が一致しているかどうかを調べます。
        /// </summary>
        public bool EqualsStrict(Card other)
        {
            return Equals(other) && JapaneseName == other.JapaneseName && Type == other.Type && Text == other.Text &&
                PT == other.PT && RelatedCardName == other.RelatedCardName && WikiLink == other.WikiLink;
        }

        public override bool Equals(object obj) => Equals(obj as Card);

        public override int GetHashCode() => Name == null ? 0 : Name.GetHashCode();

        public override string ToString() => Name;

        public Card Clone()
        {
            return new Card()
            {
                Name = Name,
                JapaneseName = JapaneseName,
                Type = Type,
                Text = Text,
                PT = PT,
                RelatedCardName = RelatedCardName,
                WikiLink = WikiLink
            };
        }

        /// <summary>
        /// 後で復元できるように XML ノードに変換します。
        /// </summary>
        /// <returns></returns>
        public XElement ToXml()
        {
            var xml = new XElement("card");

            xml.Add(new XAttribute("name", Name));
            xml.Add(new XAttribute("jaName", JapaneseName));
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
        /// 価格情報を取得し終わったときに呼び出します。
        /// </summary>
        public void OnUpdatePrice() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Price)));

        /// <summary>
        /// XML ノードからカード情報を復元します。
        /// </summary>
        public static Card FromXml(XElement cardElement)
        {
            return new Card
            {
                Name = (string)cardElement.Attribute("name"),
                JapaneseName = (string)cardElement.Attribute("jaName"),
                Type = (string)cardElement.Attribute("type"),
                PT = (string)cardElement.Attribute("pt"),
                RelatedCardName = (string)cardElement.Attribute("related"),
                WikiLink = (string)cardElement.Attribute("wikilink"),
                Text = cardElement.Value
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}