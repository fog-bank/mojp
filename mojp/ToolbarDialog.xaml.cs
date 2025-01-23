using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Mojp;

/// <summary>
/// Interaction logic for ToolbarDialog.xaml
/// </summary>
public partial class ToolbarDialog : Window
{
    public ToolbarDialog(object viewModel)
    {
        InitializeComponent();

        var vm = viewModel as MainViewModel;
        DataContext = vm;
        listBox.SelectedIndex = 0;
        FocusManager.SetFocusedElement(this, listBox);
    }

    public MainViewModel ViewModel => DataContext as MainViewModel;

    private void OnListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // ListBox の選択項目の変化に対応して、「上へ」「下へ」ボタンの有効無効を切り替える
        int index = listBox.SelectedIndex;
        btnUp.IsEnabled = index != 0;
        btnDown.IsEnabled = index + 1 < ViewModel.ToolbarCommands.Count;
    }

    private void OnCommandUp(object sender, RoutedEventArgs e)
    {
        int index = listBox.SelectedIndex;
        var commands = ViewModel?.ToolbarCommands;

        if (commands != null && index > 0 && index < commands.Count)
        {
            var command = commands[index];
            commands[index] = commands[index - 1];
            commands[index - 1] = command;

            listBox.SelectedIndex = index - 1;
            FocusManager.SetFocusedElement(this, listBox);
        }
    }

    private void OnCommandDown(object sender, RoutedEventArgs e)
    {
        int index = listBox.SelectedIndex;
        var commands = ViewModel?.ToolbarCommands;

        if (commands != null && index >= 0 && index < commands.Count - 1)
        {
            var command = commands[index];
            commands[index] = commands[index + 1];
            commands[index + 1] = command;

            listBox.SelectedIndex = index + 1;
            FocusManager.SetFocusedElement(this, listBox);
        }
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        var vm = ViewModel;
        var commands = vm?.ToolbarCommands;

        // CopyCardName,0,CopyEnglishName,0,GoToWiki,1,Option,1
        if (commands != null)
        {
            commands.Clear();

            commands.Add(vm.CopyCardNameCommand);
            commands[0].IsVisible = false;

            commands.Add(vm.CopyEnglishNameCommand);
            commands[1].IsVisible = false;

            commands.Add(vm.GoToWikiCommand);
#if !OFFLINE
            commands[2].IsVisible = true;
#else
            commands[2].IsVisible = false;
#endif
            commands.Add(vm.OptionCommand);
            commands[3].IsVisible = true;
        }
    }
}
