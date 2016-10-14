using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Mojp
{
	/// <summary>
	/// <see cref="MainWindow"/> のビュー モデルを提供します。
	/// </summary>
	public class MainViewModel : INotifyPropertyChanged
	{
		private string fontFamily = Settings.Default.CardTextFontFamily;
		private int fontSize = Settings.Default.CardTextFontSize;
		private double width = Settings.Default.WindowWidth;
		private double height = Settings.Default.WindowHeight;
		private double left = Settings.Default.WindowLeft;
		private double top = Settings.Default.WindowTop;
		private Card card = new Card() { JapaneseName = string.Empty, Text = "MO の Preview Pane を表示させた状態で、右上のカメラアイコンのボタンを押してください" };
		private string tooltip = null;

		public MainViewModel()
		{
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
		/// 現在表示しているカードを取得または設定します。<see cref="CardToolTip"/> も自動で変更します。
		/// </summary>
		public Card CurrentCard
		{
			get { return card; }
			set
			{
				card = value;
				OnPropertyChanged();
				CardToolTip = card?.Summary;
			}
		}

		/// <summary>
		/// ツールチップに表示するカードの追加情報を取得または設定します。
		/// </summary>
		public string CardToolTip
		{
			get { return tooltip; }
			set
			{
				tooltip = value;
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// カードテキストの代わりにメッセージを表示します。
		/// </summary>
		public void SetMessage(string text)
		{
			CurrentCard = new Card() { Text = text };
			CardToolTip = null;
		}

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public event PropertyChangedEventHandler PropertyChanged;
	}
}