using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace Mojp;

#if OFFLINE
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
#endif

/// <summary>
/// このアプリケーションの設定ダイアログを表します。
/// </summary>
public partial class OptionDialog : Window
{
#if !OFFLINE
    private bool isFixingCard;
#endif

    public OptionDialog(object viewModel)
    {
        InitializeComponent();

#if OFFLINE
        grpGoGitHub.IsEnabled = false;
        grpAutoCheck.IsEnabled = false;
        grpGetPDList.IsEnabled = false;
        grpPDRotationTime.IsEnabled = false;
        grpGetCardPrice.IsEnabled = false;
        grpEditCardData.IsEnabled = false;
#endif
        if (App.IsClickOnce)
        {
            grpAutoCheck.Visibility = Visibility.Collapsed;
            grpAutoCheckSub.Visibility = Visibility.Collapsed;
        }
        UpdatePDRotationTime();

        // フォントリストの初期化
        if (viewModel is MainViewModel vm)
        {
            var fonts = Fonts.SystemFontFamilies;
            var fontNames = new List<string>(fonts.Count);
            var lang = Language;

            foreach (var font in fonts)
            {
                // フォント名に日本語があるなら、それを使う
                if (!font.FamilyNames.TryGetValue(lang, out string source))
                    source = font.Source;

                fontNames.Add(source);

                // フォント名を日本語で表示する前のバージョンのための措置 (~ 1.2.11030.7)
                if (font.Source == vm.FontFamily)
                    vm.FontFamily = source;
            }
            fontNames.Sort();
            cmbFonts.ItemsSource = fontNames;

            // CardDisplayNameType 型リストの設定
            cmbCardDisplayName.SelectedIndex = vm.CardDisplayNameType switch
            {
                CardDisplayNameType.JananeseEnglish => 1,
                CardDisplayNameType.EnglishJapanese => 2,
                CardDisplayNameType.English => 3,
                _ => 0,
            };
            DataContext = vm;
        }
    }

    public MainViewModel ViewModel => DataContext as MainViewModel;

    public void UpdatePDRotationTime()
    {
#if !OFFLINE
        if (DateTime.TryParse(App.SettingsCache.PDServerLastTimeUtc, out var time))
            txbPDRotationTime.Text = time.ToString("g", CultureInfo.CurrentCulture);
        else
            txbPDRotationTime.Text = "不明";
#else
        txbPDRotationTime.Text = "無効";
#endif
    }

    protected override void OnClosing(CancelEventArgs e)
    {
#if !OFFLINE
        if (isFixingCard)
            e.Cancel = true;
#endif
        base.OnClosing(e);
    }

    private void OnCardDisplayNameChanged(object sender, SelectionChangedEventArgs e)
    {
        var vm = ViewModel;

        if (vm == null)
            return;

        switch (cmbCardDisplayName.SelectedIndex)
        {
            case 0:
                vm.CardDisplayNameType = CardDisplayNameType.Japanese;
                break;

            case 1:
                vm.CardDisplayNameType = CardDisplayNameType.JananeseEnglish;
                break;

            case 2:
                vm.CardDisplayNameType = CardDisplayNameType.EnglishJapanese;
                break;

            case 3:
                vm.CardDisplayNameType = CardDisplayNameType.English;
                break;
        }

        foreach (var card in vm.Cards)
            card?.OnUpdateDisplayName();
    }

    private void OnCustomizeToolbar(object sender, RoutedEventArgs e)
    {
        var dlg = new ToolbarDialog(DataContext) { Owner = this };
        dlg.ShowDialog();
    }

    private async void OnRetryPDList(object sender, RoutedEventArgs e)
    {
#if !OFFLINE
        imgLoading.Visibility = Visibility.Visible;

        var successPd = await CardPrice.GetOrOpenPDLegalFile(true);
        App.CurrentMainWindow?.ShowPDMessage(successPd);
        ViewModel.RefreshTab();
        UpdatePDRotationTime();

        imgLoading.Visibility = Visibility.Collapsed;
#endif
    }

    /// <summary>
    /// WHISPER の検索結果を格納したテキストファイルからカードテキストデータを構築します。必要なら永続化します。
    /// </summary>
    private async void OnBrowseSearchTxt(object sender, RoutedEventArgs e)
    {
#if !OFFLINE
        imgLoaded.Visibility = Visibility.Collapsed;

        var dlg = new OpenFileDialog
        {
            FileName = "search.txt",
            CheckFileExists = true,
            Multiselect = true,
            Filter = "テキスト ファイル (*.txt)|*.txt|すべてのファイル (*.*)|*.*"
        };

        if (dlg.ShowDialog(this) == true)
        {
            isFixingCard = true;
            imgLoading.Visibility = Visibility.Visible;

            var streams = dlg.OpenFiles();
            bool append = false;
#if NET
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
            foreach (var stream in streams)
            {
                using var sr = new StreamReader(stream, Encoding.GetEncoding("shift-jis"));
                App.SetCardInfosFromWhisper(sr, append);
                append = true;
            }

            await Task.Run(static () =>
            {
                string appendixPath = App.GetPath("appendix.xml");

                if (File.Exists(appendixPath))
                    Card.FixCardInfo(appendixPath);

                App.SaveAsXml(App.GetPath("cards.xml"));
            });

            imgLoading.Visibility = Visibility.Collapsed;
            imgLoaded.Visibility = Visibility.Visible;
            isFixingCard = false;
        }
#endif
        }

    private void OnTestBoxKeyDown(object sender, KeyEventArgs e)
    {
#if !OFFLINE
        if (e.Key != Key.Enter)
            return;

        string text = (sender as TextBox)?.Text;

        if (text == null)
            return;

        text = text.Trim();

        if (!App.TryGetCard(text, out var target))
        {
            foreach (var card in App.Cards.Values)
            {
                if (card.JapaneseName == text)
                {
                    target = card;
                    break;
                }
            }
        }
        ViewModel?.SetCard(target);
#endif
    }

    private void OnClickHyperlink(object sender, RoutedEventArgs e)
    {
#if !OFFLINE
        Process.Start((sender as Hyperlink).ToolTip.ToString());
#endif
    }
}
