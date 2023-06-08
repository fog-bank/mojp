using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Mojp
{
    partial class Card
    {
        /// <summary>
        /// アクセント記号付きのカード名などを修正します。
        /// </summary>
        public static string NormalizeName(string name)
        {
            if (name == null)
                return null;

            var sb = new StringBuilder(name.Length);
            bool replaced = false;

            foreach (char c in name)
            {
                switch (c)
                {
                    // Æther Vial など
                    // カラデシュ発売時のオラクル更新でほとんどの Æ は Ae に置換された。ただし WHISPER や wiki では AE のまま
                    //case 'Æ':
                    //  sb.Append("AE");
                    //  replaced = true;
                    //  break;

                    // Márton Stromgald や Dandân や Déjà Vu など
                    case 'á':
                    case 'â':
                    case 'à':
                        sb.Append('a');
                        replaced = true;
                        break;

                    // Ifh-Bíff Efreet
                    case 'í':
                        sb.Append('i');
                        replaced = true;
                        break;

                    // Junún Efreet や Lim-Dûl the Necromancer など
                    case 'ú':
                    case 'û':
                    case 'ü':
                        sb.Append('u');
                        replaced = true;
                        break;

                    // Séance など
                    case 'é':
                        sb.Append('e');
                        replaced = true;
                        break;

                    // Éowyn, Lady of Rohan
                    case 'É':
                        sb.Append('E');
                        replaced = true;
                        break;

                    // Jötun Owl Keeper など (PD カードリスト用。MO では o になっている)
                    case 'ö':
                        sb.Append('o');
                        replaced = true;
                        break;

                    default:
                        sb.Append(c);
                        break;
                }
            }
            return replaced ? sb.ToString() : name;
        }

        /// <summary>
        /// アーティスト名に対応する英雄譚のカード名を取得します。
        /// </summary>
        //public static bool GetSagaByArtist(string artist, out string cardName)
        //{
        //    switch (artist)
        //    {
        //        case "Jason Felix":
        //            cardName = "Fall of the Thran";
        //            return true;

        //        case "Noah Bradley":
        //            cardName = "History of Benalia";
        //            return true;

        //        case "Daniel Ljunggren":
        //            cardName = "Triumph of Gerrard";
        //            return true;

        //        case "Mark Tedin":
        //            cardName = "The Antiquities War";
        //            return true;

        //        case "James Arnold":
        //            cardName = "The Mirari Conjecture";
        //            return true;

        //        case "Franz Vohwinkel":
        //            cardName = "Time of Ice";
        //            return true;

        //        case "Vincent Proce":
        //            cardName = "Chainer's Torment";
        //            return true;

        //        case "Jenn Ravenna":
        //            cardName = "The Eldest Reborn";
        //            return true;

        //        case "Joseph Meehan":
        //            cardName = "Phyrexian Scriptures";
        //            return true;

        //        case "Seb McKinnon":
        //            cardName = "Rite of Belzenlok";
        //            return true;

        //        case "Steven Belledin":
        //            cardName = "The First Eruption";
        //            return true;

        //        case "Lake Hurwitz":
        //            cardName = "The Flame of Keld";
        //            return true;

        //        case "Adam Paquette":
        //            cardName = "The Mending of Dominaria";
        //            return true;

        //        case "Min Yum":
        //            cardName = "Song of Freyalise";
        //            return true;
        //    }
        //    cardName = null;
        //    return false;
        //}

        /// <summary>
        /// 指定した文字列が Ultimate Box Toppers のカード番号であるかどうかを調べ、対応するカード名を返します。
        /// </summary>
        //public static bool IsUltimateBoxToppers(string value, out string cardname)
        //{
        //    cardname = null;

        //    if (string.IsNullOrEmpty(value) || value.Length < 3)
        //        return false;

        //    char num1 = value[1], num2 = value[2];

        //    if (value[0] == 'U' && num1 >= '0' && num1 <= '4' && value.EndsWith("/  040 "))
        //    {
        //        switch (num1)
        //        {
        //            case '0':
        //                switch (num2)
        //                {
        //                    case '1':
        //                        cardname = "Emrakul, the Aeons Torn";
        //                        return true;

        //                    case '2':
        //                        cardname = "Karn Liberated";
        //                        return true;

        //                    case '3':
        //                        cardname = "Kozilek, Butcher of Truth";
        //                        return true;

        //                    case '4':
        //                        cardname = "Ulamog, the Infinite Gyre";
        //                        return true;

        //                    case '5':
        //                        cardname = "Snapcaster Mage";
        //                        return true;

        //                    case '6':
        //                        cardname = "Temporal Manipulation";
        //                        return true;

        //                    case '7':
        //                        cardname = "Bitterblossom";
        //                        return true;

        //                    case '8':
        //                        cardname = "Demonic Tutor";
        //                        return true;

        //                    case '9':
        //                        cardname = "Goryo's Vengeance";
        //                        return true;
        //                }
        //                break;

        //            case '1':
        //                switch (num2)
        //                {
        //                    case '0':
        //                        cardname = "Liliana of the Veil";
        //                        return true;

        //                    case '1':
        //                        cardname = "Mikaeus, the Unhallowed";
        //                        return true;

        //                    case '2':
        //                        cardname = "Reanimate";
        //                        return true;

        //                    case '3':
        //                        cardname = "Tasigur, the Golden Fang";
        //                        return true;

        //                    case '4':
        //                        cardname = "Balefire Dragon";
        //                        return true;

        //                    case '5':
        //                        cardname = "Through the Breach";
        //                        return true;

        //                    case '6':
        //                        cardname = "Eternal Witness";
        //                        return true;

        //                    case '7':
        //                        cardname = "Life from the Loam";
        //                        return true;

        //                    case '8':
        //                        cardname = "Noble Hierarch";
        //                        return true;

        //                    case '9':
        //                        cardname = "Tarmogoyf";
        //                        return true;
        //                }
        //                break;

        //            case '2':
        //                switch (num2)
        //                {
        //                    case '0':
        //                        cardname = "Vengevine";
        //                        return true;

        //                    case '1':
        //                        cardname = "Gaddock Teeg";
        //                        return true;

        //                    case '2':
        //                        cardname = "Leovold, Emissary of Trest";
        //                        return true;

        //                    case '3':
        //                        cardname = "Lord of Extinction";
        //                        return true;

        //                    case '4':
        //                        cardname = "Maelstrom Pulse";
        //                        return true;

        //                    case '5':
        //                        cardname = "Sigarda, Host of Herons";
        //                        return true;

        //                    case '6':
        //                        cardname = "Fulminator Mage";
        //                        return true;

        //                    case '7':
        //                        cardname = "Kitchen Finks";
        //                        return true;

        //                    case '8':
        //                        cardname = "Engineered Explosives";
        //                        return true;

        //                    case '9':
        //                        cardname = "Mana Vault";
        //                        return true;
        //                }
        //                break;

        //            case '3':
        //                switch (num2)
        //                {
        //                    case '0':
        //                        cardname = "Platinum Emperion";
        //                        return true;

        //                    case '1':
        //                        cardname = "Ancient Tomb";
        //                        return true;

        //                    case '2':
        //                        cardname = "Cavern of Souls";
        //                        return true;

        //                    case '3':
        //                        cardname = "Celestial Colonnade";
        //                        return true;

        //                    case '4':
        //                        cardname = "Creeping Tar Pit";
        //                        return true;

        //                    case '5':
        //                        cardname = "Dark Depths";
        //                        return true;

        //                    case '6':
        //                        cardname = "Karakas";
        //                        return true;

        //                    case '7':
        //                        cardname = "Lavaclaw Reaches";
        //                        return true;

        //                    case '8':
        //                        cardname = "Raging Ravine";
        //                        return true;

        //                    case '9':
        //                        cardname = "Stirring Wildwood";
        //                        return true;
        //                }
        //                break;

        //            case '4':
        //                cardname = "Urborg, Tomb of Yawgmoth";
        //                return true;
        //        }
        //    }
        //    return false;
        //}

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
            bool lvCard = false;
            Card prevCard = null;

            while (!sr.EndOfStream)
            {
                string line = sr.ReadLine();

                // カードの区切りを認識
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Lv 系カードかどうかチェック
                if (line.StartsWith("Ｌｖアップ"))
                    lvCard = true;

                // 各行をコロンで区切り、各項目を探す
                var tokens = line.Split('：');

                if (tokens.Length == 1)
                {
                    //texts.Add(RemoveParenthesis(line));
                    texts.Add(line.TrimEnd());
                }
                else
                {
                    switch (tokens[0])
                    {
                        case "　英語名":
                            // 新しいカードの行に入った
                            if (card?.Name != null)
                                yield return ProcessCard(card, texts);

                            card = new Card
                            {
                                Name = tokens[1].Trim()
                            };
                            texts.Clear();
                            lvCard = false;

                            if (prevCard != null)
                            {
                                // 直前に空白行が無く、関連カードである
                                card.RelatedCardName = prevCard.Name;
                                prevCard.RelatedCardName = card.Name;
                            }
                            prevCard = card;
                            break;

                        case "日本語名":
                            if (card != null)
                                card.JapaneseName = tokens[1];
                            break;

                        case "　Ｐ／Ｔ":
                        case "　忠誠度":
                            // 両面 PW カードの裏の忠誠度が空白の場合があるので、そのときは設定しない
                            if (card != null && !string.IsNullOrWhiteSpace(tokens[1]))
                            {
                                // Lv アップクリーチャーは P/T 行が複数あるので、各 P/T は通常テキストに加える
                                if (card.PT == null && !lvCard)
                                    card.PT = tokens[1];
                                else
                                    texts.Add(tokens[1]);
                            }
                            break;

                        case "　タイプ":
                            if (card != null)
                                card.Type = tokens[1];
                            break;

                        case "　コスト":
                        case "　色指標":
                        case "イラスト":
                            break;

                        case "　セット":
                            if (tokens[1] is "Unglued" or "Unhinged" or "Unstable" or "Unsanctioned" or "Unfinity")
                                card = null;
                            break;

                        case "　稀少度":
                            // 稀少度が各カードの最後の情報
                            prevCard = null;
                            break;

                        default:
                            texts.Add(line.TrimEnd());
                            break;
                    }
                }
            }

            if (card?.Name != null)
                yield return ProcessCard(card, texts);
        }

        private static Card ProcessCard(Card card, List<string> texts)
        {
            card.Text = string.Join("\n", texts);

            string wikilink = card.HasJapaneseName ? card.JapaneseName + "/" + card.Name : card.Name;

            // AE 合字処理
            //string processed = card.Name.Replace("AE", "Ae");
            //if (card.Name != processed)
            //{
            //    card.Name = processed;
            //    //card.WikiLink = wikilink;     // Wiki も Ae に統一された
            //}

            // 次元 (次元カードのページ URL には接尾辞で " (次元カード)" がつく)
            if (card.Type.StartsWith("次元"))
            {
                if (card.WikiLink == null)
                    card.WikiLink = wikilink + " (次元カード)";
                else
                    card.WikiLink += " (次元カード)";
            }
            return card;
        }

#if !OFFLINE
        /// <summary>
        /// カードテキストデータを指定した XML ファイルで修正します。
        /// </summary>
        public static void FixCardInfo(string file)
        {
            var cards = App.Cards;
            var doc = XDocument.Load(file);

            // 正規表現の構築
            var regexes = new List<Tuple<string[], Regex, string, bool>>();

            foreach (var node in doc.Root.Element("replace").Elements("regex"))
            {
                string targets = (string)node.Attribute("target");

                if (targets.Contains("all"))
                    targets = "name|ja|type|pt|rel|wiki|text";

                string pattern = (string)node.Attribute("pattern");
                try
                {
                    var regex = new Regex(pattern);
                    string value = (string)node.Attribute("value");
                    bool? debug = (bool?)node.Attribute("debug");

                    regexes.Add(Tuple.Create(targets.Split('|'), regex, value, debug.GetValueOrDefault()));
                }
                catch { Debug.WriteLine("正規表現の構築に失敗しました。パターン：" + pattern); }
            }

            // カードの追加
            foreach (var node in doc.Root.Element("add").Elements("card"))
            {
                var card = FromXml(node);
                Debug.WriteLineIf(card.RelatedCardName != null && !card.RelatedCardNames.All(cards.ContainsKey),
                    card.Name + " の関連カードが見つかりません。");

                if (cards.TryGetValue(card.Name, out var origCard))
                {
                    var mofidiedCard = origCard.Clone();

                    foreach (var tup in regexes)
                        ReplaceByRegex(mofidiedCard, tup.Item1, tup.Item2, tup.Item3);

                    if (card.EqualsStrict(mofidiedCard))
                        Debug.WriteLine(card.Name + " には既に同名カードが存在します。");
                    else
                        Debug.WriteLine(card.Name + " には既に同名カードが存在しますが、カード情報が一致しません。");
                }
                else
                {
                    cards.Add(card.Name, card);

                    var cloneCard = card.Clone();
                    foreach (var tup in regexes)
                    {
                        Debug.WriteLineIf(ReplaceByRegex(cloneCard, tup.Item1, tup.Item2, tup.Item3),
                            card.Name + " には正規表現による検索（" + tup.Item2 + "）に一致する箇所があります。");
                    }
                }
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

            // 関連カードだけ追加
            foreach (var node in doc.Root.Element("add").Elements("rel"))
            {
                string name = (string)node.Attribute("name");

                if (cards.TryGetValue(name, out var card))
                {
                    Debug.WriteLineIf(card.RelatedCardName != null, card.Name + " には既に関連カードの情報があります。");
                    card.RelatedCardName = (string)node.Attribute("rel");

                    Debug.WriteLineIf(card.RelatedCardName != null && !card.RelatedCardNames.All(cards.ContainsKey),
                        card.Name + " に追加される関連カードが見つかりません。");
                }
                else
                    Debug.WriteLine("関連カード情報の追加先となる " + name + " のカード情報がありません。");
            }

            // Wiki へのリンクだけ追加
            foreach (var node in doc.Root.Element("add").Elements("wiki"))
            {
                string name = (string)node.Attribute("name");

                if (cards.TryGetValue(name, out var card))
                {
                    Debug.WriteLineIf(card.WikiLink != null, card.Name + " には既に wiki へのリンク情報があります。");
                    card.WikiLink = (string)node.Attribute("wiki");
                }
                else
                    Debug.WriteLine("リンク情報の追加先となる " + name + " のカード情報がありません。");
            }

            // 代替テキスト検索を追加
            App.AltCardKeys.Clear();
            App.AltCardSubKeys.Clear();
            App.AltCards.Clear();

            foreach (var node in doc.Root.Element("add").Elements("alt"))
            {
                string key = (string)node.Attribute("key");
                string sub = (string)node.Attribute("sub");
                string name = (string)node.Attribute("name");

                if (!cards.ContainsKey(name) && !cards.ContainsKey(name.Replace("Ae", "AE")))
                    Debug.WriteLine("代替テキスト (" + key + " " + sub + ") の参照先となる " + name + " のカード情報がありません。");

                App.AltCardKeys.Add(key);
                App.AltCardSubKeys.Add(sub);
                App.AltCards.Add(key + sub, new AltCard(key, sub, name));
            }

            // カードの削除
            foreach (var node in doc.Root.Elements("remove").Elements("card"))
            {
                string name = (string)node.Attribute("name");
                Debug.WriteLineIf(!cards.ContainsKey(name), name + " は既にカードリストに含まれていません。");
                cards.Remove(name);
            }

            // 暫定日本語カード名の削除
            foreach (var node in doc.Root.Elements("remove").Elements("ja"))
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

            // カードデータの差し替え
            var beforeNodes = new XElement("cards");
            var replacedNodes = new XElement("replace");
            var identicalNodes = new XElement("identical");
            var appliedCount = new int[regexes.Count];

            // 英語カード名を変えることがあるので、静的リストにしてから列挙
            foreach (var card in cards.Values.ToList())
            {
                var xml = card.ToXml();
                bool replaced = false;
                bool debug = false;

                for (int i = 0; i < regexes.Count; i++)
                {
                    string prevCardName = card.Name;
                    bool applied = ReplaceByRegex(card, regexes[i].Item1, regexes[i].Item2, regexes[i].Item3);

                    // 英語カード名が変わった場合は、キーが変わったことになるので再追加
                    if (card.Name != prevCardName)
                    {
                        cards.Remove(prevCardName);
                        cards.Add(card.Name, card);
                    }

                    if (applied)
                    {
                        replaced = true;
                        debug |= regexes[i].Item4;
                        appliedCount[i]++;
                    }
                }

                if (replaced && debug)
                {
                    beforeNodes.Add(xml);
                    replacedNodes.Add(card.ToXml());
                }
            }

            for (int i = 0; i < regexes.Count; i++)
                Debug.WriteLine("Regex applied: " + appliedCount[i] + " cards, pattern: " + regexes[i].Item2);

            // 個々のカードの書き換え
            var cardNamesToReplace = new HashSet<string>();

            foreach (var node in doc.Root.Element("replace").Elements("card"))
            {
                // 変更理由を記したコメントを挿入
                if (node.PreviousNode.NodeType == XmlNodeType.Comment)
                {
                    beforeNodes.Add(node.PreviousNode);
                    replacedNodes.Add(node.PreviousNode);
                }

                var newCard = FromXml(node);
                Debug.WriteLineIf(newCard.RelatedCardName != null && !newCard.RelatedCardNames.All(cards.ContainsKey),
                    newCard.Name + " の関連カードが見つかりません。");
                Debug.WriteLineIf(!cardNamesToReplace.Add(newCard.Name), newCard.Name + " のテキスト置換を複数回行おうとしています。");

                if (cards.TryGetValue(newCard.Name, out var oldCard))
                {
                    if (newCard.EqualsStrict(oldCard))
                    {
                        // WHISPER が対応した場合に appendix.xml から外したい
                        identicalNodes.Add(new XElement("card", new XAttribute("name", newCard.Name)));
                        Debug.WriteLine(newCard.Name + " は置換する必要がありません。");
                    }
                    else
                    {
                        // 置換後と置換前で diff できるようにしたい
                        beforeNodes.Add(oldCard.ToXml());
                        replacedNodes.Add(node);
                        cards[newCard.Name] = newCard;

                        var cloneCard = newCard.Clone();
                        foreach (var tup in regexes)
                        {
                            Debug.WriteLineIf(ReplaceByRegex(cloneCard, tup.Item1, tup.Item2, tup.Item3), 
                                newCard.Name + " には正規表現による検索（" + tup.Item2 + "）に一致する箇所があります。");
                        }
                    }
                }
                else
                    Debug.WriteLine("置換先となる " + newCard.Name + " のカード情報がありません。");
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
                        identicalNodes.Add(new XElement("type", new XAttribute("name", card.Name)));
                        Debug.WriteLine(card.Name + " のカードタイプは置換する必要がありません。");
                    }
                    else
                    {
                        beforeNodes.Add(new XElement("type", new XAttribute("name", name), new XAttribute("type", card.Type)));
                        replacedNodes.Add(node);
                        card.Type = type;
                    }
                }
                else
                    Debug.WriteLine("カードタイプの置換先となる " + name + " のカード情報がありません。");
            }

            // 関連カードだけ書き換え
            foreach (var node in doc.Root.Element("replace").Elements("rel"))
            {
                string name = (string)node.Attribute("name");

                if (cards.TryGetValue(name, out var card))
                {
                    card.RelatedCardName = (string)node.Attribute("rel");
                    Debug.WriteLineIf(card.RelatedCardName != null && !card.RelatedCardNames.All(cards.ContainsKey),
                        card.Name + " に追加される関連カードが見つかりません。");
                }
                else
                    Debug.WriteLine("関連カード情報の追加先となる " + name + " のカード情報がありません。");
            }

            // 日本語カード名の重複チェック
            var jaNames = new Dictionary<string, Card>();
            foreach (var card in cards.Values)
            {
                if (jaNames.TryGetValue(card.JapaneseName, out var otherCard))
                {
                    Debug.WriteLineIf(!card.Name.EndsWith("(Alt.)"),
                        "日本語カード名の重複：≪" + card.FullName + "≫ ≪" + otherCard.FullName + "≫");
                }
                else
                    jaNames.Add(card.JapaneseName, card);
            }

            var beforeRoot = new XElement("mojp", beforeNodes);
            var beforeResult = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), beforeRoot);
            beforeResult.Save(App.GetPath("appendix_before.xml"));

            var root = new XElement("mojp", replacedNodes, identicalNodes);
            var result = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
            result.Save(App.GetPath("appendix_result.xml"));
        }
#endif

#if !OFFLINE
        /// <summary>
        /// <see cref="Card"/> オブジェクト内の指定した対象に対して、<see cref="Regex"/> で一致したテキストを置換します。
        /// </summary>
        /// <returns>実際に 1 か所以上で置換されたかどうかを示す値。</returns>
        private static bool ReplaceByRegex(Card card, string[] targets, Regex regex, string value)
        {
            bool applied = false;

            foreach (string target in targets)
            {
                switch (target)
                {
                    case "name":
                        string name = regex.Replace(card.Name, value);

                        if (card.Name != name)
                        {
                            card.Name = name;
                            applied = true;
                        }
                        break;

                    case "ja":
                        string jaName = regex.Replace(card.JapaneseName, value);

                        if (card.JapaneseName != jaName)
                        {
                            card.JapaneseName = jaName;
                            applied = true;
                        }
                        break;

                    case "type":
                        string type = regex.Replace(card.Type, value);

                        if (card.Type != type)
                        {
                            card.Type = type;
                            applied = true;
                        }
                        break;

                    case "pt":
                        string pt = regex.Replace(card.PT, value);

                        if (card.PT != pt)
                        {
                            card.PT = pt;
                            applied = true;
                        }
                        break;

                    case "rel":
                        string related = regex.Replace(card.RelatedCardName, value);

                        if (card.RelatedCardName != related)
                        {
                            card.RelatedCardName = related;
                            applied = true;
                        }
                        break;

                    case "wiki":
                        string wikilink = regex.Replace(card.WikiLink, value);

                        if (card.WikiLink != wikilink)
                        {
                            card.WikiLink = wikilink;
                            applied = true;
                        }
                        break;

                    case "text":
                        string text = regex.Replace(card.Text, value);

                        if (card.Text != text)
                        {
                            card.Text = text;
                            card.lines = null;
                            applied = true;
                        }
                        break;
                }
            }
            return applied;
        }
#endif
    }
}