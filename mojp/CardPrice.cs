using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
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
        private static DateTime requestTime = DateTime.UtcNow - TimeSpan.FromMilliseconds(200);
        private static HashSet<string> pdLegalCards = null;

        private const string CacheFileName = "price_list.txt";
        private const string PDLegalFileName = "pd_legal_cards.txt";
        private const string HttpErrorMsg = "取得失敗";

        /// <summary>
        /// カード価格取得を有効にするかどうかを示す値を取得または設定します。
        /// </summary>
        public static bool EnableCardPrice => App.SettingsCache.GetCardPrice;

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

            var now = DateTime.UtcNow;
            var culture = CultureInfo.InvariantCulture;

            using (var sr = File.OpenText(CacheFileName))
            {
                while (!sr.EndOfStream)
                {
                    string name = sr.ReadLine();
                    string tix = sr.ReadLine();
                    string expire = sr.ReadLine();

                    if (DateTime.TryParseExact(expire, "o", culture, DateTimeStyles.RoundtripKind, out var expireTime))
                    {
                        // 念のため UTC 時刻に変換
                        expireTime = expireTime.ToUniversalTime();

                        if (expireTime > now)
                            prices.TryAdd(name, Tuple.Create(tix, expireTime));
                    }
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

            var now = DateTime.UtcNow;
            var culture = CultureInfo.InvariantCulture;

            using (var sw = File.CreateText(CacheFileName))
            {
                foreach (var pair in prices)
                {
                    // 取得中 or 取得失敗のデータは破棄
                    if (string.IsNullOrEmpty(pair.Value?.Item1) || pair.Value.Item1 == HttpErrorMsg)
                        continue;

                    // 有効期限切れの価格情報は破棄
                    if (pair.Value.Item2 < now)
                        continue;

                    sw.WriteLine(pair.Key);
                    sw.WriteLine(pair.Value.Item1);
                    sw.WriteLine(pair.Value.Item2.ToUniversalTime().ToString("o", culture));
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
        /// <param name="forceCheck"><see langword="true"/> の場合、最終確認日時に関わらず HTTP アクセスを行います。</param>
        public static async Task<GetPDListResult> GetOrOpenPDLegalFile(bool forceCheck)
        {
            bool exists = File.Exists(PDLegalFileName);
            var culture = CultureInfo.InvariantCulture;
            DateTime lastTime = default;
            var result = GetPDListResult.NoCheck;

            if (exists)
            {
                if (!DateTime.TryParseExact(
                    App.SettingsCache.PDListLastTimeUtc, "o", culture, DateTimeStyles.RoundtripKind, out lastTime))
                {
                    // 設定ファイルに最終確認日時を保存する前のバージョンとの互換性を維持するための代替措置
                    lastTime = File.GetLastWriteTime(PDLegalFileName).ToUniversalTime();
                }
            }

            // 初回であるか、少なくとも前回から 1 日は経過している
            if (forceCheck || !exists || DateTime.UtcNow - lastTime > TimeSpan.FromDays(1))
            {
                using (var req = new HttpRequestMessage(HttpMethod.Get, "http://pdmtgo.com/legal_cards.txt"))
                {
                    // 最終更新日をチェックして通信量を減らす
                    if (!forceCheck && exists)
                        req.Headers.IfModifiedSince = lastTime;

                    try
                    {
                        using (var resp = await App.HttpClient.Value.SendAsync(req, HttpCompletionOption.ResponseHeadersRead))
                        {
                            Debug.WriteLine("PD カードリストの取得結果：HttpStatusCode." + resp.StatusCode);

                            if (resp.StatusCode != HttpStatusCode.NotModified)
                            {
                                if (resp.IsSuccessStatusCode)
                                {
                                    using (var file = File.Create(PDLegalFileName))
                                        await resp.Content.CopyToAsync(file);

                                    result = exists ? GetPDListResult.Update : GetPDListResult.New;
                                }
                                else
                                    return GetPDListResult.NotFound;
                            }
                            else
                                result = GetPDListResult.NotModified;
                        }
                    }
                    catch
                    {
                        Debug.WriteLine("PD カードリストのダウンロード中にエラーが発生しました。");
                        return GetPDListResult.Error;
                    }
                }
            }

            var pdLegalCards = new HashSet<string>();
            var separator = new[] { " // " };

            foreach (string line in File.ReadLines(PDLegalFileName))
            {
                // 分割カードは名前を分ける
                foreach (string name in line.Split(separator, StringSplitOptions.RemoveEmptyEntries))
                {
                    string cardName = Card.NormalizeName(name);

                    if (!App.Cards.ContainsKey(cardName))
                    {
                        Debug.WriteLine(cardName);
                        return GetPDListResult.Conflict;
                    }
                    pdLegalCards.Add(cardName);
                }
            }

            // 枚数をチェック (少なくとも基本土地5枚は入る)
            if (pdLegalCards.Count < 5)
                return GetPDListResult.Conflict;

            // ローテ直後はサーバー上のファイルが頻繁に更新される場合があるので、カードリスト全体の確認が取れてから最終確認日時を記録する
            App.SettingsCache.PDListLastTimeUtc = DateTime.UtcNow.ToString("o", culture);
            CardPrice.pdLegalCards = pdLegalCards;
            return result;
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
                if (value == null || value.Item2 > DateTime.UtcNow)
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
            string tix = await GetCardPrice(query);
            
            if (tix == null)
            {
                // リクエスト間隔が短すぎたか、ネットワークが遅い場合
                await Task.Delay(1000);

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

            value = Tuple.Create(tix, DateTime.UtcNow + TimeSpan.FromDays(1));
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
        private static async Task<string> GetCardPrice(string cardName)
        {
            const string NoPrice = "― tix";

            // 前回アクセスから 200 ms 以上空ける
            if (usingScryfall || DateTime.UtcNow - requestTime < TimeSpan.FromMilliseconds(200) || usingScryfall)
            {
                Debug.WriteLine("× " + cardName + " (" + DateTime.Now.TimeOfDay + ")");
                return null;
            }
            usingScryfall = true;

            string uri = "https://api.scryfall.com/cards/search?order=tix&q=" + Uri.EscapeUriString(cardName);
            Debug.WriteLine(uri + " (" + DateTime.Now.TimeOfDay + ")");

            string json = null;
            try
            {
                using (var response = await App.HttpClient.Value.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (response.IsSuccessStatusCode)
                        json = await response.Content.ReadAsStringAsync();
                }
            }
            catch { Debug.WriteLine("Scryfall へのアクセスに失敗しました。"); }

            requestTime = DateTime.UtcNow;
            usingScryfall = false;

            if (json == null)
                return HttpErrorMsg;

            // ヒット件数の確認
            const string TotalCardsTag = "\"total_cards\":";
            int startIndex = json.IndexOf(TotalCardsTag);

            if (startIndex == -1)
                return NoPrice;

            if (json.Substring(startIndex + TotalCardsTag.Length, 2) != "1,")
            {
                // exact サーチじゃないので、複数ヒットする可能性がある
                const string CardTag = "\"name\":";
                startIndex = json.IndexOf(CardTag + "\"" + cardName.Replace("+", " // ") + "\"");

                if (startIndex == -1)
                    return NoPrice;
            }

            // tix 情報を探す範囲をカード 1 種類分に絞る
            const string RelatedTag = "\"related_uris\":";
            int endIndex = json.IndexOf(RelatedTag, startIndex);

            // PD リーガル情報
            //const string PDLegalityTag = "\"penny\":";
            //const string LegalValue = "\"legal\"";
            //int regalityIndex = response.IndexOf(PDLegalityTag, startIndex, endIndex - startIndex);
            //string isPDRegal = response.IndexOf(LegalValue, regalityIndex, PDLegalityTag.Length + LegalValue.Length) != -1 ? "[PD] " : string.Empty;

            const string TixTag = "\"tix\":";
            startIndex = json.IndexOf(TixTag, startIndex, endIndex - startIndex);

            if (startIndex == -1)
                return NoPrice;

            startIndex = json.IndexOf('"', startIndex + TixTag.Length) + 1;
            endIndex = json.IndexOf('"', startIndex);
            return json.Substring(startIndex, endIndex - startIndex) + " tix";
        }
    }

    public enum GetPDListResult
    {
        /// <summary>
        /// 確認してから日にちが経っていないので、確認していません。
        /// </summary>
        NoCheck,
        /// <summary>
        /// ローカルに無かったので、新しくダウンロードしました。
        /// </summary>
        New,
        /// <summary>
        /// サーバー側のファイルが更新されていました。
        /// </summary>
        Update,
        /// <summary>
        /// 更新する必要がありません。
        /// </summary>
        NotModified,
        /// <summary>
        /// HTTP アクセスに失敗しました。
        /// </summary>
        NotFound,
        /// <summary>
        /// 不明なエラーです。
        /// </summary>
        Error,
        /// <summary>
        /// ダウンロードしたカードリストに問題があります。
        /// </summary>
        Conflict
    }
}