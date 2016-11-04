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
			cacheReq.TreeScope = TreeScope.Element;
			cacheReq.Add(AutomationElement.NameProperty);
			cacheReq.AutomationElementMode = AutomationElementMode.None;

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

					if (card != null && !string.IsNullOrEmpty(card.Name))
						return card;
				}
				return null;
			}
		}

		/// <summary>
		/// カードテキストの代わりにメッセージを表示します。
		/// </summary>
		public void SetMessage(string text)
		{
			Cards.Clear();
			Cards.Add(new Card { Text = text });
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
					timer = new DispatcherTimer(RefreshInterval, DispatcherPriority.Normal, OnCapture, dispatcher);
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
			Debug.WriteLine(nameof(OnCapture) + " " + DateTime.Now);

			var proc = Process.GetProcessesByName("mtgo");

			if (proc.Length == 0)
			{
				ReleaseAutomationElement();
				SetMessage("起動中のプロセスの中に MO が見つかりません。");
				return;
			}

			// "Preview" という名前のウィンドウを探す (なぜかルートの子で見つかる)
			var currentPrevWnd = AutomationElement.RootElement.FindFirst(TreeScope.Children,
				new AndCondition(
					new PropertyCondition(AutomationElement.ProcessIdProperty, proc[0].Id),
					new PropertyCondition(AutomationElement.ClassNameProperty, "Window"),
					new PropertyCondition(AutomationElement.NameProperty, "Preview")));

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

				// UI テキストの変化を追う
				using (cacheReq.Activate())
					Automation.AddAutomationPropertyChangedEventHandler(prevWnd, TreeScope.Descendants, OnAutomaionNamePropertyChanged, AutomationElement.NameProperty);

				SetMessage("準備完了");
			}
		}

		/// <remarks>
		/// AutomationPropertyChangedEventHandler は UI スレッドとは別スレッドで動いている
		/// </remarks>
		private void OnAutomaionNamePropertyChanged(object sender, AutomationPropertyChangedEventArgs e)
		{
			if (prevWnd == null)
				return;
			
			string srcName = GetNamePropertyValue(sender as AutomationElement);
			Debug.WriteLineIf(!string.IsNullOrWhiteSpace(srcName), srcName);

			// 新しいテキストがカード名かどうかを調べ、そうでないなら不必要な検索をしないようにする
			// トークンの場合は、カード名を含むとき (= コピートークン) と含まないとき (→ 空表示にする) とがあるので検索を続行する
			if (!App.Cards.ContainsKey(srcName) && !srcName.StartsWith("Token"))
			{
				string cardType = null;

				// 紋章やヴァンガードの場合は確定で空表示にする
				if (srcName.StartsWith("Emblem"))
					cardType = "紋章";
				else if (srcName == "Vanguard")
					cardType = "ヴァンガード";

				if (cardType != null)
					App.Current.Dispatcher.Invoke(() => SetMessage(cardType));

				return;
			}

			// テキストが空でなく、特定の UI 要素でない TextBlock をすべて拾う
			AutomationElementCollection texts;
			using (cacheReq.Activate())
				texts = prevWnd.FindAll(TreeScope.Descendants, condition);

			// 一連のテキストからカード名を探す (両面カードなど複数のカード名にヒットする場合があるので一通り探し直す必要がある)
			var foundCards = new List<Card>();

			foreach (AutomationElement text in texts)
			{
				string name = GetNamePropertyValue(text);

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

			App.Current.Dispatcher.Invoke(() =>
			{
				Cards.Clear();

				foreach (var card in foundCards)
					Cards.Add(card);

				// ツールバーと重ならないようにするためのダミー項目
				Cards.Add(new Card());
				SelectedIndex = 0;
			});
		}

		/// <summary>
		/// UI テキストからカード名の候補となる文字列を取得します。
		/// </summary>
		private static string GetNamePropertyValue(AutomationElement src)
		{
			string name = null;
			try
			{
				name = src?.Cached.Name;
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