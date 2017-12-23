using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Mojp
{
    public class CardPrice
    {
        public static readonly DependencyProperty TargetCardProperty 
            = DependencyProperty.RegisterAttached("TargetCard", typeof(Card), typeof(CardPrice), 
                new FrameworkPropertyMetadata(Card.Empty, FrameworkPropertyMetadataOptions.AffectsRender, OnTargetCardChanged));

        // card_name -> (tix_info, expire_time)
        private static readonly Dictionary<string, Tuple<string, DateTime>> prices = new Dictionary<string, Tuple<string, DateTime>>();
        private const string CacheFileName = "price_list.txt";

        [AttachedPropertyBrowsableForType(typeof(TextBlock))]
        public static Card GetTargetCard(TextBlock element) => element.GetValue(TargetCardProperty) as Card;

        [AttachedPropertyBrowsableForType(typeof(TextBlock))]
        public static void SetTargetCard(TextBlock element, Card value) => element.SetValue(TargetCardProperty, value);

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

                    prices[name] = Tuple.Create(tix, DateTime.Parse(expire));
                }
            }
        }

        public static void SaveCacheData()
        {
            using (var sw = File.CreateText(CacheFileName))
            {
                var now = DateTime.Now;
                var pairs = prices.Where(pair => pair.Value.Item2 >= now).ToList();

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
            var element = sender as TextBlock;
            var card = e.NewValue as Card;

            if (element == null || card == null || string.IsNullOrEmpty(card.Name))
            {
                element.Text = string.Empty;
                return;
            }

            if (card.Type != null && (card.Type.Length == 0 || card.Type.StartsWith("トークン") || card.Type.StartsWith("ヴァンガード") ||
                card.Type.StartsWith("次元") || card.Type.StartsWith("現象")))
            {
                element.Text = string.Empty;
                return;
            }

            if (!prices.TryGetValue(card.Name, out var value) || value.Item1 == null || value.Item2 <= DateTime.Now)
            {
                element.Text = "価格取得中";

                string tix = await App.GetCardPrice(card.WikiLink != null && card.WikiLink.Contains("/") ? card.WikiLink.Split('/')[1] : card.Name);

                if (tix == null)
                    return;

                Debug.WriteLine(tix + " tix for: " + card.Name);

                value = Tuple.Create(tix, DateTime.Now + TimeSpan.FromDays(1));
                prices[card.Name] = value;
            }
            element.Text = value.Item1 + " tix";
        }
    }
}