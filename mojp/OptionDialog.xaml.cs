using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;

namespace Mojp
{
	/// <summary>
	/// このアプリケーションの設定ダイアログを表します。
	/// </summary>
	public partial class OptionDialog : Window
	{
		public OptionDialog(object viewModel)
		{
			InitializeComponent();

			var vm = viewModel as MainViewModel;
			var fonts = Fonts.SystemFontFamilies;
			var fontNames = new List<string>(fonts.Count);
			var lang = this.Language;

			foreach (var font in fonts)
			{
				// 日本語フォントのみをリストにする
				string source;
				if (font.FamilyNames.TryGetValue(lang, out source))
					fontNames.Add(source);

				// フォント名を日本語で表示する前のバージョンのための措置 (~ 1.2.11030.7)
				if (source != null && font.Source == vm.FontFamily)
					vm.FontFamily = source;
			}
			fontNames.Sort();
			this.cmbFonts.ItemsSource = fontNames;
			
			this.DataContext = vm;
		}

		/// <summary>
		/// WHISPER の検索結果を格納したテキストファイルからカードテキストデータを構築します。必要なら永続化します。
		/// </summary>
		private void OnBrowseSearchTxt(object sender, RoutedEventArgs e)
		{
			imgLoaded.Visibility = Visibility.Collapsed;

			var dlg = new OpenFileDialog();
			dlg.FileName = "search.txt";
			dlg.CheckFileExists = true;
			dlg.Filter = "すべてのファイル (*.*)|*.*";

			if (dlg.ShowDialog(this) == true)
			{
				using (var stream = dlg.OpenFile())
				using (var sr = new StreamReader(stream, Encoding.GetEncoding("shift-jis")))
				{
					App.SetCardInfosFromWhisper(sr);

					if (File.Exists("appendix.xml"))
						App.FixCardInfo("appendix.xml");

					if (cbxSaveDb.IsChecked == true)
						App.SaveAsXml("cards.xml");

					imgLoaded.Visibility = Visibility.Visible;
				}
			}
		}

		private void OnClickHyperlink(object sender, RoutedEventArgs e)
		{
			Process.Start((sender as Hyperlink).ToolTip.ToString());
		}
	}
}
