﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Mojp
{
    partial class Card
    {
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

                // Lv 系カードかどうかチェック
                if (line.StartsWith("Ｌｖアップ"))
                    lvCard = true;

                // 各行をコロンで区切り、各項目を探す
                var tokens = line.Split('：');

                if (tokens.Length == 1)
                {
                    //texts.Add(RemoveParenthesis(line));
                    texts.Add(line);
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
                            //string jaName = tokens[1];

                            // 読みがついているなら、取り除く
                            //int hiragana = jaName.IndexOf('（');
                            //if (hiragana >= 0)
                            //    jaName = jaName.Substring(0, hiragana);

                            //card.JapaneseName = jaName.Trim();
                            card.JapaneseName = tokens[1];
                            break;

                        case "　Ｐ／Ｔ":
                        case "　忠誠度":
                            // 両面 PW カードの裏の忠誠度が空白の場合があるので、そのときは設定しない
                            if (!string.IsNullOrWhiteSpace(tokens[1]))
                            {
                                // Lv アップクリーチャーは P/T 行が複数あるので、各 P/T は通常テキストに加える
                                if (card.PT == null && !lvCard)
                                    card.PT = tokens[1];
                                else
                                    texts.Add(tokens[1]);
                            }
                            break;

                        case "　タイプ":
                            //card.Type = RemoveParenthesis(tokens[1]).Replace("---", "―");
                            card.Type = tokens[1];
                            break;

                        case "　コスト":
                        case "　色指標":
                        case "イラスト":
                        case "　セット":
                        case "　稀少度":
                            break;

                        default:
                            //texts.Add(RemoveParenthesis(line));
                            texts.Add(line);
                            break;
                    }
                }
            }

            if (card.Name != null)
                yield return ProcessCard(card, texts);
        }

        private static Card ProcessCard(Card card, IEnumerable<string> texts)
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

        /// <summary>
        /// カードテキストデータを指定した XML ファイルで修正します。
        /// </summary>
        public static void FixCardInfo(string file)
        {
            var cards = App.Cards;
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
            var beforeNodes = new XElement("cards");
            var replacedNodes = new XElement("replace");
            var identicalNodes = new XElement("identical");

            // 正規表現による置換
            var regexes = new List<Tuple<string[], Regex, string, bool>>();

            foreach (var node in doc.Root.Element("replace").Elements("regex"))
            {
                string targets = (string)node.Attribute("target");

                if (targets == "all")
                    targets = "name|jaName|type|pt|related|wikilink|text";

                string pattern = (string)node.Attribute("pattern");
                try
                {
                    var regex = new Regex(pattern);
                    string value = (string)node.Attribute("value");
                    bool? nodebug = (bool?)node.Attribute("nodebug");

                    regexes.Add(Tuple.Create(targets.Split('|'), regex, value, nodebug.GetValueOrDefault()));
                }
                catch { Debug.WriteLine("正規表現の構築に失敗しました。パターン：" + pattern); }
            }

            foreach (var card in cards.Values.ToList())
            {
                var xml = card.ToXml();
                bool replaced = false;
                bool nodebug = true;

                foreach (var tuple in regexes)
                {
                    var targets = tuple.Item1;
                    var regex = tuple.Item2;
                    string value = tuple.Item3;

                    foreach (string target in targets)
                    {
                        switch (target)
                        {
                            case "name":
                                string name = regex.Replace(card.Name, value);

                                if (card.Name != name)
                                {
                                    cards.Remove(card.Name);
                                    card.Name = name;
                                    cards.Add(name, card);
                                    replaced = true;
                                    nodebug &= tuple.Item4;
                                }
                                break;

                            case "jaName":
                                string jaName = regex.Replace(card.JapaneseName, value);

                                if (card.JapaneseName != jaName)
                                {
                                    card.JapaneseName = jaName;
                                    replaced = true;
                                    nodebug &= tuple.Item4;
                                }
                                break;

                            case "type":
                                string type = regex.Replace(card.Type, value);

                                if (card.Type != type)
                                {
                                    card.Type = type;
                                    replaced = true;
                                    nodebug &= tuple.Item4;
                                }
                                break;

                            case "pt":
                                string pt = regex.Replace(card.PT, value);

                                if (card.PT != pt)
                                {
                                    card.PT = pt;
                                    replaced = true;
                                    nodebug &= tuple.Item4;
                                }
                                break;

                            case "related":
                                string related = regex.Replace(card.RelatedCardName, value);

                                if (card.RelatedCardName != related)
                                {
                                    card.RelatedCardName = related;
                                    replaced = true;
                                    nodebug &= tuple.Item4;
                                }
                                break;

                            case "wikilink":
                                string wikilink = regex.Replace(card.WikiLink, value);

                                if (card.WikiLink != wikilink)
                                {
                                    card.WikiLink = wikilink;
                                    replaced = true;
                                    nodebug &= tuple.Item4;
                                }
                                break;

                            case "text":
                                string text = regex.Replace(card.Text, value);

                                if (card.Text != text)
                                {
                                    card.Text = text;
                                    card.lines = null;
                                    replaced = true;
                                    nodebug &= tuple.Item4;
                                }
                                break;
                        }
                    }
                }

                if (replaced && !nodebug)
                {
                    beforeNodes.Add(xml);
                    replacedNodes.Add(card.ToXml());
                }
            }

            foreach (var node in doc.Root.Element("replace").Elements("card"))
            {
                var card = FromXml(node);
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
                        beforeNodes.Add(cards[card.Name].ToXml());
                        replacedNodes.Add(node);
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
                        identicalNodes.Add(new XElement("type", new XAttribute("name", card.Name)));
                        Debug.WriteLine(card.Name + " のカードタイプは置換する必要がありません。");
                    }
                    else
                    {
                        beforeNodes.Add(new XElement("type", new XAttribute("name", name), new XAttribute("type", cards[card.Name].Type)));
                        replacedNodes.Add(node);
                        cards[card.Name].Type = type;
                    }
                }
                else
                    Debug.WriteLine("カードタイプの置換先となる " + name + " のカード情報がありません。");
            }

            var beforeRoot = new XElement("mojp", beforeNodes);
            var beforeResult = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), beforeRoot);
            beforeResult.Save("appendix_before.xml");

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

        ///// <summary>
        ///// サブタイプの英語名を削除した文字列にします。
        ///// </summary>
        //private static string RemoveParenthesis(string line)
        //{
        //    var text = new StringBuilder(line.Length);
        //    var parenthesis = new StringBuilder();
        //    var sb = text;

        //    for (int i = 0; i < line.Length; i++)
        //    {
        //        char c = line[i];
        //        switch (c)
        //        {
        //            case '(':
        //                sb = parenthesis;
        //                break;

        //            case ')':
        //                bool english = parenthesis.Length >= 2;

        //                for (int j = 0; j < parenthesis.Length; j++)
        //                {
        //                    char parChr = parenthesis[j];

        //                    if (parChr >= 'a' && parChr <= 'z')
        //                        continue;

        //                    if (parChr >= 'A' && parChr <= 'Z')
        //                        continue;

        //                    // 「Bolas's Meditation Realm」「Urza's」「Power-Plant」
        //                    if (parChr == ' ' || parChr == '\'' || parChr == '-')
        //                        continue;

        //                    english = false;
        //                    break;
        //                }
        //                sb = text;

        //                if (english)
        //                {
        //                    // サブタイプとタイプの間にあるべき中点が抜けている場合を修正
        //                    if (i + 1 < line.Length && line[i + 1] != '・')
        //                    {
        //                        string follow = line.Substring(i + 1);

        //                        if (follow.StartsWith("クリーチャー") || follow.StartsWith("アーティファクト") || follow.StartsWith("土地") ||
        //                            follow.StartsWith("呪文") || follow.StartsWith("パーマネント") || follow.StartsWith("カード"))
        //                            sb.Append('・');
        //                    }
        //                }
        //                else
        //                    sb.Append('(').Append(parenthesis.ToString()).Append(')');

        //                parenthesis.Clear();
        //                break;

        //            default:
        //                sb.Append(c);
        //                break;
        //        }
        //    }
        //    return text.ToString();
        //}
    }
}