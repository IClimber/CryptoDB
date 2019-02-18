using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
