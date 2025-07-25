﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Mojp;

/// <summary>
/// このアプリ用の <see cref="ICommand"/> の実装です。
/// </summary>
public abstract class Command : ICommand, INotifyPropertyChanged
{
    private bool isVisible = true;

    public Command(MainViewModel viewModel, string name)
    {
        ViewModel = viewModel;
        Name = name;

        CommandMap.Add(name, this);
    }

    public MainViewModel ViewModel { get; }

    /// <summary>
    /// シリアライズ化されたコマンド名とインスタンス化されたコマンドを関連付ける <see cref="Dictionary{string,Command}"/> です。
    /// </summary>
    public static Dictionary<string, Command> CommandMap { get; } = new Dictionary<string, Command>(5);

    /// <summary>
    /// シリアライズに使うコマンド名を取得します。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// ユーザーに表示するコマンド名を取得します。
    /// </summary>
    public abstract string Header { get; }

    /// <summary>
    /// ユーザーに表示するアイコン画像のパスを取得します。
    /// </summary>
    public virtual string Image { get; }

    /// <summary>
    /// このコマンドをツールバーに表示するかどうかを示す値を取得または設定します。
    /// </summary>
    public bool IsVisible
    {
        get => isVisible;
        set
        {
            isVisible = value;
            OnPropertyChanged();
        }
    }

    public virtual bool IsEnabled => true;

    public virtual bool CanExecute(object parameter) => true;

    public abstract void Execute(object parameter);

    /// <summary>
    /// このコマンドの有効・無効が切り替わったことを通知します。
    /// </summary>
    public void OnCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public event EventHandler CanExecuteChanged;

    public event PropertyChangedEventHandler PropertyChanged;
}

public sealed class CopyCardNameCommand(MainViewModel viewModel) : Command(viewModel, "CopyCardName")
{
    public sealed override string Header => "カード名をコピー";

    public sealed override string Image => @"Resources\Copy.png";

    public sealed override bool CanExecute(object parameter) => ViewModel?.SelectedCard != null;

    public sealed override void Execute(object parameter)
    {
        // 日本語訳が無いカードについては、コピー不可だったが、代わりに英語名をコピーするように仕様変更
        string name = ViewModel?.SelectedCard?.JapaneseName;

        if (name != null)
            Clipboard.SetText(name);
    }
}

public sealed class CopyEnglishNameCommand(MainViewModel viewModel) : Command(viewModel, "CopyEnglishName")
{
    public sealed override string Header => "カード名 (英語) をコピー";

    public sealed override string Image => @"Resources\CopyEn.png";

    public sealed override bool CanExecute(object parameter) => ViewModel?.SelectedCard?.EnglishName != null;

    public sealed override void Execute(object parameter)
    {
        // MO ヴァンガードは MO 上ではカード名が "Avatar - ..." となっている。
        //（ただしゲーム上ではカード名に "Avatar - " を含まない。例：Necropotence Avatar のカードテキスト）
        // そこで、日本語名の代わりにオラクルでのカード名である "... Avatar" を表示し、それをコピーするようにする
        string name = ViewModel?.SelectedCard?.EnglishName;

        if (name != null)
            Clipboard.SetText(name);
    }
}

public sealed class GoToWikiCommand(MainViewModel viewModel) : Command(viewModel, "GoToWiki")
{
    public sealed override string Header => "カードを MTG Wiki で調べる";

    public sealed override string Image => @"Resources\Web.png";

#if OFFLINE
    public sealed override bool IsEnabled => false;
#endif

    public sealed override bool CanExecute(object parameter)
    {
#if !OFFLINE
        var card = ViewModel?.SelectedCard;

        if (card == null)
            return false;

        // 明示的なリンク無効化
        if (card.WikiLink == string.Empty)
            return false;

        // 特殊パターンのリンク指定
        if (card.WikiLink != null)
            return true;

        // トークンで該当するページとなると、クリーチャータイプの解説ページがあるが、ややこしいパターンもあるのでリンクを無効にする
        if (card.Type.StartsWith("トークン", StringComparison.Ordinal))
            return false;

        return true;
#else
        return false;
#endif
    }

    public sealed override void Execute(object parameter)
    {
#if !OFFLINE
        var card = ViewModel?.SelectedCard;

        if (card == null)
            return;

        var sb = new StringBuilder("http://mtgwiki.com/wiki/", 254);

        if (card.WikiLink == null)
        {
            if (card.HasJapaneseName)
                sb.Append(Uri.EscapeDataString(card.JapaneseName)).Append('/');

            sb.Append(Uri.EscapeDataString(card.Name.Replace(' ', '_')));
        }
        else
            sb.Append(Uri.EscapeUriString(card.WikiLink));

        Process.Start(sb.ToString());
#endif
    }
}

public sealed class OptionCommand(MainViewModel viewModel) : Command(viewModel, "Option")
{
    public sealed override string Header => "設定";

    public sealed override string Image => @"Resources\Gears.png";

    public sealed override void Execute(object parameter) => App.CurrentMainWindow?.OnOption(this, null);
}
