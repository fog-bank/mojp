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

        private async void OnInitialized(object sender, EventArgs e)
        {
            if (File.Exists("cards.xml"))
            {
                imgLoading.Visibility = Visibility.Visible;

                await Task.Run(() =>
                {
                    App.SetCardInfosFromXml("cards.xml");
                });

                imgLoading.Visibility = Visibility.Hidden;
            }
            ViewModel.SetRefreshTimer(Dispatcher);

            if (ViewModel.AutoVersionCheck && await App.IsLatestRelease(ViewModel.AcceptsPrerelease))
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
                    link = Uri.EscapeUriString(card.JapaneseName) + "/" + card.Name.Replace(' ', '_');
                }
                else
                    link = card.Name.Replace(' ', '_');
            }
            else
                link = Uri.EscapeUriString(link);

            Process.Start("http://mtgwiki.com/wiki/" + link);
        }

        private async void OnOption(object sender, RoutedEventArgs e)
        {
            var dlg = new OptionDialog(DataContext)
            {
                Owner = this
            };
            dlg.ShowDialog();

            Topmost = ViewModel.TopMost;

            // Preview Pane の自動探索の設定を反映
            ViewModel.SetRefreshTimer(Dispatcher);

            notifier.Visibility = ViewModel.AutoVersionCheck &&
                await App.IsLatestRelease(ViewModel.AcceptsPrerelease) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnGoToNewRelease(object sender, RoutedEventArgs e)
        {
            notifier.Visibility = Visibility.Collapsed;
            Process.Start((sender as Hyperlink).ToolTip.ToString());
        }

        private void OnCloseNotifier(object sender, RoutedEventArgs e) => notifier.Visibility = Visibility.Collapsed;

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
