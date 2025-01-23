using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;

namespace Mojp;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    public MainViewModel ViewModel => DataContext as MainViewModel;

    protected override void OnClosing(CancelEventArgs e)
    {
        ViewModel?.Release();

        base.OnClosing(e);
    }

    // ウィンドウ全体でドラッグ可能にする
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        // スクロールバーやリンクがある所はドラッグを開始しない（ボタンは処理済みと思われる）
        if (!e.Handled && e.LeftButton == MouseButtonState.Pressed && !(e.OriginalSource is Thumb || e.OriginalSource is Hyperlink))
            DragMove();
    }

#if !OFFLINE
    internal void ShowPDMessage(GetPDListResult result)
    {
        if (App.Cards.Count == 0)
            result = GetPDListResult.NoCheck;

        switch (result)
        {
            case GetPDListResult.New:
            case GetPDListResult.Update:
                pdSuccess.Visibility = Visibility.Visible;
                pdError.Visibility = Visibility.Collapsed;
                break;

            case GetPDListResult.NotFound:
            case GetPDListResult.Error:
            case GetPDListResult.Conflict:
                pdSuccess.Visibility = Visibility.Collapsed;
                pdError.Visibility = Visibility.Visible;
                CardPrice.ClearPDLegalList();
                break;

            default:
                pdSuccess.Visibility = Visibility.Collapsed;
                pdError.Visibility = Visibility.Collapsed;
                break;
        }
    }
#endif

    private async void OnInitialized(object sender, EventArgs e)
    {
        var vm = ViewModel;

        imgLoading.Visibility = Visibility.Visible;

        await Task.Run(() =>
        {
#if !OFFLINE
            if (File.Exists(App.GetPath("cards.xml")))
                App.SetCardInfosFromXml(App.GetPath("cards.xml"));

            if (CardPrice.EnableCardPrice)
                CardPrice.OpenCacheData();
#else
            App.SetCardInfosFromResource();
#endif
        });

        await vm.CaptureMagicOnline();

        if (App.Cards.Count == 0)
        {
            vm.InvokeSetMessage(
                "同梱のカードテキストデータ (cards.xml) を取得できません。" +
                "セキュリティ対策ソフトによってブロックされている可能性があります。" + Environment.NewLine +
                "[場所] " + Path.Combine(Path.GetDirectoryName(typeof(App).Assembly.Location), "cards.xml"));
        }

#if !OFFLINE
        if (vm.GetPDList)
        {
            var successPd = await CardPrice.GetOrOpenPDLegalFile(false);
            ShowPDMessage(successPd);
        }
#endif
        imgLoading.Visibility = Visibility.Hidden;
        vm.SetRefreshTimer(Dispatcher);

#if !OFFLINE
        if (vm.AutoVersionCheck && await App.IsOutdatedRelease(vm.AcceptsPrerelease))
            notifier.Visibility = Visibility.Visible;
#endif
    }

    internal async void OnOption(object sender, RoutedEventArgs e)
    {
        var vm = ViewModel;
        bool oldPrice = vm.GetCardPrice;
        bool oldPd = vm.GetPDList;

        var dlg = new OptionDialog(DataContext) { Owner = this };
        dlg.ShowDialog();

        Topmost = vm.TopMost;

        // MO の定期検索の設定を反映
        vm.SetRefreshTimer(Dispatcher);

#if !OFFLINE
        notifier.Visibility = vm.AutoVersionCheck &&
            await App.IsOutdatedRelease(vm.AcceptsPrerelease) ? Visibility.Visible : Visibility.Collapsed;

        // カード価格関連の変更を反映するために await
        imgLoading.Visibility = Visibility.Visible;

        if (vm.GetCardPrice != oldPrice)
        {
            if (CardPrice.EnableCardPrice)
                await Task.Run(CardPrice.OpenCacheData);
            else
                CardPrice.ClearCacheData();
        }

        if (vm.GetPDList != oldPd)
        {
            if (vm.GetPDList)
            {
                var successPd = await CardPrice.GetOrOpenPDLegalFile(false);
                ShowPDMessage(successPd);
            }
            else
            {
                CardPrice.ClearPDLegalList();
                pdSuccess.Visibility = Visibility.Collapsed;
                pdError.Visibility = Visibility.Collapsed;
            }
        }

        if (vm.GetCardPrice != oldPrice || vm.GetPDList != oldPd)
            vm.RefreshTab();

        imgLoading.Visibility = Visibility.Hidden;
#endif
    }

    private void OnGoToNewRelease(object sender, RoutedEventArgs e)
    {
#if !OFFLINE
        notifier.Visibility = Visibility.Collapsed;
        Process.Start((sender as Hyperlink).ToolTip.ToString());
#endif
    }

    private void OnCloseNotifier(object sender, RoutedEventArgs e) => notifier.Visibility = Visibility.Collapsed;

    private void OnClosePdSuccess(object sender, RoutedEventArgs e) => pdSuccess.Visibility = Visibility.Collapsed;

    private void OnClosePdError(object sender, RoutedEventArgs e) => pdError.Visibility = Visibility.Collapsed;

    private async void OnHide(object sender, RoutedEventArgs e)
    {
        Visibility = Visibility.Hidden;

        await Task.Delay(5000);

        Visibility = Visibility.Visible;
    }

    private void OnWindowMinimize(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void OnWindowClose(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);
}
