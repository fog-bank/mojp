using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Mojp
{
	public partial class OptionDialog : Window
	{
		public OptionDialog(object vm)
		{
			InitializeComponent();

			var fonts = Fonts.SystemFontFamilies;
			var fontNames = new List<string>(fonts.Count);

			foreach (var font in fonts)
				fontNames.Add(font.Source);

			fontNames.Sort();
			this.cmbFonts.ItemsSource = fontNames;

			this.DataContext = vm;
		}

		private void OnBrowseSearchTxt(object sender, RoutedEventArgs e)
		{
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

					if (cbxSaveDb.IsChecked == true)
						App.SaveAsXml("cards.xml");
				}
			}
		}
	}
}
