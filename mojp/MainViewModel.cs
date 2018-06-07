using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Automation;
using System.Windows.Threading;

namespace Mojp
{
    /// <summary>
    /// <see cref="MainWindow"/> のビュー モデルを提供します。
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly SettingsCache settings = App.SettingsCache;

        private int selectedIndex = -1;

        private AutomationElement prevWnd;
        private CacheRequest cacheReq = new CacheRequest();
        private Condition condition;
        private DispatcherTimer timer;

        public MainViewModel()
        {
            // UI 要素の名前だけキャッシュする
            cacheReq.Add(AutomationElement.NameProperty);
            cacheReq.AutomationElementMode = AutomationElementMode.None;

            // テキストが空でなく、特定の UI 要素でない TextBlock をすべて拾う
            condition = new AndCondition(
                new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock"),
                new NotCondition(new PropertyCondition(AutomationElement.NameProperty, string.Empty)),
                new PropertyCondition(AutomationElement.AutomationIdProperty, string.Empty));

            SetMessage(AutoRefresh ?
                "MO の Preview Pane を探しています" :
                "MO の Preview Pane を表示させた状態で、右上のカメラアイコンのボタンを押してください");
        }

        /// <summary>
        /// このアプリケーションで使用する表示フォントを取得または設定します。
        /// </summary>
        public string FontFamily
        {
            get => settings.CardTextFontFamily;
            set
            {
                settings.CardTextFontFamily = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// このアプリケーションで使用するフォントサイズを取得または設定します。
        /// </summary>
        public int FontSize
        {
            get => settings.CardTextFontSize;
            set
            {
                settings.CardTextFontSize = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// このアプリケーションを常に手前に表示するかどうかを示す値を取得または設定します。
        /// </summary>
        public bool TopMost
        {
            get => settings.TopMost;
            set
            {
                settings.TopMost = value;
                OnPropertyChanged();
            }
        }

        public CardDisplayNameType CardDisplayNameType
        {
            get => settings.CardDisplayNameType;
            set
            {
                settings.CardDisplayNameType = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// このアプリケーションのウィンドウの幅を取得または設定します。
        /// </summary>
        public double WindowWidth
        {
            get => settings.WindowWidth;
            set
            {
                settings.WindowWidth = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// このアプリケーションのウィンドウの高さを取得または設定します。
        /// </summary>
        public double WindowHeight
        {
            get => settings.WindowHeight;
            set
            {
                settings.WindowHeight = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// このアプリケーションのウィンドウの左の表示位置を取得または設定します。
        /// </summary>
        public double WindowLeft
        {
            get => settings.WindowLeft;
            set
            {
                settings.WindowLeft = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// このアプリケーションのウィンドウの上の表示位置を取得または設定します。
        /// </summary>
        public double WindowTop
        {
            get => settings.WindowTop;
            set
            {
                settings.WindowTop = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 基本土地に反応するかどうかを示す値を取得または設定します。
        /// </summary>
        public bool ShowBasicLands
        {
            get => settings.ShowBasicLands;
            set
            {
                settings.ShowBasicLands = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Preview Page の探索を自動化するかどうかの値を取得または設定します。
        /// </summary>
        public bool AutoRefresh
        {
            get => settings.AutoRefresh;
            set
            {
                settings.AutoRefresh = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Preview Page の探索を行う間隔を取得します。
        /// </summary>
        public TimeSpan RefreshInterval => TimeSpan.FromMilliseconds(RefreshIntervalMilliseconds);

        /// <summary>
        /// Preview Page の探索を行う間隔をミリ秒単位で取得または設定します。
        /// </summary>
        public int RefreshIntervalMilliseconds
        {
            get => settings.RefreshIntervalMilliseconds;
            set
            {
                settings.RefreshIntervalMilliseconds = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 自動的に更新を確認するかどうかを示す値を取得または設定します。
        /// </summary>
        public bool AutoVersionCheck
        {
            get => settings.AutoVersionCheck;
            set
            {
                settings.AutoVersionCheck = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 開発版の更新も確認するかどうかを示す値を取得または設定します。
        /// </summary>
        public bool AcceptsPrerelease
        {
            get => settings.AcceptsPrerelease;
            set
            {
                settings.AcceptsPrerelease = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// カード価格を scryfall.com に問い合わせるかどうかを示す値を取得または設定します。
        /// </summary>
        public bool GetCardPrice
        {
            get => settings.GetCardPrice;
            set
            {
                settings.GetCardPrice = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Penny Dreadful のカードリストを取得するかどうかを示す値を取得または設定します。
        /// </summary>
        public bool GetPDList
        {
            get => settings.GetPDList;
            set
            {
                settings.GetPDList = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 表示中のカードのコレクションを取得します。
        /// </summary>
        public ObservableCollection<Card> Cards { get; } = new ObservableCollection<Card>();

        /// <summary>
        /// <see cref="System.Windows.Controls.TabControl"/> で手前に表示しているカードのインデックス番号を取得または設定します。
        /// </summary>
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                selectedIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanCopyJapaneseName));
                OnPropertyChanged(nameof(CanCopyEnglishName));
                OnPropertyChanged(nameof(CanBrowseWiki));
            }
        }

        /// <summary>
        /// <see cref="System.Windows.Controls.TabControl"/> で手前に表示しているカードを取得します。
        /// </summary>
        public Card SelectedCard
        {
            get
            {
                if (SelectedIndex >= 0 && SelectedIndex < Cards.Count)
                {
                    var card = Cards[SelectedIndex];

                    if (!string.IsNullOrEmpty(card?.Name))
                        return card;
                }
                return null;
            }
        }

        /// <summary>
        /// 日本語カード名をコピーできるかどうかを示す値を取得します。
        /// </summary>
        public bool CanCopyJapaneseName => SelectedCard != null && SelectedCard.HasJapaneseName;

        /// <summary>
        /// 英語カード名をコピーできるかどうかを示す値を取得します。
        /// </summary>
        public bool CanCopyEnglishName => SelectedCard != null && SelectedCard.Name != null;

        /// <summary>
        /// MTG Wiki でカードを調べられるかどうかを示す値を取得します。
        /// </summary>
        public bool CanBrowseWiki
        {
            get
            {
                var card = SelectedCard;

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
        }

        /// <summary>
        /// カードテキストの代わりにメッセージを表示します。
        /// </summary>
        public void SetMessage(string text)
        {
            var card = new Card(text);

            if (Cards.Count > 0)
            {
                Cards[0] = card;

                for (int i = Cards.Count - 1; i >= 1; i--)
                    Cards.RemoveAt(i);
            }
            else
                Cards.Add(card);

            SelectedIndex = 0;
        }

        /// <summary>
        /// 指定したカードを表示します。
        /// </summary>
        public void SetCard(Card card)
        {
            if (card == null)
                return;

            if (Cards.Count > 0)
            {
                Cards[0] = card;

                // 項目数が減る場合に末端から削除
                for (int i = Cards.Count - 1; i >= 1; i--)
                    Cards.RemoveAt(i);
            }
            else
                Cards.Add(card);

            if (card.RelatedCardName != null)
            {
                foreach (string relatedName in card.RelatedCardNames)
                {
                    App.Cards.TryGetValue(relatedName, out var card2);
                    Cards.Add(card2);
                }
            }
            SelectedIndex = 0;
        }

        /// <summary>
        /// 画面を更新するように要求します。
        /// </summary>
        public void RefreshTab() => SearchCardName();

        /// <summary>
        /// Preview Pane を自動的に探索するためのタイマーを必要なら設定します。
        /// </summary>
        public void SetRefreshTimer(Dispatcher dispatcher)
        {
            if (timer == null)
            {
                if (AutoRefresh)
                {
                    CapturePreviewPane();
                    timer = new DispatcherTimer(RefreshInterval, DispatcherPriority.Normal, OnCapture, dispatcher);
                }
            }
            else
            {
                timer.IsEnabled = AutoRefresh;
                timer.Interval = RefreshInterval;
            }
        }

        /// <summary>
        /// MO のプレビューウィンドウを探します。
        /// </summary>
        public void CapturePreviewPane() => OnCapture(null, EventArgs.Empty);

        /// <summary>
        /// 各リソースを解放します。
        /// </summary>
        public void Release()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= OnCapture;
                timer = null;
            }
            cacheReq = null;
            condition = null;

            App.CurrentDispatcher.BeginInvoke((Action)ReleaseAutomationElement);
        }

        /// <summary>
        /// UI Automation イベントハンドラーを削除し、<see cref="AutomationElement"/> への参照を解放します。
        /// </summary>
        public void ReleaseAutomationElement()
        {
            if (prevWnd != null)
            {
                prevWnd = null;
                Automation.RemoveAllEventHandlers();
                Debug.WriteLine("Removed automation event handlers.");
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) 
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// MO のプレビューウィンドウを探し、UI テキストの変化イベントが発生するようにします。
        /// </summary>
        private void OnCapture(object sender, EventArgs e)
        {
            var proc = Process.GetProcessesByName("mtgo");

            if (proc.Length == 0)
            {
                ReleaseAutomationElement();
                SetMessage("起動中のプロセスの中に MO が見つかりません。");
                return;
            }

            // "Preview" という名前のウィンドウを探す (なぜかルートの子で見つかる)
            AutomationElement currentPrevWnd = null;
            try
            {
                currentPrevWnd = AutomationElement.RootElement.FindFirst(TreeScope.Children,
                    new AndCondition(
                        new PropertyCondition(AutomationElement.ProcessIdProperty, proc[0].Id),
                        new PropertyCondition(AutomationElement.ClassNameProperty, "Window"),
                        new PropertyCondition(AutomationElement.NameProperty, "Preview")));
            }
            catch { Debug.WriteLine("Preview Pane の取得に失敗しました。"); }

            if (currentPrevWnd == null)
            {
                ReleaseAutomationElement();
                SetMessage("MO の Preview Pane が見つかりません。");
                return;
            }

            if (currentPrevWnd != prevWnd)
            {
                // 新しい Preview Pane が見つかった
                ReleaseAutomationElement();
                prevWnd = currentPrevWnd;

                // とりあえずカード名を探す
                SearchCardName();

                // UI テキストの変化を追う
                // 対戦中、カード情報は Preview Pane の直接の子ではなく、
                // ZoomCard_View というカスタムコントロールの下にくるので、スコープは子孫にしないとダメ
                using (cacheReq.Activate())
                    Automation.AddAutomationPropertyChangedEventHandler(
                        prevWnd, TreeScope.Descendants, OnAutomaionNamePropertyChanged, AutomationElement.NameProperty);

                if (SelectedCard == null)
                    SetMessage("準備完了");
            }
        }

        /// <summary>
        /// 変更された要素名がカード名かどうかを調べます。
        /// </summary>
        /// <remarks>AutomationPropertyChangedEventHandler は UI スレッドとは別スレッドで動いている</remarks>
        private void OnAutomaionNamePropertyChanged(object sender, AutomationPropertyChangedEventArgs e)
        {
            if (prevWnd == null)
                return;

            string name = GetNamePropertyValue(sender as AutomationElement);
            //Debug.WriteLineIf(!string.IsNullOrWhiteSpace(name), name);

            // キャッシュ無効化時
            if (name == null)
                return;

            // 新しいテキストがカード名かどうかを調べ、そうでないなら不必要な全体検索をしないようにする
            // トークンの場合は、カード名を含むとき (= コピートークン) と含まないとき (→ 空表示にする) とがあるので検索を続行する
            if (!App.Cards.ContainsKey(name) && !name.StartsWith("Token"))
            {
                string cardType = null;
                const string triggerPrefix = "Triggered ability from ";

                // 統治者以外の紋章やテキストレスのヴァンガードの場合は、確定で空表示にする
                if (name.StartsWith("Emblem") && name != "Emblem - ")
                    cardType = "紋章";
                else if (name.StartsWith("Avatar - "))
                    cardType = "ヴァンガード";
                else if (name.StartsWith(triggerPrefix))
                {
                    // スタックにのった英雄譚からの誘発型能力
                    name = name.Substring(triggerPrefix.Length);

                    if (App.Cards.TryGetValue(name, out var card))
                    {
                        App.CurrentDispatcher.Invoke(() => SetCard(card));
                        return;
                    }
                }
                else if (Card.GetSagaNameByArtist(name, out string saga))
                {
                    // 英雄譚はカード名を Automation で探せないため、アーティスト名で 1:1 対応で探す
                    if (CheckCardTypeForSaga(saga))
                        return;
                }

                if (cardType != null)
                    App.CurrentDispatcher.Invoke(() => SetMessage(cardType));

                return;
            }
            Debug.WriteLine(name);

            SearchCardName();
        }

        /// <summary>
        /// 現在の Preview Pane 内のテキストからカード名を取得し、表示します。
        /// </summary>
        private void SearchCardName()
        {
            AutomationElementCollection elements;
            using (cacheReq.Activate())
                elements = prevWnd?.FindAll(TreeScope.Descendants, condition);

            if (elements == null)
                return;

            // 一連のテキストからカード名を探す (合体カードなど複数のカード名にヒットする場合があるので一通り探し直す必要がある)
            var foundCards = new List<Card>();
            bool isToken = false;

            foreach (AutomationElement element in elements)
            {
                string name = GetNamePropertyValue(element);

                // キャッシュ無効化時
                if (name == null)
                    return;

                // 英雄譚の絵師かもしれない場合
                if (Card.GetSagaNameByArtist(name, out string saga))
                {
                    if (CheckCardTypeForSaga(saga))
                        return;
                }

                // WHISPER データベースからカード情報を取得
                if (App.Cards.TryGetValue(name, out var card))
                {
                    if (!foundCards.Contains(card))
                    {
                        foundCards.Add(card);

                        // 両面カードの場合に、Preview Pane に片面だけ表示されていても、もう一方の面を表示するようにする
                        if (card.RelatedCardName != null)
                        {
                            foreach (string relatedName in card.RelatedCardNames)
                            {
                                App.Cards.TryGetValue(relatedName, out var card2);

                                if (!foundCards.Contains(card2))
                                    foundCards.Add(card2);
                            }
                        }
                    }
                }
                else if (!isToken && name.StartsWith("Token"))
                    isToken = true;
            }

            // 設定によっては基本土地 5 種の場合に表示を変えないようにする
            if (!ShowBasicLands && foundCards.Count == 1)
            {
                switch (foundCards[0].Name)
                {
                    case "Plains":
                    case "Island":
                    case "Swamp":
                    case "Mountain":
                    case "Forest":
                        return;
                }
            }

            // 汎用のトークン
            if (foundCards.Count == 0 && isToken)
            {
                App.CurrentDispatcher.Invoke(() => SetMessage("トークン"));
                return;
            }

            App.CurrentDispatcher.Invoke(() =>
            {
                int j = 0;

                for (int i = 0; i < Cards.Count && j < foundCards.Count; i++, j++)
                    Cards[i] = foundCards[j];

                // 項目数が減る場合：末端から削除
                for (int i = Cards.Count - 1; i >= foundCards.Count; i--)
                    Cards.RemoveAt(i);

                // 項目数が増える場合：継続して追加
                for (; j < foundCards.Count; j++)
                    Cards.Add(foundCards[j]);

                SelectedIndex = 0;
            });
        }

        /// <summary>
        /// 現在のカードのカードタイプが英雄譚であるかどうかをチェックし、そうであるなら、指定したカード名のカードを表示します。
        /// </summary>
        private bool CheckCardTypeForSaga(string cardName)
        {
            if (App.Cards.TryGetValue(cardName, out var foundCard))
            {
                // 英雄譚かどうかをチェックする
                AutomationElement element = null;
                using (cacheReq.Activate())
                {
                    element = prevWnd?.FindFirst(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.AutomationIdProperty, "CardType"));
                }

                string cardType = GetNamePropertyValue(element);

                if (cardType != null && cardType.EndsWith("Saga"))
                {
                    App.CurrentDispatcher.Invoke(() => SetCard(foundCard));
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// UI テキストからカード名の候補となる文字列を取得します。
        /// </summary>
        private static string GetNamePropertyValue(AutomationElement element)
        {
            string name = null;
            try
            {
                name = element?.Cached.Name;
            }
            catch
            {
                Debug.WriteLine("キャッシュされた Name プロパティ値の取得に失敗しました。");
                return null;
            }

            if (name == null)
                return string.Empty;

            // 特殊文字を置き換える (アキュート・アクセントつきの文字など)
            return Card.NormalizeName(name);
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
