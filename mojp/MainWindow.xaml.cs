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
            ViewModel?.SaveSettings();
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
            imgLoading.Visibility = Visibility.Visible;
            CardPrice.EnableCardPrice = ViewModel.GetCardPrice;

            await Task.Run(() =>
            {
                if (File.Exists("cards.xml"))
                    App.SetCardInfosFromXml("cards.xml");
                
                if (CardPrice.EnableCardPrice)
                    CardPrice.OpenCacheData();
            });

            if (ViewModel.GetPDList)
            {
                var successPd = await CardPrice.GetOrOpenPDLegalFile(false);
                ShowPDMessage(successPd);
            }
            imgLoading.Visibility = Visibility.Hidden;
            ViewModel.SetRefreshTimer(Dispatcher);

            if (ViewModel.AutoVersionCheck && await App.IsOutdatedRelease(ViewModel.AcceptsPrerelease))
                notifier.Visibility = Visibility.Visible;
        }

        private void OnCapture(object sender, RoutedEventArgs e) => ViewModel.CapturePreviewPane();

        private void OnCopyCardName(object sender, RoutedEventArgs e) => Clipboard.SetText(ViewModel.SelectedCard.JapaneseName);

        private void OnCopyEnglishName(object sender, RoutedEventArgs e)
        {
            // MO ヴァンガードは MO 上ではカード名が "Avatar - ..." となっている。
            //（ただしゲーム上ではカード名に "Avatar - " を含まない。例：Necropotence Avatar のカードテキスト）
            // そこで、日本語名の代わりにオラクルでのカード名である "... Avatar" を表示し、それをコピーするようにする
            if (ViewModel.SelectedCard.Type != "ヴァンガード")
                Clipboard.SetText(ViewModel.SelectedCard.Name);
            else
                Clipboard.SetText(ViewModel.SelectedCard.JapaneseName);
        }

        private void OnGoToWiki(object sender, RoutedEventArgs e)
        {
            var card = ViewModel.SelectedCard;
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
            var dlg = new OptionDialog(DataContext)
            {
                Owner = this
            };
            bool oldPrice = ViewModel.GetCardPrice;
            bool oldPd = ViewModel.GetPDList;

            dlg.ShowDialog();

            Topmost = ViewModel.TopMost;

            // Preview Pane の自動探索の設定を反映
            ViewModel.SetRefreshTimer(Dispatcher);

            notifier.Visibility = ViewModel.AutoVersionCheck &&
                await App.IsOutdatedRelease(ViewModel.AcceptsPrerelease) ? Visibility.Visible : Visibility.Collapsed;

            // カード価格関連の変更を反映するために await
            imgLoading.Visibility = Visibility.Visible;

            if (ViewModel.GetCardPrice != oldPrice)
            {
                if (CardPrice.EnableCardPrice)
                    await Task.Run(() => CardPrice.OpenCacheData());
                else
                    CardPrice.ClearCacheData();
            }

            if (ViewModel.GetPDList != oldPd)
            {
                if (ViewModel.GetPDList)
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

            if (ViewModel.GetCardPrice != oldPrice || ViewModel.GetPDList != oldPd)
                ViewModel.RefreshTab();

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
