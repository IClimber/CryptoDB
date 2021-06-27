using System.Windows;

namespace CryptoDataBase
{
	/// <summary>
	/// Interaction logic for Message.xaml
	/// </summary>
	public partial class Message : Window
	{
		private string _message;

		public Message()
		{
			InitializeComponent();
		}

		public Message(string message) : this()
		{
			_message = message;
		}

		public static void Show(string message)
		{
			Message msgWindow = new Message(message);
			//msgWindow.Owner = this;
			msgWindow.Show();
		}
	}
}
