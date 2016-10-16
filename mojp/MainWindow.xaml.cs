using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Mojp
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		public MainViewModel ViewModel
		{
			get { return DataContext as MainViewModel; }
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			ViewModel.Release();
			Settings.Default.Save();

			base.OnClosing(e);
		}

		// ウィンドウ全体でドラッグ可能にする
		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			if (e.LeftButton == MouseButtonState.Pressed)
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

			if (ViewModel.AutoRefresh)
				ViewModel.SetRefreshTimer(this.Dispatcher);
		}

		private void OnCopyCardName(object sender, RoutedEventArgs e)
		{
			var card = ViewModel?.CurrentCard;
			string name = card?.JapaneseName;

			if (string.IsNullOrEmpty(name))
				name = card?.Name;

			if (name != null)
				Clipboard.SetText(name);
		}

		private void OnVoice(object sender, RoutedEventArgs e)
		{
		}

		private async void OnHide(object sender, RoutedEventArgs e)
		{
			Visibility = Visibility.Hidden;

			await Task.Delay(5000);

			Visibility = Visibility.Visible;
		}

		private void OnCapture(object sender, RoutedEventArgs e)
		{
			ViewModel.CapturePreviewPane();
		}

		private void OnOption(object sender, RoutedEventArgs e)
		{
			// 設定画面を上にする
			Topmost = false;

			var dlg = new OptionDialog(DataContext);
			dlg.ShowDialog();

			Topmost = ViewModel.TopMost;

			// Preview Pane の自動探索の設定を反映
			ViewModel.SetRefreshTimer(this.Dispatcher);
		}

		private void OnWindowMinimize(object sender, RoutedEventArgs e)
		{
			SystemCommands.MinimizeWindow(this);
		}

		private void OnWindowClose(object sender, RoutedEventArgs e)
		{
			SystemCommands.CloseWindow(this);
		}
	}
}
