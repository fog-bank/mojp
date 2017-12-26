using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Mojp
{
    public static class CardPrice
    {
        public static readonly DependencyProperty TargetCardProperty 
            = DependencyProperty.RegisterAttached("TargetCard", typeof(Card), typeof(CardPrice), 
                new FrameworkPropertyMetadata(Card.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure, OnTargetCardChanged));

        public static readonly DependencyProperty LegalityProperty
            = DependencyProperty.RegisterAttached("Legality", typeof(Card), typeof(CardPrice),
                new FrameworkPropertyMetadata(Card.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure, OnLegalityChanged));

        // card_name -> (tix_info, expire_time)
        private static readonly ConcurrentDictionary<string, Tuple<string, DateTime>> prices 
            = new ConcurrentDictionary<string, Tuple<string, DateTime>>();
        private const string CacheFileName = "price_list.txt";

        [AttachedPropertyBrowsableForType(typeof(TextBlock))]
        public static Card GetTargetCard(TextBlock element) => element.GetValue(TargetCardProperty) as Card;

        [AttachedPropertyBrowsableForType(typeof(TextBlock))]
        public static void SetTargetCard(TextBlock element, Card value) => element.SetValue(TargetCardProperty, value);

        [AttachedPropertyBrowsableForType(typeof(UIElement))]
        public static Card GetLegality(UIElement element) => element.GetValue(TargetCardProperty) as Card;

        [AttachedPropertyBrowsableForType(typeof(UIElement))]
        public static void SetLegality(UIElement element, Card value) => element.SetValue(TargetCardProperty, value);

        public static string GetPrice(Card card)
        {
            if (IsSpecialCard(card))
                return string.Empty;

            if (prices.TryGetValue(card.Name, out var value) && value?.Item1 != null)
                return value.Item1;
            else
                return "価格取得中";
        }

        public static void OpenCacheData()
        {
            if (!File.Exists(CacheFileName))
                return;

            using (var sr = File.OpenText(CacheFileName))
            {
                while (!sr.EndOfStream)
                {
                    string name = sr.ReadLine();
                    string tix = sr.ReadLine();
                    string expire = sr.ReadLine();

                    prices.TryAdd(name, Tuple.Create(tix, DateTime.Parse(expire)));
                }
            }
        }

        public static void SaveCacheData()
        {
            using (var sw = File.CreateText(CacheFileName))
            {
                var now = DateTime.Now;
                var pairs = prices.Where(pair => !string.IsNullOrEmpty(pair.Value?.Item1) && pair.Value.Item2 >= now).ToList();

                foreach (var pair in pairs)
                {
                    sw.WriteLine(pair.Key);
                    sw.WriteLine(pair.Value.Item1);
                    sw.WriteLine(pair.Value.Item2);
                }
            }
        }

        private static async void OnTargetCardChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var card = e.NewValue as Card;

            if (IsSpecialCard(card))
                return;

            if (prices.TryGetValue(card.Name, out var value))
            {
                // まだ有効期限内
                if (value != null && value.Item2 > DateTime.Now)
                    return;
            }
            else
                prices.TryAdd(card.Name, null);
            
            // scryfall.com に問い合わせ（分割カード名は補正をかける）
            string tix = await App.GetCardPrice(
                card.WikiLink != null && card.WikiLink.Contains("/") ? card.WikiLink.Split('/')[1] : card.Name);

            if (tix == null)
                return;
            
            Debug.WriteLine(card.Name + " | " + tix + " (" + DateTime.Now.TimeOfDay + ")");

            value = Tuple.Create(tix, DateTime.Now + TimeSpan.FromDays(1));
            prices.TryUpdate(card.Name, value, null);
            card.OnUpdatePrice();
        }

        private static void OnLegalityChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var element = sender as UIElement;
            var card = e.NewValue as Card;

            if (element == null)
                return;

            element.Visibility = App.IsPDLegal(card) ? Visibility.Visible : Visibility.Collapsed;
        }

            private static bool IsSpecialCard(Card card)
        {
            if (card == null || string.IsNullOrEmpty(card.Name))
                return true;

            if (card.Type != null && (card.Type.Length == 0 || card.Type.StartsWith("トークン") ||
                card.Type.StartsWith("ヴァンガード") || card.Type.StartsWith("次元") || card.Type.StartsWith("現象")))
                return true;

            return false;
        }
    }
}