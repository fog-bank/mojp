using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Mojp
{
    public static class CardPrice
    {
        public static readonly DependencyProperty PriceTargetProperty
            = DependencyProperty.RegisterAttached("PriceTarget", typeof(Card), typeof(CardPrice),
                new FrameworkPropertyMetadata(Card.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure, OnPriceTargetChanged));

        public static readonly DependencyProperty LegalTargetProperty
            = DependencyProperty.RegisterAttached("LegalTarget", typeof(Card), typeof(CardPrice),
                new FrameworkPropertyMetadata(Card.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure, OnLegalTargetChanged));

        // card_name -> (tix_info, expire_time)
        private static readonly ConcurrentDictionary<string, Tuple<string, DateTime>> prices
            = new ConcurrentDictionary<string, Tuple<string, DateTime>>();

        private static volatile bool usingScryfall = false;
        private static DateTime requestTime = DateTime.Now - TimeSpan.FromMilliseconds(200);
        private static HashSet<string> pdLegalCards = null;

        private const string CacheFileName = "price_list.txt";
        private const string PDLegalFileName = "pd_legal_cards.txt";

        /// <summary>
        /// カード価格取得を有効にするかどうかを示す値を取得または設定します。
        /// </summary>
        public static bool EnableCardPrice { get; set; }

        [AttachedPropertyBrowsableForType(typeof(TextBlock))]
        public static Card GetPriceTarget(TextBlock element) => element.GetValue(PriceTargetProperty) as Card;

        [AttachedPropertyBrowsableForType(typeof(TextBlock))]
        public static void SetPriceTarget(TextBlock element, Card value) => element.SetValue(PriceTargetProperty, value);

        [AttachedPropertyBrowsableForType(typeof(UIElement))]
        public static Card GetLegalTarget(UIElement element) => element.GetValue(LegalTargetProperty) as Card;

        [AttachedPropertyBrowsableForType(typeof(UIElement))]
        public static void SetLegalTarget(UIElement element, Card value) => element.SetValue(LegalTargetProperty, value);

        /// <summary>
        /// 指定したカードの価格の取得を試みます。
        /// </summary>
        /// <returns>既にキャッシュ済みの場合はその値。そうでない場合は、プレースホルダの文字列。</returns>
        public static string GetPrice(Card card)
        {
            if (!EnableCardPrice || IsSpecialCard(card))
                return string.Empty;

            if (prices.TryGetValue(card.Name, out var value) && value?.Item1 != null)
                return value.Item1;
            else
                return "価格取得中";
        }

        /// <summary>
        /// 指定したカードが Penny Dreadful フォーマットでリーガルかどうかを調べます。
        /// </summary>
        public static bool IsPDLegal(Card card) => pdLegalCards != null && pdLegalCards.Contains(card?.Name);

        /// <summary>
        /// カード価格のキャッシュデータを読み込みます。
        /// </summary>
        public static void OpenCacheData()
        {
            if (!File.Exists(CacheFileName))
                return;

            var now = DateTime.Now;

            using (var sr = File.OpenText(CacheFileName))
            {
                while (!sr.EndOfStream)
                {
                    string name = sr.ReadLine();
                    string tix = sr.ReadLine();
                    string expire = sr.ReadLine();

                    if (DateTime.TryParse(expire, out var expireTime) && expireTime > now)
                        prices.TryAdd(name, Tuple.Create(tix, expireTime));
                }
            }
        }

        /// <summary>
        /// カード価格のキャッシュデータを保存します。
        /// </summary>
        public static void SaveCacheData()
        {
            if (prices == null)
                return;

            var now = DateTime.Now;

            using (var sw = File.CreateText(CacheFileName))
            {
                foreach (var pair in prices.Where(pair => !string.IsNullOrEmpty(pair.Value?.Item1) && pair.Value.Item2 > now))
                {
                    sw.WriteLine(pair.Key);
                    sw.WriteLine(pair.Value.Item1);
                    sw.WriteLine(pair.Value.Item2.ToString("o"));
                }
            }
        }

        /// <summary>
        /// カード価格のキャッシュデータをメモリから削除します。
        /// </summary>
        public static void ClearCacheData()
        {
            prices.Clear();
            //File.Delete(CacheFileName);
        }

        /// <summary>
        /// Penny Dreadful のカードリストを取得するか、取得済みのファイルを開き、カードリストを準備します。
        /// </summary>
        public static async Task GetOrOpenPDLegalFile()
        {
            if (!File.Exists(PDLegalFileName) || DateTime.Now - File.GetLastWriteTime(PDLegalFileName) > TimeSpan.FromDays(1))
            {
                Stream response = null;
                try
                {
                    response = await App.HttpClient.Value.GetStreamAsync("http://pdmtgo.com/legal_cards.txt");
                }
                catch { Debug.WriteLine("HTTPS アクセスに失敗しました。"); }

                using (var file = File.Create(PDLegalFileName))
                    await response.CopyToAsync(file);
            }

            var pdLegalCards = new HashSet<string>();
            var split = new[] { " // " };

            foreach (string line in File.ReadLines(PDLegalFileName))
            {
                // 分割カードは名前を分ける
                if (line.Contains(split[0]))
                    pdLegalCards.UnionWith(line.Split(split, StringSplitOptions.RemoveEmptyEntries));
                else
                    pdLegalCards.Add(line);
            }
            CardPrice.pdLegalCards = pdLegalCards;
        }

        /// <summary>
        /// Penny Dreadful のカードリストをメモリから削除します。
        /// </summary>
        public static void ClearPDLegalList()
        {
            pdLegalCards = null;
            //File.Delete(PDLegalFileName);
        }

        /// <summary>
        /// <see cref="PriceTargetProperty"/> 添付プロパティの値が変更されたときに、カード価格情報の取得を行います。
        /// </summary>
        private static async void OnPriceTargetChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var card = e.NewValue as Card;

            if (!EnableCardPrice || IsSpecialCard(card))
                return;

            if (prices.TryGetValue(card.Name, out var value))
            {
                // 既に取得要請が出ている or まだキャッシュが有効かどうか
                if (value == null || value.Item2 > DateTime.Now)
                    return;
                else
                    prices.TryUpdate(card.Name, null, value);
            }
            else
            {
                // 同時取得要請に備える
                if (!prices.TryAdd(card.Name, null))
                    return;
            }

            // 分割カード名は修正したうえで scryfall.com に問い合わせ
            string query = card.WikiLink != null && card.WikiLink.Contains("/") ? card.WikiLink.Split('/')[1] : card.Name;
            string tix = await GetCardPrice(query).ConfigureAwait(false);
            
            if (tix == null)
            {
                // リクエスト間隔が短すぎたか、ネットワークが遅い場合
                await Task.Delay(500);

                if (!card.IsObserved)
                {
                    // 既に表示されていない
                    prices.TryRemove(card.Name, out var _);
                    return;
                }

                tix = await GetCardPrice(query);

                if (tix == null)
                {
                    prices.TryRemove(card.Name, out var _);
                    return;
                }
            }
            Debug.WriteLine(card.Name + " | " + tix + " (" + DateTime.Now.TimeOfDay + ")");

            value = Tuple.Create(tix, DateTime.Now + TimeSpan.FromDays(1));
            prices.TryUpdate(card.Name, value, null);
            card.OnUpdatePrice();
        }

        /// <summary>
        /// <see cref="LegalTargetProperty"/> 添付プロパティの値が変更されたときに、対象の <see cref="UIElement"/> の表示を切り替えます。
        /// </summary>
        private static void OnLegalTargetChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is UIElement element)
                element.Visibility = IsPDLegal(e.NewValue as Card) ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// カード価格を取得しなくてよいカードかどうかを判定します。
        /// </summary>
        private static bool IsSpecialCard(Card card)
        {
            if (string.IsNullOrEmpty(card?.Name))
                return true;

            if (card.Type != null && (card.Type.Length == 0 || card.Type.StartsWith("トークン") ||
                card.Type.StartsWith("ヴァンガード") || card.Type.StartsWith("次元") || card.Type.StartsWith("現象")))
                return true;

            return false;
        }

        /// <summary>
        /// scryfall.com からカード価格を取得します。
        /// </summary>
        /// <returns>取得前のエラーは <see langword="null"/> 。取得時や取得後のエラーは <see cref="string.Empty"/> 。</returns>
        private static async Task<string> GetCardPrice(string cardName)
        {
            const string NoPrice = "― tix";

            if (usingScryfall || DateTime.Now - requestTime < TimeSpan.FromMilliseconds(200) || usingScryfall)
            {
                Debug.WriteLine("× " + cardName + " (" + DateTime.Now.TimeOfDay + ")");
                return null;
            }
            usingScryfall = true;

            string uri = "https://api.scryfall.com/cards/search?order=tix&q=" + Uri.EscapeUriString(cardName.Replace("'", null));
            Debug.WriteLine(uri + " (" + DateTime.Now.TimeOfDay + ")");

            string response = null;
            try
            {
                response = await App.HttpClient.Value.GetStringAsync(uri);
            }
            catch { Debug.WriteLine("HTTPS アクセスに失敗しました。"); }

            requestTime = DateTime.Now;
            usingScryfall = false;

            if (response == null)
                return NoPrice;

            // exact サーチじゃないので、複数ヒットする可能性がある
            const string TotalCardsTag = "\"total_cards\":";
            int startIndex = response.IndexOf(TotalCardsTag);

            if (startIndex == -1)
                return NoPrice;

            if (response.Substring(startIndex + TotalCardsTag.Length, 2) != "1,")
            {
                const string CardTag = "\"name\":";
                startIndex = response.IndexOf(CardTag + "\"" + cardName.Replace("+", " // ") + "\"");

                if (startIndex == -1)
                    return NoPrice;
            }
            const string RelatedTag = "\"related_uris\":";
            int endIndex = response.IndexOf(RelatedTag, startIndex);

            //const string PDLegalityTag = "\"penny\":";
            //const string LegalValue = "\"legal\"";
            //int regalityIndex = response.IndexOf(PDLegalityTag, startIndex, endIndex - startIndex);
            //string isPDRegal = response.IndexOf(LegalValue, regalityIndex, PDLegalityTag.Length + LegalValue.Length) != -1 ? "[PD] " : string.Empty;

            const string TixTag = "\"tix\":";
            startIndex = response.IndexOf(TixTag, startIndex, endIndex - startIndex);

            if (startIndex == -1)
                return NoPrice;

            startIndex = response.IndexOf('"', startIndex + TixTag.Length) + 1;
            endIndex = response.IndexOf('"', startIndex);
            return response.Substring(startIndex, endIndex - startIndex) + " tix";
        }
    }
}