
using System.Windows;
using System.Windows.Controls;
#if !OFFLINE
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Net.Http;
#endif

namespace Mojp;

public static class CardPrice
{
#if OFFLINE
    public static readonly DependencyProperty PriceTargetProperty
        = DependencyProperty.RegisterAttached("PriceTarget", typeof(Card), typeof(CardPrice));

    public static readonly DependencyProperty LegalTargetProperty
        = DependencyProperty.RegisterAttached("LegalTarget", typeof(Card), typeof(CardPrice));
#else
    public static readonly DependencyProperty PriceTargetProperty
        = DependencyProperty.RegisterAttached("PriceTarget", typeof(Card), typeof(CardPrice),
            new FrameworkPropertyMetadata(Card.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure, OnPriceTargetChanged));

    public static readonly DependencyProperty LegalTargetProperty
        = DependencyProperty.RegisterAttached("LegalTarget", typeof(Card), typeof(CardPrice),
            new FrameworkPropertyMetadata(Card.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure, OnLegalTargetChanged));
#endif

#if !OFFLINE
    // card_name -> (tix_info, expire_time)
    private static readonly ConcurrentDictionary<string, Tuple<string, DateTime>> prices = new();

    private static volatile bool usingScryfall = false;
    private static DateTime requestTime = DateTime.UtcNow - TimeSpan.FromMilliseconds(200);
    private static HashSet<string> pdLegalCards = null;

    private const string CacheFileName = "price_list.txt";
    private const string PDLegalFileName = "pd_legal_cards.txt";
    private const string HttpErrorMsg = "取得失敗";
#endif

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

#if !OFFLINE
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
        string path = App.GetPath(CacheFileName);

        if (!File.Exists(path))
            return;

        var now = DateTime.UtcNow;
        var culture = CultureInfo.InvariantCulture;

        using var sr = File.OpenText(path);

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

    /// <summary>
    /// カード価格のキャッシュデータを保存します。
    /// </summary>
    public static void SaveCacheData()
    {
        if (prices == null)
            return;

        var now = DateTime.UtcNow;
        var culture = CultureInfo.InvariantCulture;

        using var sw = File.CreateText(App.GetPath(CacheFileName));

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
    public static async Task<GetPDListResult> GetOrOpenPDLegalFile(bool forceCheck = false)
    {
        string path = App.GetPath(PDLegalFileName);
        bool exists = File.Exists(path);
        var culture = CultureInfo.InvariantCulture;
        DateTime lastCheckTime = default;
        DateTime lastModifiedTime = default;
        var result = GetPDListResult.NoCheck;

        if (exists)
        {
            // 最終確認日時の取得
            if (!DateTime.TryParseExact(
                App.SettingsCache.PDListLastTimeUtc, "o", culture, DateTimeStyles.RoundtripKind, out lastCheckTime))
            {
                // 設定ファイルに最終確認日時を保存する前のバージョンとの互換性を維持するための代替措置
                lastCheckTime = File.GetLastWriteTime(path).ToUniversalTime();
            }

            // 最終更新日時の取得
            if (!DateTime.TryParseExact(
                App.SettingsCache.PDServerLastTimeUtc, "o", culture, DateTimeStyles.RoundtripKind, out lastModifiedTime))
            {
                // PD S9 更新前時刻
                lastModifiedTime = new DateTime(2018, 7, 13, 7, 0, 0, DateTimeKind.Utc);
            }
        }

        // 初回であるか、少なくとも前回から 1 日は経過している
        if (forceCheck || !exists || DateTime.UtcNow - lastCheckTime > TimeSpan.FromDays(1))
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "http://pdmtgo.com/legal_cards.txt");

            // 最終更新日をチェックして通信量を減らす
            if (!forceCheck && exists)
                req.Headers.IfModifiedSince = lastModifiedTime;

            try
            {
                using var resp = await App.HttpClient.Value.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

                Debug.WriteLine("PD カードリストの取得結果：HttpStatusCode." + resp.StatusCode);

                if (resp.StatusCode != HttpStatusCode.NotModified)
                {
                    if (resp.IsSuccessStatusCode)
                    {
                        using (var file = File.Create(path))
                            await resp.Content.CopyToAsync(file);

                        result = exists ? GetPDListResult.Update : GetPDListResult.New;
                        lastModifiedTime = resp.Content.Headers.LastModified?.UtcDateTime ?? DateTime.UtcNow;
                    }
                    else
                        return GetPDListResult.NotFound;
                }
                else
                    result = GetPDListResult.NotModified;
            }
            catch
            {
                Debug.WriteLine("PD カードリストのダウンロード中にエラーが発生しました。");
                return GetPDListResult.Error;
            }
        }

        var legalCards = new HashSet<string>();
        var separator = new[] { " // " };

        foreach (string line in File.ReadLines(path))
        {
            // 分割カードは名前を分ける
            foreach (string name in line.Split(separator, StringSplitOptions.RemoveEmptyEntries))
            {
                string cardName = Card.NormalizeName(name);

                // HACK: PD S28 (ONE) にバグ
                if (cardName.EndsWith(" - Sketch", StringComparison.Ordinal))
                    continue;

                // HACK: PD S34 (BLB) にバグ
                if (cardName.Equals("Sol'Kanar the Tainted", StringComparison.OrdinalIgnoreCase))
                    cardName = "Sol'kanar the Tainted";

                if (!App.TryGetCard(cardName, out _))
                {
                    Debug.WriteLine("PD カードリスト：" + cardName);
                    return GetPDListResult.Conflict;
                }
                legalCards.Add(cardName);
            }
        }

        // 枚数をチェック (少なくとも基本土地5枚は入る)
        if (legalCards.Count < 5)
            return GetPDListResult.Conflict;

        // ローテ直後はサーバー上のファイルが頻繁に更新される場合があるので、カードリスト全体の確認が取れてから最終確認日時を記録する
        if (result != GetPDListResult.NoCheck)
            App.SettingsCache.PDListLastTimeUtc = DateTime.UtcNow.ToString("o", culture);

        if (result == GetPDListResult.New || result == GetPDListResult.Update)
            App.SettingsCache.PDServerLastTimeUtc = lastModifiedTime.ToString("o", culture);

        pdLegalCards = legalCards;
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
    /// カード価格を取得しなくてよいカードかどうかを判定します。
    /// </summary>
    public static bool IsSpecialCard(Card card)
    {
        if (string.IsNullOrEmpty(card?.Name))
            return true;

        if (string.IsNullOrEmpty(card.Type))
            return true;

        if (card.Name is "Gleemox" or
            "Everflame, Heroes' Legacy" or "Legitimate Businessperson" or "Mileva, the Stalwart" or "Mishra's Warform" or "Vitu-Ghazi")
            return true;

        if (card.Type.StartsWith("トークン", StringComparison.Ordinal) || card.Type.StartsWith("次元", StringComparison.Ordinal))
            return true;

        if (card.Type is "ヴァンガード" or "現象" or "ダンジョン")
            return true;

        return false;
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

        // 分割カードは wikilink を利用して連結したカード名を使う。UNF のステッカー系カードは除外
        string query = card.Name;

        if (card.WikiLink != null && !card.WikiLink.Contains("＿"))
        {
            int slashIndex = card.WikiLink.IndexOf('/');

            if (slashIndex > 0)
                query = card.WikiLink.Substring(slashIndex + 1);
        }
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
            using var response = await App.HttpClient.Value.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);

            if (response.IsSuccessStatusCode)
                json = await response.Content.ReadAsStringAsync();
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

        if (TrySubstring(json, startIndex + TotalCardsTag.Length, 2) != "1,")
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

        if (endIndex == -1)
            endIndex = json.Length;

        // PD リーガル情報
        //const string PDLegalityTag = "\"penny\":";
        //const string LegalValue = "\"legal\"";
        //int regalityIndex = response.IndexOf(PDLegalityTag, startIndex, endIndex - startIndex);
        //string isPDRegal = response.IndexOf(LegalValue, regalityIndex, PDLegalityTag.Length + LegalValue.Length) != -1 ? "[PD] " : string.Empty;

        const string TixTag = "\"tix\":";
        startIndex = json.IndexOf(TixTag, startIndex, endIndex - startIndex);

        if (startIndex == -1 || TrySubstring(json, startIndex + TixTag.Length, 4) == "null")
            return NoPrice;

        startIndex += TixTag.Length;

        if (startIndex >= json.Length)
            return NoPrice;

        startIndex = json.IndexOf('"', startIndex) + 1;

        if (startIndex == 0)
            return NoPrice;

        endIndex = json.IndexOf('"', startIndex);
        string tix = TrySubstring(json, startIndex, endIndex - startIndex);

        if (tix == null)
            return NoPrice;

        return tix + " tix";
    }

    private static string TrySubstring(string target, int startIndex, int length)
    {
        if (startIndex < 0 || startIndex + length > target.Length)
            return null;

        return target.Substring(startIndex, length);
    }
#endif
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
