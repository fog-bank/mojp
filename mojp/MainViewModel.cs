using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Mojp;

/// <summary>
/// <see cref="MainWindow"/> のビュー モデルを提供します。
/// </summary>
public sealed partial class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly SettingsCache settings = App.SettingsCache;
    private readonly AutomationHandler automation;
    private readonly ObservableCollection<Card> displayCards = [];
    private DispatcherTimer timer;
    private int selectedIndex = -1;

    public MainViewModel()
    {
        CopyCardNameCommand = new CopyCardNameCommand(this);
        CopyEnglishNameCommand = new CopyEnglishNameCommand(this);
        GoToWikiCommand = new GoToWikiCommand(this);
        OptionCommand = new OptionCommand(this);
        ArrangeToolbarCommands();

        automation = new AutomationHandler(this);
    }

    #region Commands

    public CopyCardNameCommand CopyCardNameCommand { get; }
    public CopyEnglishNameCommand CopyEnglishNameCommand { get; }
    public GoToWikiCommand GoToWikiCommand { get; }
    public OptionCommand OptionCommand { get; }

    #endregion

    #region Settings property

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

    #endregion

    /// <summary>
    /// Preview Page の探索を行う間隔を取得します。
    /// </summary>
    public TimeSpan RefreshInterval => TimeSpan.FromMilliseconds(RefreshIntervalMilliseconds);

    /// <summary>
    /// ツールバーに表示するコマンドのコレクションを取得します。
    /// </summary>
    public ObservableCollection<Command> ToolbarCommands { get; } = [];

    /// <summary>
    /// 表示中のカードのコレクションを取得します。
    /// </summary>
    public ObservableCollection<Card> Cards => displayCards;

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
            var card = Cards?.ElementAtOrDefault(SelectedIndex);
            return string.IsNullOrEmpty(card?.Name) ? null : card;
        }
    }

    /// <summary>
    /// カードテキストの代わりにメッセージを表示します。
    /// </summary>
    public void SetMessage(string text) => SetCard(new Card(text));

    /// <summary>
    /// UI スレッド上で、カードテキストの代わりにメッセージを表示します。
    /// </summary>
    public void InvokeSetMessage(string text) => App.CurrentDispatcher.Invoke(() => SetMessage(text));

    /// <summary>
    /// 指定したカードを表示します。
    /// </summary>
    public void SetCard(Card card)
    {
        card ??= Card.Empty;

        if (Cards != null && Cards.Count >= 1 && Cards[0] == card)
            return;

        if (Cards.Count > 0)
            Cards[0] = card;
        else
            Cards.Add(card);

        SelectedIndex = 0;
        int count = 1;

        foreach (string relatedName in card.RelatedCardNames)
        {
            if (App.TryGetCard(relatedName, out var card2))
            {
                if (count < Cards.Count)
                    Cards[count] = card2;
                else
                    Cards.Add(card2);

                count++;
            }
        }

        for (int i = Cards.Count - 1; i >= count; i--)
            Cards.RemoveAt(i);
    }

    /// <summary>
    /// UI スレッド上で、指定したカードを表示します。
    /// </summary>
    public void InvokeSetCard(Card card) => App.CurrentDispatcher.Invoke(() => SetCard(card));

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

#if OFFLINE
                if (command.Name == "GoToWiki")
                    command.IsVisible = false;
#endif
            }
        }
    }

    public async Task InitAutomation()
    {
        await automation.RegisterEventHandler();
        await automation.FindMagicOnline();
    }

    /// <summary>
    /// MO を定期的に探索するためのタイマーを設定します。
    /// </summary>
    public void SetRefreshTimer(Dispatcher dispatcher)
    {
        if (timer == null)
        {
            timer = new DispatcherTimer(RefreshInterval, DispatcherPriority.Normal, OnCapture, dispatcher);
        }
        else
            timer.Interval = RefreshInterval;
    }

    //public void RestartRefreshTimer() => timer?.Start();

    //public void StopRefreshTimer() => timer?.Stop();

    /// <summary>
    /// 画面を更新するように要求します。
    /// </summary>
    public void RefreshTab()
    {
        var card = SelectedCard;

        if (card != null)
        {
            SetCard(null);
            SetCard(card);
        }
    }

    /// <summary>
    /// 各リソースを解放します。
    /// </summary>
    public void Dispose()
    {
        var commandNames = new List<string>(ToolbarCommands.Count);

        foreach (var command in ToolbarCommands)
        {
            commandNames.Add(command.Name);
            commandNames.Add(command.IsVisible ? "1" : "0");
        }
        settings.ToolbarCommands = commandNames;

        if (timer != null)
        {
            timer.Stop();
            timer.Tick -= OnCapture;
            timer = null;
        }
        Cards.Clear();

        automation.Dispose();
    }

    private async void OnCapture(object sender, EventArgs e) => await automation.FindMagicOnline();

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public event PropertyChangedEventHandler PropertyChanged;
}
