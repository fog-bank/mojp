using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;

namespace Mojp
{
	public partial class MainWindow : Window
	{
		private AutomationElement prevWnd;

		public MainWindow()
		{
			InitializeComponent();
		}

		public MainViewModel ViewModel
		{
			get { return DataContext as MainViewModel; }
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
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			Automation.RemoveAllEventHandlers();
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

		/// <summary>
		/// MO のプレビューウィンドウを探し、UI テキストの変化イベントが発生するようにします。
		/// </summary>
		private void OnCapture(object sender, RoutedEventArgs e)
		{
			Automation.RemoveAllEventHandlers();

			// 念のため、MO が起動していることを確認
			if (Process.GetProcessesByName("mtgo").Length == 0)
			{
				ViewModel.SetMessage("起動中のプロセスの中に MO が見つかりません。");
				return;
			}

			// "Preview" という名前のウィンドウを探す (なぜかルートの子で見つかる)
			prevWnd = AutomationElement.RootElement.FindFirst(TreeScope.Children,
				new AndCondition(
					new PropertyCondition(AutomationElement.ClassNameProperty, "Window"),
					new PropertyCondition(AutomationElement.NameProperty, "Preview")));

			if (prevWnd == null)
			{
				ViewModel.SetMessage("MO の Preview Pane が見つかりません。");
				return;
			}
			else
				ViewModel.SetMessage("準備完了");

			// UI テキストの変化を追う
			Automation.AddAutomationPropertyChangedEventHandler(prevWnd, TreeScope.Descendants, OnAutomaionNamePropertyChanged, AutomationElement.NameProperty);
		}

		private void OnAutomaionNamePropertyChanged(object sender, AutomationPropertyChangedEventArgs e)
		{
			// 新しいテキストがカード名かどうかを調べ、そうでないなら不必要な検索をしないようにする
			string srcName = GetNamePropertyValue(sender as AutomationElement);

			if (!App.Cards.ContainsKey(srcName))
			{
				// 紋章やヴァンガードの場合は空にする
				if (srcName.StartsWith("Emblem") || srcName == "Vanguard")
					Dispatcher.Invoke(() => ViewModel.CurrentCard = null);

				return;
			}

			// テキストが空でなく、特定の UI 要素でない TextBlock をすべて拾う
			var texts = prevWnd.FindAll(TreeScope.Descendants, 
				new AndCondition(
					new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock"),
					new NotCondition(new PropertyCondition(AutomationElement.NameProperty, string.Empty)),
					new PropertyCondition(AutomationElement.AutomationIdProperty, string.Empty)));

			// 一連のテキストからカード名を探す
			bool set = false;
			var annotations = new List<string>();

			foreach (AutomationElement text in texts)
			{
				string name = GetNamePropertyValue(text);

				// WHISPER データベースからカード情報を取得
				Card card;
				if (name != null && App.Cards.TryGetValue(name, out card))
				{
					if (!set)
					{
						// AutomationPropertyChangedEventHandler は UI スレッドとは別スレッド
						Dispatcher.Invoke(() => ViewModel.CurrentCard = card);
						set = true;
					}
					annotations.Add(card.Summary);
				}
			}

			// カード名が見つからなかった
			if (!set)
				Dispatcher.Invoke(() => ViewModel.CurrentCard = null);

			// 分割カードや両面カードなどは最初の情報のみを表示し、残りはツールチップにする
			if (annotations.Count > 1)
				Dispatcher.Invoke(() => ViewModel.CardToolTip = string.Join(Environment.NewLine + "--------" + Environment.NewLine, annotations));
		}

		/// <summary>
		/// UI テキストからカード名の候補となる文字列を取得します。
		/// </summary>
		private static string GetNamePropertyValue(AutomationElement src)
		{
			string name = null;
			try
			{
				name = src?.GetCurrentPropertyValue(AutomationElement.NameProperty)?.ToString();
			}
			catch { }

			if (name != null)
			{
				// 特殊文字を置き換える
				var sb = new StringBuilder(name.Length + 1);
				bool replaced = false;

				foreach (char c in name)
				{
					switch (c)
					{
						// Æther Vial など
						case 'Æ':
							sb.Append("AE");
							replaced = true;
							break;

						// Márton Stromgald や Dandân など
						case 'á':
						case 'â':
							sb.Append("a");
							replaced = true;
							break;

						default:
							sb.Append(c);
							break;
					}
				}

				if (replaced)
					name = sb.ToString();
			}
			return name;
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

		private void OnOption(object sender, RoutedEventArgs e)
		{
			Topmost = false;

			var dlg = new OptionDialog(DataContext);
			dlg.ShowDialog();

			Topmost = true;
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
