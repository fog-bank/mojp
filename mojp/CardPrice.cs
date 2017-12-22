using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Mojp
{
    public class CardPrice
    {
        public static readonly DependencyProperty TargetCardProperty = DependencyProperty.RegisterAttached(
            "TargetCard", typeof(Card), typeof(CardPrice), new FrameworkPropertyMetadata(Card.Empty, OnTargetCardChanged));

        private static readonly Dictionary<string, Tuple<string, DateTime>> prices = new Dictionary<string, Tuple<string, DateTime>>();

        [AttachedPropertyBrowsableForType(typeof(TextBlock))]
        public static Card GetTargetCard(TextBlock element) => element.GetValue(TargetCardProperty) as Card;

        [AttachedPropertyBrowsableForType(typeof(TextBlock))]
        public static void SetTargetCard(TextBlock element, Card value) => element.SetValue(TargetCardProperty, value);

        private static async void OnTargetCardChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var element = sender as TextBlock;
            var card = e.NewValue as Card;

            if (element == null || card == null || string.IsNullOrEmpty(card.Name))
                return;

            if (!prices.TryGetValue(card.Name, out var value) || value.Item1 == null || DateTime.Now - value.Item2 >= TimeSpan.FromDays(1))
            {
                var sw = Stopwatch.StartNew();

                string tix = await App.GetCardPrice(card.Name);

                Debug.WriteLine(tix + " tix for (" + sw.Elapsed + "): " + card.Name);

                value = Tuple.Create(tix, DateTime.Now);
                prices[card.Name] = value;
            }
            element.Text = value.Item1;
        }
    }
}