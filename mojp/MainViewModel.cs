using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Mojp
{
	public class MainViewModel : INotifyPropertyChanged
	{
		private string fontFamily = Settings.Default.CardTextFontFamily;
		private int fontSize = Settings.Default.CardTextFontSize;
		private double width = Settings.Default.WindowWidth;
		private double height = Settings.Default.WindowHeight;
		private double left = Settings.Default.WindowLeft;
		private double top = Settings.Default.WindowTop;
		private Card card = new Card() { JapaneseName = "島", Text = "(T): あなたのマナ・プールに(青)を加える。" };
		private string tooltip = null;

		public MainViewModel()
		{
		}

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

		public string CardToolTip
		{
			get { return tooltip; }
			set
			{
				tooltip = value;
				OnPropertyChanged();
			}
		}

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