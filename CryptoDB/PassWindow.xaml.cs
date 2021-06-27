using CryptoDataBase.CDB;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace CryptoDataBase
{
	/// <summary>
	/// Interaction logic for PassWindow.xaml
	/// </summary>
	public partial class PassWindow : Window
	{
		public string Password { get { return _Password; } }
		private string _Password;

		public PassWindow()
		{
			InitializeComponent();

			passwordBox.Focus();
		}

		private void buttonOk_Click(object sender, RoutedEventArgs e)
		{
			_Password = passwordBox.Password;
			DialogResult = true;
			Close();
		}

		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				buttonCancel_Click(null, null);
			}
		}

		private void passwordBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				buttonOk_Click(null, null);
			}
		}

		private void buttonCancel_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		private void KeyFile_Drop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				string[] files = ((string[])e.Data.GetData(DataFormats.FileDrop));
				if (files.Count() > 0)
				{
					byte[] pass = Crypto.GetFileSHA256(files[0]);
					if (pass != null)
					{
						_Password = Encoding.UTF8.GetString(pass);
						DialogResult = true;
						Close();
					}
				}
			}
		}
	}
}