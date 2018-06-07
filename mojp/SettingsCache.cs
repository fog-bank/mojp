using System;
using System.Globalization;

namespace Mojp
{
    /// <summary>
    /// 設定の値をローカル変数に保持します。
    /// </summary>
    /// <remarks>
    /// <see cref="PDListLastTimeUtc"/> を除き、プロパティ値の直接の Read/Write は ViewModel 越しに行う。Read のみなら他の場所でも可
    /// </remarks>
    public class SettingsCache
    {
        private double width = 330;
        private double height = 260;
        private int fontSize = 12;
        private int refreshInterval = 4000;

        public SettingsCache()
        { }

        // バージョン情報
        public bool AutoVersionCheck { get; set; }

        public bool AcceptsPrerelease { get; set; }

        // 表示
        public double WindowWidth
        {
            get => width;
            set
            {
                if (width > 0)
                    width = value;
            }
        }

        public double WindowHeight
        {
            get => height;
            set
            {
                if (width > 0)
                    height = value;
            }
        }

        public double WindowLeft { get; set; }

        public double WindowTop { get; set; }

        public string CardTextFontFamily { get; set; }

        public int CardTextFontSize
        {
            get => fontSize;
            set
            {
                if (value > 0)
                    fontSize = value;
            }
        }

        public CardDisplayNameType CardDisplayNameType { get; set; }

        public bool TopMost { get; set; }

        // 動作
        public bool ShowBasicLands { get; set; }

        public bool AutoRefresh { get; set; }

        public int RefreshIntervalMilliseconds
        {
            get => refreshInterval;
            set
            {
                if (value > 0)
                    refreshInterval = value;
            }
        }

        public bool GetPDList { get; set; }

        public bool GetCardPrice { get; set; }

        public string PDListLastTimeUtc { get; set; }

        /// <summary>
        /// 設定内容を <see cref="Settings.Default"/> から読み込みます。
        /// </summary>
        public void Read()
        {
            var opt = Settings.Default;

            AutoVersionCheck = opt.AutoVersionCheck;
            AcceptsPrerelease = opt.AcceptsPrerelease;

            WindowWidth = opt.WindowWidth;
            WindowHeight = opt.WindowHeight;
            WindowLeft = opt.WindowLeft;
            WindowTop = opt.WindowTop;
            CardTextFontFamily = opt.CardTextFontFamily;
            CardTextFontSize = opt.CardTextFontSize;

            int enumValue = opt.CardDisplayNameType;
            if (enumValue >= 0 && enumValue <= 4)
                CardDisplayNameType = (CardDisplayNameType)enumValue;
            else
            {
                // 現在の OS 表示言語に基づき、既定値を決定し、次からその値を使う
                CardDisplayNameType = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja" ?
                    CardDisplayNameType.Japanese : CardDisplayNameType.English;
            }

            TopMost = opt.TopMost;

            ShowBasicLands = opt.ShowBasicLands;
            AutoRefresh = opt.AutoRefresh;
            RefreshIntervalMilliseconds = (int)opt.RefreshInterval.TotalMilliseconds;
            GetPDList = opt.GetPDList;
            GetCardPrice = opt.GetCardPrice;
            PDListLastTimeUtc = opt.PDListLastTimeUtc;
        }

        /// <summary>
        /// 設定内容を <see cref="Settings.Default"/> に書き戻します。
        /// </summary>
        public void Write()
        {
            var opt = Settings.Default;

            opt.AutoVersionCheck = AutoVersionCheck;
            opt.AcceptsPrerelease = AcceptsPrerelease;

            opt.WindowWidth = WindowWidth;
            opt.WindowHeight = WindowHeight;
            opt.WindowLeft = WindowLeft;
            opt.WindowTop = WindowTop;
            opt.CardTextFontFamily = CardTextFontFamily;
            opt.CardTextFontSize = CardTextFontSize;
            opt.CardDisplayNameType = (int)CardDisplayNameType;
            opt.TopMost = TopMost;

            opt.ShowBasicLands = ShowBasicLands;
            opt.AutoRefresh = AutoRefresh;
            opt.RefreshInterval = TimeSpan.FromMilliseconds(RefreshIntervalMilliseconds);
            opt.GetPDList = GetPDList;
            opt.GetCardPrice = GetCardPrice;
            opt.PDListLastTimeUtc = PDListLastTimeUtc;
        }
    }

    /// <summary>
    /// タブにカード名を表示するときの書式を指定します。
    /// </summary>
    public enum CardDisplayNameType
    {
        Default = -1,
        Japanese,
        JananeseEnglish,
        EnglishJapanese,
        English
    }
}
