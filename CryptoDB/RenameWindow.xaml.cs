using CryptoDataBase.CryptoContainer.Types;
using System.Windows;
using System.Windows.Input;

namespace CryptoDataBase
{
    /// <summary>
    /// Interaction logic for RenameWindow.xaml
    /// </summary>
    public partial class RenameWindow : Window
	{
		public RenameWindow()
		{
			InitializeComponent();
			textBox.Focus();
		}

		public RenameWindow(string oldName, ElementType type) : this()
		{
			textBox.Text = oldName;

			int length = type == ElementType.File ? oldName.LastIndexOf('.') : oldName.Length;
			textBox.Select(0, length < 0 ? 0 : length);
		}

		private void button1_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			Close();
		}

		private void textBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				button_Click(null, null);
			}
		}

		private void button_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		private void textBox_KeyDown(object sender, KeyEventArgs e)
		{
			if ((e.Key == Key.Enter) && (sender == textBox))
			{
				button1_Click(null, null);
			}
		}
	}
}
