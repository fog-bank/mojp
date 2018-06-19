using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Mojp
{
    /// <summary>
    /// <see cref="MainWindow"/> のビュー モデルを提供します。
    /// </summary>
    public sealed partial class MainViewModel : INotifyPropertyChanged
    {
        private readonly SettingsCache settings = App.SettingsCache;
        private AutomationHandler automation;
        private DispatcherTimer timer;
        private int selectedIndex = -1;

        public MainViewModel()
        {
            SetMessage(AutoRefresh ?
                "MO の Preview Pane を探しています" :
                "MO の Preview Pane を表示させた状態で、右上のカメラアイコンのボタンを押してください");

            CaptureCommand = new CaptureCommand(this);
            CopyCardNameCommand = new CopyCardNameCommand(this);
            CopyEnglishNameCommand = new CopyEnglishNameCommand(this);
            GoToWikiCommand = new GoToWikiCommand(this);
            OptionCommand = new OptionCommand(this);
            ArrangeToolbarCommands();

            automation = new AutomationHandler(this);
        }

        public CaptureCommand CaptureCommand { get; }
        public CopyCardNameCommand CopyCardNameCommand { get; }
        public CopyEnglishNameCommand CopyEnglishNameCommand { get; }
        public GoToWikiCommand GoToWikiCommand { get; }
        public OptionCommand OptionCommand { get; }

        /// <summary>
        /// このアプリケーションで使用する表示フォントを取得または設定します。
        /// </summary>
        public string FontFamily
        {
            get => settings.CardTextFontFamily;
            set
            {
                settings.CardTextFontFamily = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// このアプリケーションで使用するフォントサイズを取得または設定します。
        /// </summary>
        public int FontSize
        {
            get => settings.CardTextFontSize;
            set
            {
                settings.CardTextFontSize = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// このアプリケーションを常に手前に表示するかどうかを示す値を取得または設定します。
        /// </summary>
        public bool TopMost
        {
            get => settings.TopMost;
            set
            {
                settings.TopMost = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// カード名の表示方法を取得または設定します。
        /// </summary>
        public CardDisplayNameType CardDisplayNameType
        {
            get => settings.CardDisplayNameType;
            set
            {
                settings.CardDisplayNameType = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// ツールバーを表示するかどうかを示す値を取得または設定します。
        /// </summary>
        public bool EnableToolbar
        {
            get => settings.EnableToolbar;
            set
            {
                settings.EnableToolbar = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// ツールバーに表示するコマンドのコレクションを取得します。
        /// </summary>
        public ObservableCollection<Command> ToolbarCommands { get; } = new ObservableCollection<Command>();

        /// <summary>
        /// このアプリケーションのウィンドウの幅を取得または設定します。
        /// </summary>
        public double WindowWidth
        {
            get => settings.WindowWidth;
            set
            {
                settings.WindowWidth = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// このアプリケーションのウィンドウの高さを取得または設定します。
        /// </summary>
        public double WindowHeight
        {
            get => settings.WindowHeight;
            set
            {
                settings.WindowHeight = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// このアプリケーションのウィンドウの左の表示位置を取得または設定します。
        /// </summary>
        public double WindowLeft
        {
            get => settings.WindowLeft;
            set
            {
                settings.WindowLeft = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// このアプリケーションのウィンドウの上の表示位置を取得または設定します。
        /// </summary>
        public double WindowTop
        {
            get => settings.WindowTop;
            set
            {
                settings.WindowTop = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 基本土地に反応するかどうかを示す値を取得または設定します。
        /// </summary>
        public bool ShowBasicLands
        {
            get => settings.ShowBasicLands;
            set
            {
                settings.ShowBasicLands = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Preview Page の探索を自動化するかどうかの値を取得または設定します。
        /// </summary>
        public bool AutoRefresh
        {
            get => settings.AutoRefresh;
            set
            {
                settings.AutoRefresh = value;
                OnPropertyChanged();
                CaptureCommand.OnAutoRefreshChanged();
            }
        }

        /// <summary>
        /// Preview Page の探索を行う間隔を取得します。
        /// </summary>
        public TimeSpan RefreshInterval => TimeSpan.FromMilliseconds(RefreshIntervalMilliseconds);

        /// <summary>
        /// Preview Page の探索を行う間隔をミリ秒単位で取得または設定します。
        /// </summary>
        public int RefreshIntervalMilliseconds
        {
            get => settings.RefreshIntervalMilliseconds;
            set
            {
                settings.RefreshIntervalMilliseconds = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 自動的に更新を確認するかどうかを示す値を取得または設定します。
        /// </summary>
        public bool AutoVersionCheck
        {
            get => settings.AutoVersionCheck;
            set
            {
                settings.AutoVersionCheck = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 開発版の更新も確認するかどうかを示す値を取得または設定します。
        /// </summary>
        public bool AcceptsPrerelease
        {
            get => settings.AcceptsPrerelease;
            set
            {
                settings.AcceptsPrerelease = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// カード価格を scryfall.com に問い合わせるかどうかを示す値を取得または設定します。
        /// </summary>
        public bool GetCardPrice
        {
            get => settings.GetCardPrice;
            set
            {
                settings.GetCardPrice = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Penny Dreadful のカードリストを取得するかどうかを示す値を取得または設定します。
        /// </summary>
        public bool GetPDList
        {
            get => settings.GetPDList;
            set
            {
                settings.GetPDList = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 表示中のカードのコレクションを取得します。
        /// </summary>
        public ObservableCollection<Card> Cards { get; } = new ObservableCollection<Card>();

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

                CopyCardNameCommand?.OnCanExecuteChanged();
                CopyEnglishNameCommand?.OnCanExecuteChanged();
                GoToWikiCommand?.OnCanExecuteChanged();
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
        /// UI スレッド上で、カードテキストの代わりにメッセージを表示します。
        /// </summary>
        public void InvokeSetMessage(string text) => App.CurrentDispatcher.Invoke(() => SetMessage(text));

        /// <summary>
        /// 指定したカードを表示します。
        /// </summary>
        public void SetCard(Card card)
        {
            if (card == null)
                return;

            if (Cards.Count > 0)
            {
                Cards[0] = card;

                // 項目数が減る場合に末端から削除
                for (int i = Cards.Count - 1; i >= 1; i--)
                    Cards.RemoveAt(i);
            }
            else
                Cards.Add(card);

            if (card.RelatedCardName != null)
            {
                foreach (string relatedName in card.RelatedCardNames)
                {
                    if (App.Cards.TryGetValue(relatedName, out var card2))
                        Cards.Add(card2);
                }
            }
            SelectedIndex = 0;
        }

        /// <summary>
        /// UI スレッド上で、指定したカードを表示します。
        /// </summary>
        public void InvokeSetCard(Card card) => App.CurrentDispatcher.Invoke(() => SetCard(card));

        public void InvokeSetCards(List<Card> cards)
        {
            App.CurrentDispatcher.Invoke(SetCards);

            void SetCards()
            {
                int j = 0;

                for (int i = 0; i < Cards.Count && j < cards.Count; i++, j++)
                    Cards[i] = cards[j];

                // 項目数が減る場合：末端から削除
                for (int i = Cards.Count - 1; i >= cards.Count; i--)
                    Cards.RemoveAt(i);

                // 項目数が増える場合：継続して追加
                for (; j < cards.Count; j++)
                    Cards.Add(cards[j]);

                SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 画面を更新するように要求します。
        /// </summary>
        public void RefreshTab() => Task.Run(automation.SearchCardName);

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
        /// 設定の値を使ってツールバーの配置を変更します。
        /// </summary>
        public void ArrangeToolbarCommands()
        {
            for (int i = 0; i + 1 < settings.ToolbarCommands.Count; i += 2)
            {
                if (Command.CommandMap.TryGetValue(settings.ToolbarCommands[i], out var command))
                {
                    ToolbarCommands.Add(command);
                    command.IsVisible = settings.ToolbarCommands[i + 1] == "1";
                }
            }
        }

        /// <summary>
        /// 各リソースを解放します。
        /// </summary>
        public void Release()
        {
            var commandNames = new List<string>(ToolbarCommands.Count);
            settings.ToolbarCommands = commandNames;

            foreach (var command in ToolbarCommands)
            {
                commandNames.Add(command.Name);
                commandNames.Add(command.IsVisible ? "1" : "0");
            }
            
            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= OnCapture;
                timer = null;
            }
            Cards.Clear();

            Task.Run(automation.Release);
        }

        /// <summary>
        /// MO のプレビューウィンドウを探し、UI テキストの変化イベントが発生するようにします。
        /// </summary>
        private void OnCapture(object sender, EventArgs e) => Task.Run(automation.CaptureMtgo);

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
