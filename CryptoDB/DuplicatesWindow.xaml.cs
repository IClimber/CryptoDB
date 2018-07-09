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
using System.Drawing;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ImageConverter;
using System.ComponentModel;

namespace CryptoDataBase
{
	/// <summary>
	/// Interaction logic for DuplicatesWindow.xaml
	/// </summary>
	public partial class DuplicatesWindow : Window
	{
		private DuplicatesWindow()
		{
			InitializeComponent();
		}

		public DuplicatesWindow(Bitmap origFileThumbnail, string origFileName, List<Element> duplicatesList) : this()
		{
			OrigImage.Source = ImgConverter.BitmapToImageSource(origFileThumbnail);
			OrigFileName.Text = origFileName;

			listView.ItemsSource = new BindingList<Element>(duplicatesList.OrderByDescending(x => x.Type).ToList());
		}

		private void listView_MouseDown(object sender, MouseButtonEventArgs e)
		{
			listView.SelectedItem = null;
			listView.Focus();
		}

		private void CheckBox_Click(object sender, RoutedEventArgs e)
		{
			(Owner as MainWindow).ShowDuplicateMessage = (sender as CheckBox).IsChecked != true;
		}

		private void ShowPropertiesWindow(List<Element> elements)
		{
			PropertiesWindow property = new PropertiesWindow(elements);
			property.Owner = this;
			property.Show();
		}

		private void listView_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if ((Keyboard.Modifiers == ModifierKeys.Control) && (e.Key == Key.Enter))
			{
				e.Handled = true;
				ShowPropertiesWindow(listView.SelectedItems.Cast<Element>().ToList());
				return;
			}
		}

		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				Close();
			}
		}
	}
}
