using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace Mojp
{
    public abstract class Command : ICommand
    {
        public Command(MainViewModel viewModel) => ViewModel = viewModel;

        public MainViewModel ViewModel { get; }

        public abstract string Name { get; }

        public abstract string Header { get; }

        public virtual string Image { get; }

        public virtual bool CanExecute(object parameter) => true;

        public abstract void Execute(object parameter);

        public void OnCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        public event EventHandler CanExecuteChanged;
    }

    public sealed class CaptureCommand : Command
    {
        public CaptureCommand(MainViewModel viewModel) : base(viewModel)
        { }

        public sealed override string Name => "Capture";

        public sealed override string Header => App.SettingsCache.AutoRefresh ? "MO を探す (自動化中)" : "MO を探す";

        public sealed override string Image => @"Resources\Camera.png";

        public sealed override void Execute(object parameter) => App.CurrentDispatcher.Invoke(() => ViewModel.CapturePreviewPane());
    }

    public sealed class CopyCardNameCommand : Command
    {
        public CopyCardNameCommand(MainViewModel viewModel) : base(viewModel)
        { }

        public sealed override string Name => "CopyCardName";

        public sealed override string Header => "カード名をコピー";

        public sealed override string Image => @"Resources\Copy.png";

        public sealed override bool CanExecute(object parameter) => ViewModel?.SelectedCard != null && ViewModel.SelectedCard.HasJapaneseName;

        public sealed override void Execute(object parameter)
        {
            string name = ViewModel?.SelectedCard?.JapaneseName;

            if (name != null)
                Clipboard.SetText(name);
        }
    }

    public sealed class CopyEnglishNameCommand : Command
    {
        public CopyEnglishNameCommand(MainViewModel viewModel) : base(viewModel)
        { }

        public sealed override string Name => "CopyEnglishName";

        public sealed override string Header => "カード名 (英語) をコピー";

        public sealed override string Image => @"Resources\Copy.png";

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

    public sealed class GoToWikiCommand : Command
    {
        public GoToWikiCommand(MainViewModel viewModel) : base(viewModel)
        { }

        public sealed override string Name => "GoToWiki";

        public sealed override string Header => "カードを MTG Wiki で調べる";

        public sealed override string Image => @"Resources\Web.png";

        public sealed override bool CanExecute(object parameter)
        {
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
            if (card.Type.StartsWith("トークン"))
                return false;

            return true;
        }

        public sealed override void Execute(object parameter)
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
    }

    public sealed class OptionCommand : Command
    {
        public OptionCommand(MainViewModel viewModel) : base(viewModel)
        { }

        public sealed override string Name => "Option";

        public sealed override string Header => "設定";

        public sealed override string Image => @"Resources\Gears.png";

        public sealed override void Execute(object parameter)
        { }
    }
}
