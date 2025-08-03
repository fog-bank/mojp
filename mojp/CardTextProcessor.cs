using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Mojp;

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
                // 検索用：á|â|à|ä|í|ú|û|ü|é|É|ó|ö|ñ

                // Márton Stromgald や Dandân や Déjà Vu や Song of Eärendil など
                case 'á':
                case 'â':
                case 'à':
                case 'ä':
                    sb.Append('a');
                    replaced = true;
                    break;

                // Ifh-Bíff Efreet
                case 'í':
                case 'ï':
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

                // Éowyn, Lady of Rohan など
                case 'É':
                    sb.Append('E');
                    replaced = true;
                    break;

                // Jötun Owl Keeper など (PD カードリスト用。MO では o になっている)
                case 'ó':
                case 'ö':
                    sb.Append('o');
                    replaced = true;
                    break;

                // Robo-Piñata
                case 'ñ':
                    sb.Append('n');
                    replaced = true;
                    break;

                // Ratonhnhaké꞉ton (for PD)
                case '꞉':
                    sb.Append(':');
                    replaced = true;
                    break;

                default:
                    sb.Append(c);
                    break;
            }
        }
        return replaced ? sb.ToString() : name;
    }

#if !OFFLINE
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
        var colon = new[] { '：' };

        while (!sr.EndOfStream)
        {
            string line = sr.ReadLine();

            // カードの区切りを認識
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Lv 系カードかどうかチェック
            if (line.StartsWith("Ｌｖアップ", StringComparison.Ordinal))
                lvCard = true;

            // 各行をコロンで区切り、各項目を探す
            var tokens = line.Split(colon, 2);

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
                        if (tokens[1] is "Special" or "Astral Set" or "Dreamcast's Original" or "Mystery Booster" or "Unglued" or "Unhinged" or "Unstable" or "Unsanctioned" or "Unfinity")
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
#endif

#if !OFFLINE
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
        if (card.Type.StartsWith("次元", StringComparison.Ordinal))
        {
            if (card.WikiLink == null)
                card.WikiLink = wikilink + " (次元カード)";
            else
                card.WikiLink += " (次元カード)";
        }
        return card;
    }
#endif

#if !OFFLINE
    /// <summary>
    /// カードテキストデータを指定した XML ファイルで修正します。
    /// </summary>
    public static void FixCardInfo(string file)
    {
        var cards = App.Cards;
        var doc = XDocument.Load(file);
        var relNotFound = new List<Card>();

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

            if (card.RelatedCardName != null && !card.RelatedCardNames.All(cards.ContainsKey))
                relNotFound.Add(card);

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
            string pt = (string)node.Attribute("pt");
            bool? append = (bool?)node.Attribute("append");

            if (cards.TryGetValue(name, out var card))
            {
                if (append.GetValueOrDefault())
                {
                    Debug.WriteLineIf(card.Text.Contains(pt), card.Name + "（宇宙船）には既に P/T の情報があります。");
                    card.Text = card.Text + "\n" + pt;
                }
                else
                {
                    Debug.WriteLineIf(card.PT != null, card.Name + " には既に P/T の情報があります。");
                    card.PT = pt;
                }
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
        Debug.WriteLine("正規表現の適用開始");

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

        // 関連カードの存在チェック
        foreach (var card in relNotFound)
        {
            Debug.WriteLineIf(card.RelatedCardName != null && !card.RelatedCardNames.All(cards.ContainsKey),
                "追加された " + card.Name + " の関連カード (" + card.RelatedCardName + ") が見つかりません。");
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