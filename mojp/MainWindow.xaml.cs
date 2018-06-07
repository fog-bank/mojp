using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;

namespace Mojp
{
    public partial class MainWindow : Window
    {
        public MainWindow() => InitializeComponent();

        public MainViewModel ViewModel => DataContext as MainViewModel;

        protected override void OnClosing(CancelEventArgs e)
        {
            var vm = ViewModel;

            if (vm != null)
                vm.Release();

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

        internal void ShowPDMessage(GetPDListResult result)
        {
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
                    pdError.Visibility = Visibility.Visible;
                    pdSuccess.Visibility = Visibility.Collapsed;
                    CardPrice.ClearPDLegalList();
                    break;

                default:
                    pdSuccess.Visibility = Visibility.Collapsed;
                    pdError.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private async void OnInitialized(object sender, EventArgs e)
        {
            var vm = ViewModel;

            imgLoading.Visibility = Visibility.Visible;

            await Task.Run(() =>
            {
                if (File.Exists("cards.xml"))
                    App.SetCardInfosFromXml("cards.xml");
                
                if (CardPrice.EnableCardPrice)
                    CardPrice.OpenCacheData();
            });

            if (vm.GetPDList)
            {
                var successPd = await CardPrice.GetOrOpenPDLegalFile(false);
                ShowPDMessage(successPd);
            }
            imgLoading.Visibility = Visibility.Hidden;
            vm.SetRefreshTimer(Dispatcher);

            if (vm.AutoVersionCheck && await App.IsOutdatedRelease(vm.AcceptsPrerelease))
                notifier.Visibility = Visibility.Visible;
        }

        private void OnCapture(object sender, RoutedEventArgs e) => ViewModel.CapturePreviewPane();

        private void OnCopyCardName(object sender, RoutedEventArgs e)
        {
            string name = ViewModel.SelectedCard?.JapaneseName;

            if (name != null)
             Clipboard.SetText(name);
        }

        private void OnCopyEnglishName(object sender, RoutedEventArgs e)
        {
            var card = ViewModel.SelectedCard;

            if (card == null)
                return;

            // MO ヴァンガードは MO 上ではカード名が "Avatar - ..." となっている。
            //（ただしゲーム上ではカード名に "Avatar - " を含まない。例：Necropotence Avatar のカードテキスト）
            // そこで、日本語名の代わりにオラクルでのカード名である "... Avatar" を表示し、それをコピーするようにする
            string name = card.Type == "ヴァンガード" ? card.JapaneseName : card.Name;

            if (name != null)
                Clipboard.SetText(name);
        }

        private void OnGoToWiki(object sender, RoutedEventArgs e)
        {
            var card = ViewModel?.SelectedCard;

            if (card == null)
                return;

            string link = card.WikiLink;

            if (link == null)
            {
                if (card.HasJapaneseName)
                {
                    link = card.JapaneseName + "/" + card.Name.Replace(' ', '_');
                }
                else
                    link = card.Name.Replace(' ', '_');
            }
            link = Uri.EscapeUriString(link);

            Process.Start("http://mtgwiki.com/wiki/" + link);
        }

        private async void OnOption(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            bool oldPrice = vm.GetCardPrice;
            bool oldPd = vm.GetPDList;

            var dlg = new OptionDialog(DataContext) { Owner = this };
            dlg.ShowDialog();

            Topmost = vm.TopMost;

            // Preview Pane の自動探索の設定を反映
            vm.SetRefreshTimer(Dispatcher);

            notifier.Visibility = vm.AutoVersionCheck &&
                await App.IsOutdatedRelease(vm.AcceptsPrerelease) ? Visibility.Visible : Visibility.Collapsed;

            // カード価格関連の変更を反映するために await
            imgLoading.Visibility = Visibility.Visible;

            if (vm.GetCardPrice != oldPrice)
            {
                if (CardPrice.EnableCardPrice)
                    await Task.Run(() => CardPrice.OpenCacheData());
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
        }

        private void OnGoToNewRelease(object sender, RoutedEventArgs e)
        {
            notifier.Visibility = Visibility.Collapsed;
            Process.Start((sender as Hyperlink).ToolTip.ToString());
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
}
