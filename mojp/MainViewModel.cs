using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Automation;
using System.Windows.Threading;

namespace Mojp
{
    /// <summary>
    /// <see cref="MainWindow"/> のビュー モデルを提供します。
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private string fontFamily = Settings.Default.CardTextFontFamily;
        private int fontSize = Settings.Default.CardTextFontSize;
        private bool topMost = Settings.Default.TopMost;
        private double width = Settings.Default.WindowWidth;
        private double height = Settings.Default.WindowHeight;
        private double left = Settings.Default.WindowLeft;
        private double top = Settings.Default.WindowTop;
        private bool showBasicLands = Settings.Default.ShowBasicLands;
        private bool autoRefresh = Settings.Default.AutoRefresh;
        private TimeSpan refreshInterval = Settings.Default.RefreshInterval;
        private bool autoVersionCheck = Settings.Default.AutoVersionCheck;
        private bool acceptsPrerelease = Settings.Default.AcceptsPrerelease;
        private bool getCardPrice = Settings.Default.GetCardPrice;
        private bool getPDList = Settings.Default.GetPDList;

        private ObservableCollection<Card> cards = new ObservableCollection<Card>();
        private int selectedIndex = -1;

        private AutomationElement prevWnd;
        private CacheRequest cacheReq = new CacheRequest();
        private Condition condition;
        private DispatcherTimer timer;

        public MainViewModel()
        {
            // UI 要素の名前だけキャッシュする
            cacheReq.Add(AutomationElement.NameProperty);
            cacheReq.AutomationElementMode = AutomationElementMode.None;

            // テキストが空でなく、特定の UI 要素でない TextBlock をすべて拾う
            condition = new AndCondition(
                new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock"),
                new NotCondition(new PropertyCondition(AutomationElement.NameProperty, string.Empty)),
                new PropertyCondition(AutomationElement.AutomationIdProperty, string.Empty));

            SetMessage(AutoRefresh ?
                "MO の Preview Pane を探しています" :
                "MO の Preview Pane を表示させた状態で、右上のカメラアイコンのボタンを押してください");
        }

        /// <summary>
        /// このアプリケーションで使用する表示フォントを取得または設定します。
        /// </summary>
        public string FontFamily
        {
            get => fontFamily;
            set
            {
                fontFamily = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// このアプリケーションで使用するフォントサイズを取得または設定します。
        /// </summary>
        public int FontSize
        {
            get => fontSize;
            set
            {
                fontSize = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// このアプリケーションを常に手前に表示するかどうかを示す値を取得または設定します。
        /// </summary>
        public bool TopMost
        {
            get => topMost;
            set
            {
                topMost = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// このアプリケーションのウィンドウの幅を取得または設定します。
        /// </summary>
        public double WindowWidth
        {
            get => width;
            set
            {
                width = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// このアプリケーションのウィンドウの高さを取得または設定します。
        /// </summary>
        public double WindowHeight
        {
            get => height;
            set
            {
                height = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// このアプリケーションのウィンドウの左の表示位置を取得または設定します。
        /// </summary>
        public double WindowLeft
        {
            get => left;
            set
            {
                left = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// このアプリケーションのウィンドウの上の表示位置を取得または設定します。
        /// </summary>
        public double WindowTop
        {
            get => top;
            set
            {
                top = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 基本土地に反応するかどうかを示す値を取得または設定します。
        /// </summary>
        public bool ShowBasicLands
        {
            get => showBasicLands;
            set
            {
                showBasicLands = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Preview Page の探索を自動化するかどうかの値を取得または設定します。
        /// </summary>
        public bool AutoRefresh
        {
            get => autoRefresh;
            set
            {
                autoRefresh = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Preview Page の探索を行う間隔を取得します。
        /// </summary>
        public TimeSpan RefreshInterval => refreshInterval;

        /// <summary>
        /// Preview Page の探索を行う間隔をミリ秒単位で取得または設定します。
        /// </summary>
        public int RefreshIntervalMilliseconds
        {
            get => (int)refreshInterval.TotalMilliseconds;
            set
            {
                if (value <= 0)
                    value = 1;

                refreshInterval = TimeSpan.FromMilliseconds(value);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 自動的に更新を確認するかどうかを示す値を取得または設定します。
        /// </summary>
        public bool AutoVersionCheck
        {
            get => autoVersionCheck;
            set
            {
                autoVersionCheck = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 開発版の更新も確認するかどうかを示す値を取得または設定します。
        /// </summary>
        public bool AcceptsPrerelease
        {
            get => acceptsPrerelease;
            set
            {
                acceptsPrerelease = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// カード価格を scryfall.com に問い合わせるかどうかを示す値を取得または設定します。
        /// </summary>
        public bool GetCardPrice
        {
            get => getCardPrice;
            set
            {
                getCardPrice = value;
                CardPrice.EnableCardPrice = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Penny Dreadful のカードリストを取得するかどうかを示す値を取得または設定します。
        /// </summary>
        public bool GetPDList
        {
            get => getPDList;
            set
            {
                getPDList = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 表示中のカードのコレクションを取得します。
        /// </summary>
        public ObservableCollection<Card> Cards => cards;

        /// <summary>
        /// <see cref="System.Windows.Controls.TabControl"/> で手前に表示しているカードのインデックス番号を取得または設定します。
        /// </summary>
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                selectedIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanCopyJapaneseName));
                OnPropertyChanged(nameof(CanCopyEnglishName));
                OnPropertyChanged(nameof(CanBrowseWiki));
            }
        }

        /// <summary>
        /// <see cref="System.Windows.Controls.TabControl"/> で手前に表示しているカードを取得します。
        /// </summary>
        public Card SelectedCard
        {
            get
            {
                if (SelectedIndex >= 0 && SelectedIndex < Cards.Count)
                {
                    var card = Cards[SelectedIndex];

                    if (!string.IsNullOrEmpty(card?.Name))
                        return card;
                }
                return null;
            }
        }

        /// <summary>
        /// 日本語カード名をコピーできるかどうかを示す値を取得します。
        /// </summary>
        public bool CanCopyJapaneseName => SelectedCard != null && SelectedCard.HasJapaneseName;

        /// <summary>
        /// 英語カード名をコピーできるかどうかを示す値を取得します。
        /// </summary>
        public bool CanCopyEnglishName => SelectedCard != null && SelectedCard.Name != null;

        /// <summary>
        /// MTG Wiki でカードを調べられるかどうかを示す値を取得します。
        /// </summary>
        public bool CanBrowseWiki
        {
            get
            {
                var card = SelectedCard;

                if (card == null)
                    return false;

                // 明示的なリンク無効化
                if (card.WikiLink == string.Empty)
                    return false;

                // 特殊パターンのリンク指定
                if (card.WikiLink != null)
                    return true;

                // トークンで該当するページとなると、クリーチャータイプの解説ページがあるが、ややこしいパターンもあるのでリンクを無効にする
                if (card.Type.StartsWith("トークン"))
                    return false;

                return true;
            }
        }

        /// <summary>
        /// カードテキストの代わりにメッセージを表示します。
        /// </summary>
        public void SetMessage(string text)
        {
            var card = new Card(text);

            if (Cards.Count > 0)
            {
                Cards[0] = card;

                for (int i = Cards.Count - 1; i >= 1; i--)
                    Cards.RemoveAt(i);
            }
            else
                Cards.Add(card);

            SelectedIndex = 0;
        }

        /// <summary>
        /// 画面を更新するように要求します。
        /// </summary>
        public void RefreshTab() => SearchCardName();

        /// <summary>
        /// Preview Pane を自動的に探索するためのタイマーを必要なら設定します。
        /// </summary>
        public void SetRefreshTimer(Dispatcher dispatcher)
        {
            if (timer == null)
            {
                if (AutoRefresh)
                {
                    CapturePreviewPane();
                    timer = new DispatcherTimer(RefreshInterval, DispatcherPriority.Normal, OnCapture, dispatcher);
                }
            }
            else
            {
                timer.IsEnabled = AutoRefresh;
                timer.Interval = RefreshInterval;
            }
        }

        /// <summary>
        /// MO のプレビューウィンドウを探します。
        /// </summary>
        public void CapturePreviewPane() => OnCapture(null, EventArgs.Empty);

        /// <summary>
        /// 現在の設定を <see cref="Settings"/> に書き戻します。
        /// </summary>
        public void SaveSettings()
        {
            Settings.Default.AcceptsPrerelease = AcceptsPrerelease;
            Settings.Default.AutoRefresh = AutoRefresh;
            Settings.Default.AutoVersionCheck = AutoVersionCheck;
            Settings.Default.CardTextFontFamily = FontFamily;
            Settings.Default.CardTextFontSize = FontSize;
            Settings.Default.GetCardPrice = GetCardPrice;
            Settings.Default.GetPDList = GetPDList;
            Settings.Default.RefreshInterval = TimeSpan.FromMilliseconds(RefreshIntervalMilliseconds);
            Settings.Default.ShowBasicLands = ShowBasicLands;
            Settings.Default.TopMost = TopMost;
            Settings.Default.WindowHeight = WindowHeight;
            Settings.Default.WindowLeft = WindowLeft;
            Settings.Default.WindowTop = WindowTop;
            Settings.Default.WindowWidth = WindowWidth;
        }

        /// <summary>
        /// 各リソースを解放します。
        /// </summary>
        public void Release()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= OnCapture;
                timer = null;
            }
            cacheReq = null;
            condition = null;

            App.CurrentDispatcher.BeginInvoke((Action)ReleaseAutomationElement);
        }

        /// <summary>
        /// UI Automation イベントハンドラーを削除し、<see cref="AutomationElement"/> への参照を解放します。
        /// </summary>
        public void ReleaseAutomationElement()
        {
            if (prevWnd != null)
            {
                prevWnd = null;
                Automation.RemoveAllEventHandlers();
                Debug.WriteLine("Removed automation event handlers.");
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) 
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// MO のプレビューウィンドウを探し、UI テキストの変化イベントが発生するようにします。
        /// </summary>
        private void OnCapture(object sender, EventArgs e)
        {
            var proc = Process.GetProcessesByName("mtgo");

            if (proc.Length == 0)
            {
                ReleaseAutomationElement();
                SetMessage("起動中のプロセスの中に MO が見つかりません。");
                return;
            }

            // "Preview" という名前のウィンドウを探す (なぜかルートの子で見つかる)
            AutomationElement currentPrevWnd = null;
            try
            {
                currentPrevWnd = AutomationElement.RootElement.FindFirst(TreeScope.Children,
                    new AndCondition(
                        new PropertyCondition(AutomationElement.ProcessIdProperty, proc[0].Id),
                        new PropertyCondition(AutomationElement.ClassNameProperty, "Window"),
                        new PropertyCondition(AutomationElement.NameProperty, "Preview")));
            }
            catch { Debug.WriteLine("Preview Pane の取得に失敗しました。"); }

            if (currentPrevWnd == null)
            {
                ReleaseAutomationElement();
                SetMessage("MO の Preview Pane が見つかりません。");
                return;
            }

            if (currentPrevWnd != prevWnd)
            {
                // 新しい Preview Pane が見つかった
                ReleaseAutomationElement();
                prevWnd = currentPrevWnd;

                // とりあえずカード名を探す
                SearchCardName();

                // UI テキストの変化を追う
                // 対戦中、カード情報は Preview Pane の直接の子ではなく、
                // ZoomCard_View というカスタムコントロールの下にくるので、スコープは子孫にしないとダメ
                using (cacheReq.Activate())
                    Automation.AddAutomationPropertyChangedEventHandler(
                        prevWnd, TreeScope.Descendants, OnAutomaionNamePropertyChanged, AutomationElement.NameProperty);

                if (SelectedCard == null)
                    SetMessage("準備完了");
            }
        }

        /// <summary>
        /// 変更された要素名がカード名かどうかを調べます。
        /// </summary>
        /// <remarks>AutomationPropertyChangedEventHandler は UI スレッドとは別スレッドで動いている</remarks>
        private void OnAutomaionNamePropertyChanged(object sender, AutomationPropertyChangedEventArgs e)
        {
            if (prevWnd == null)
                return;

            string name = GetNamePropertyValue(sender as AutomationElement);
            //Debug.WriteLineIf(!string.IsNullOrWhiteSpace(name), name);

            // キャッシュ無効化時
            if (name == null)
                return;

            // 新しいテキストがカード名かどうかを調べ、そうでないなら不必要な全体検索をしないようにする
            // トークンの場合は、カード名を含むとき (= コピートークン) と含まないとき (→ 空表示にする) とがあるので検索を続行する
            if (!App.Cards.ContainsKey(name) && !name.StartsWith("Token"))
            {
                string cardType = null;

                // 統治者以外の紋章やテキストレスのヴァンガードの場合は、確定で空表示にする
                if (name.StartsWith("Emblem") && name != "Emblem - ")
                    cardType = "紋章";
                else if (name.StartsWith("Avatar - "))
                    cardType = "ヴァンガード";
                else
                {
                    // 英雄譚はカード名を Automation で探せない
                    switch (name)
                    {
                        case "Jason Felix":     // Fall of the Thran
                        case "Noah Bradley":    // History of Benalia
                        case "Daniel Ljunggren":    // Triumph of Gerrard
                        case "Mark Tedin":      // The Antiquities War
                        case "James Arnold":    // The Mirari Conjecture
                        case "Franz Vohwinkel": // Time of Ice
                        case "Vincent Proce":   // Chainer's Torment
                        case "Jenn Ravenna":    // The Eldest Reborn
                        case "Joseph Meehan":   // Phyrexian Scriptures
                        case "Seb McKinnon":    // Rite of Belzenlok
                        case "Steven Belledin": // The First Eruption
                        case "Lake Hurwitz":    // The Flame of Keld
                        case "Adam Paquette":   // The Mending of Dominaria
                        case "Min Yum":         // Song of Freyalise
                            SearchSagaByArtist(name);
                            return;
                    }
                }

                if (cardType != null)
                    App.CurrentDispatcher.Invoke(() => SetMessage(cardType));

                return;
            }
            Debug.WriteLine(name);

            SearchCardName();
        }

        /// <summary>
        /// 現在の Preview Pane 内のテキストからカード名を取得し、表示します。
        /// </summary>
        private void SearchCardName()
        {
            AutomationElementCollection elements;
            using (cacheReq.Activate())
                elements = prevWnd?.FindAll(TreeScope.Descendants, condition);

            if (elements == null)
                return;

            // 一連のテキストからカード名を探す (合体カードなど複数のカード名にヒットする場合があるので一通り探し直す必要がある)
            var foundCards = new List<Card>();
            bool isToken = false;

            foreach (AutomationElement element in elements)
            {
                string name = GetNamePropertyValue(element);

                // キャッシュ無効化時
                if (name == null)
                    return;

                // WHISPER データベースからカード情報を取得
                if (App.Cards.TryGetValue(name, out var card))
                {
                    if (!foundCards.Contains(card))
                    {
                        foundCards.Add(card);

                        // 両面カードの場合に、Preview Pane に片面だけ表示されていても、もう一方の面を表示するようにする
                        if (card.RelatedCardName != null)
                        {
                            foreach (string relatedName in card.RelatedCardNames)
                            {
                                App.Cards.TryGetValue(relatedName, out var card2);

                                if (!foundCards.Contains(card2))
                                    foundCards.Add(card2);
                            }
                        }
                    }
                }
                else if (!isToken && name.StartsWith("Token"))
                    isToken = true;
            }

            // 設定によっては基本土地 5 種の場合に表示を変えないようにする
            if (!ShowBasicLands && foundCards.Count == 1)
            {
                switch (foundCards[0].Name)
                {
                    case "Plains":
                    case "Island":
                    case "Swamp":
                    case "Mountain":
                    case "Forest":
                        return;
                }
            }

            // 汎用のトークン
            if (foundCards.Count == 0 && isToken)
            {
                App.CurrentDispatcher.Invoke(() => SetMessage("トークン"));
                return;
            }

            App.CurrentDispatcher.Invoke(() =>
            {
                int j = 0;

                for (int i = 0; i < Cards.Count && j < foundCards.Count; i++, j++)
                    Cards[i] = foundCards[j];

                // 項目数が減る場合：末端から削除
                for (int i = Cards.Count - 1; i >= foundCards.Count; i--)
                    Cards.RemoveAt(i);

                // 項目数が増える場合：継続して追加
                for (; j < foundCards.Count; j++)
                    Cards.Add(foundCards[j]);

                SelectedIndex = 0;
            });
        }

        /// <summary>
        /// 英雄譚のカード名が見つからないため、アーティスト名で無理やり検索します。
        /// </summary>
        private void SearchSagaByArtist(string artist)
        {
            // 英雄譚かどうかをチェックする
            AutomationElement element = null;
            using (cacheReq.Activate())
            {
                element = prevWnd?.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "CardType"));
            }

            string cardType = GetNamePropertyValue(element);

            if (cardType != "Enchantment — Saga")
                return;

            Card foundCard = null;

            switch (artist)
            {
                case "Jason Felix":
                    App.Cards.TryGetValue("Fall of the Thran", out foundCard);
                    break;

                case "Noah Bradley":
                    App.Cards.TryGetValue("History of Benalia", out foundCard);
                    break;

                case "Daniel Ljunggren":
                    App.Cards.TryGetValue("Triumph of Gerrard", out foundCard);
                    break;

                case "Mark Tedin":
                    App.Cards.TryGetValue("The Antiquities War", out foundCard);
                    break;

                case "James Arnold":
                    App.Cards.TryGetValue("The Mirari Conjecture", out foundCard);
                    break;

                case "Franz Vohwinkel":
                    App.Cards.TryGetValue("Time of Ice", out foundCard);
                    break;

                case "Vincent Proce":
                    App.Cards.TryGetValue("Chainer's Torment", out foundCard);
                    break;

                case "Jenn Ravenna":
                    App.Cards.TryGetValue("The Eldest Reborn", out foundCard);
                    break;

                case "Joseph Meehan":
                    App.Cards.TryGetValue("Phyrexian Scriptures", out foundCard);
                    break;

                case "Seb McKinnon":
                    App.Cards.TryGetValue("Rite of Belzenlok", out foundCard);
                    break;

                case "Steven Belledin":
                    App.Cards.TryGetValue("The First Eruption", out foundCard);
                    break;

                case "Lake Hurwitz":
                    App.Cards.TryGetValue("The Flame of Keld", out foundCard);
                    break;

                case "Adam Paquette":
                    App.Cards.TryGetValue("The Mending of Dominaria", out foundCard);
                    break;

                case "Min Yum":
                    App.Cards.TryGetValue("Song of Freyalise", out foundCard);
                    break;
            }

            if (foundCard != null)
            {
                App.CurrentDispatcher.Invoke(() =>
                {
                    Cards[0] = foundCard;

                        // 項目数が減る場合に末端から削除
                        for (int i = Cards.Count - 1; i >= 1; i--)
                        Cards.RemoveAt(i);

                    SelectedIndex = 0;
                });
            }
            else
                App.CurrentDispatcher.Invoke(() => SetMessage("エンチャント ― 英雄譚"));
        }

        /// <summary>
        /// UI テキストからカード名の候補となる文字列を取得します。
        /// </summary>
        private static string GetNamePropertyValue(AutomationElement element)
        {
            string name = null;
            try
            {
                name = element?.Cached.Name;
            }
            catch
            {
                Debug.WriteLine("キャッシュされた Name プロパティ値の取得に失敗しました。");
                return null;
            }

            if (name == null)
                return string.Empty;

            // 特殊文字を置き換える (アキュート・アクセントつきの文字など)
            return Card.NormalizeName(name);
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
