using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Automation;
using System.Windows.Threading;

namespace Mojp
{
	/// <summary>
	/// <see cref="MainWindow"/> のビュー モデルを提供します。
	/// </summary>
	public class MainViewModel : INotifyPropertyChanged
	{
		private string fontFamily = Settings.Default.CardTextFontFamily;
		private int fontSize = Settings.Default.CardTextFontSize;
		private bool topMost = Settings.Default.TopMost;
		private double width = Settings.Default.WindowWidth;
		private double height = Settings.Default.WindowHeight;
		private double left = Settings.Default.WindowLeft;
		private double top = Settings.Default.WindowTop;
		private bool showBasicLands = Settings.Default.ShowBasicLands;
		private bool autoRefresh = Settings.Default.AutoRefresh;
		private TimeSpan refreshInterval = Settings.Default.RefreshInterval;

		private ObservableCollection<Card> cards = new ObservableCollection<Card>();
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
			get { return fontFamily; }
			set
			{
				fontFamily = value;
				OnPropertyChanged();
				Settings.Default.CardTextFontFamily = value;
			}
		}

		/// <summary>
		/// このアプリケーションで使用するフォントサイズを取得または設定します。
		/// </summary>
		public int FontSize
		{
			get { return fontSize; }
			set
			{
				fontSize = value;
				OnPropertyChanged();
				Settings.Default.CardTextFontSize = value;
			}
		}

		/// <summary>
		/// このアプリケーションを常に手前に表示するかどうかを示す値を取得または設定します。
		/// </summary>
		public bool TopMost
		{
			get { return topMost; }
			set
			{
				topMost = value;
				OnPropertyChanged();
				Settings.Default.TopMost = value;
			}
		}

		/// <summary>
		/// このアプリケーションのウィンドウの幅を取得または設定します。
		/// </summary>
		public double WindowWidth
		{
			get { return width; }
			set
			{
				width = value;
				OnPropertyChanged();
				Settings.Default.WindowWidth = value;
			}
		}

		/// <summary>
		/// このアプリケーションのウィンドウの高さを取得または設定します。
		/// </summary>
		public double WindowHeight
		{
			get { return height; }
			set
			{
				height = value;
				OnPropertyChanged();
				Settings.Default.WindowHeight = value;
			}
		}

		/// <summary>
		/// このアプリケーションのウィンドウの左の表示位置を取得または設定します。
		/// </summary>
		public double WindowLeft
		{
			get { return left; }
			set
			{
				left = value;
				OnPropertyChanged();
				Settings.Default.WindowLeft = value;
			}
		}

		/// <summary>
		/// このアプリケーションのウィンドウの上の表示位置を取得または設定します。
		/// </summary>
		public double WindowTop
		{
			get { return top; }
			set
			{
				top = value;
				OnPropertyChanged();
				Settings.Default.WindowTop = value;
			}
		}

		/// <summary>
		/// 基本土地に反応するかどうかを示す値を取得または設定します。
		/// </summary>
		public bool ShowBasicLands
		{
			get { return showBasicLands; }
			set
			{
				showBasicLands = value;
				OnPropertyChanged();
				Settings.Default.ShowBasicLands = value;
			}
		}

		/// <summary>
		/// Preview Page の探索を自動化するかどうかの値を取得または設定します。
		/// </summary>
		public bool AutoRefresh
		{
			get { return autoRefresh; }
			set
			{
				autoRefresh = value;
				OnPropertyChanged();
				Settings.Default.AutoRefresh = value;
			}
		}

		/// <summary>
		/// Preview Page の探索を行う間隔を取得します。
		/// </summary>
		public TimeSpan RefreshInterval => refreshInterval;

		/// <summary>
		/// Preview Page の探索を行う間隔をミリ秒単位で取得または設定します。
		/// </summary>
		public int RefreshIntervalMilliseconds
		{
			get { return (int)refreshInterval.TotalMilliseconds; }
			set
			{
				if (value <= 0)
					value = 1;

				refreshInterval = TimeSpan.FromMilliseconds(value);
				OnPropertyChanged();
				Settings.Default.RefreshInterval = refreshInterval;
			}
		}

		/// <summary>
		/// 表示中のカードのコレクションを取得します。
		/// </summary>
		public ObservableCollection<Card> Cards => cards;

		/// <summary>
		/// <see cref="System.Windows.Controls.TabControl"/> で手前に表示しているカードのインデックス番号を取得または設定します。
		/// </summary>
		public int SelectedIndex
		{
			get { return selectedIndex; }
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

				if (card != null)
				{
					if (card.WikiLink != null)
						return true;

					// トークンで該当するページとなると、クリーチャータイプの解説ページがあるが、ややこしいパターンもあるのでリンクを無効にする
					if (!card.Type.StartsWith("トークン"))
						return true;
				}
				return false;
			}
		}

		/// <summary>
		/// カードテキストの代わりにメッセージを表示します。
		/// </summary>
		public void SetMessage(string text)
		{
			var card = new Card { Text = text };

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
			ReleaseAutomationElement();
			cacheReq = null;
			condition = null;

			if (timer != null)
			{
				timer.Stop();
				timer.Tick -= OnCapture;
				timer = null;
			}
		}

		/// <summary>
		/// UI Automation イベントハンドラーを削除し、<see cref="AutomationElement"/> への参照を解放します。
		/// </summary>
		public void ReleaseAutomationElement()
		{
			if (prevWnd != null)
			{
				Automation.RemoveAllEventHandlers();
				prevWnd = null;
			}
		}

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

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
			catch { }

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
				// 対戦中、カード情報は Preview Pane の直接の子ではなく、ZoomCard_View というカスタムコントロールの下にくるので、スコープは子孫にしないとダメ
				using (cacheReq.Activate())
					Automation.AddAutomationPropertyChangedEventHandler(prevWnd, TreeScope.Descendants, OnAutomaionNamePropertyChanged, AutomationElement.NameProperty);

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

			// 新しいテキストがカード名かどうかを調べ、そうでないなら不必要な検索をしないようにする
			// トークンの場合は、カード名を含むとき (= コピートークン) と含まないとき (→ 空表示にする) とがあるので検索を続行する
			if (!App.Cards.ContainsKey(name) && !name.StartsWith("Token"))
			{
				string cardType = null;

				// 紋章やヴァンガードの場合は確定で空表示にする
				if (name.StartsWith("Emblem"))
					cardType = "紋章";
				else if (name == "Vanguard")
					cardType = "ヴァンガード";

				if (cardType != null)
					App.Current.Dispatcher.Invoke(() => SetMessage(cardType));

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

			// 一連のテキストからカード名を探す (両面カードなど複数のカード名にヒットする場合があるので一通り探し直す必要がある)
			var foundCards = new List<Card>();

			foreach (AutomationElement element in elements)
			{
				string name = GetNamePropertyValue(element);

				// WHISPER データベースからカード情報を取得
				Card card;
				if (name != null && App.Cards.TryGetValue(name, out card) && !foundCards.Contains(card))
				{
					foundCards.Add(card);

					// 両面カードの場合に、Preview Pane に片面だけ表示されていても、もう一方の面を表示するようにする
					if (card.RelatedCardName != null)
					{
						var card2 = App.Cards[card.RelatedCardName];

						if (!foundCards.Contains(card2))
							foundCards.Add(card2);
					}
				}
			}

			// 設定によっては基本土地 5 種の場合は表示を変えないようにする
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

			// ツールバーと重ならないようにするためのダミー項目
			foundCards.Add(Card.Empty);

			App.Current.Dispatcher.Invoke(() =>
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
		/// UI テキストからカード名の候補となる文字列を取得します。
		/// </summary>
		private static string GetNamePropertyValue(AutomationElement element)
		{
			string name = null;
			try
			{
				name = element?.Cached.Name;
			}
			catch { Debug.WriteLine("Exception in calling GetCurrentPropertyValue method."); }

			if (name == null)
				return string.Empty;

			// 特殊文字を置き換える
			var sb = new StringBuilder(name.Length + 1);
			bool replaced = false;

			foreach (char c in name)
			{
				switch (c)
				{
					// Æther Vial など
					//case 'Æ':
					//	sb.Append("AE");
					//	replaced = true;
					//	break;

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

			return name;
		}

		public event PropertyChangedEventHandler PropertyChanged;
	}
}