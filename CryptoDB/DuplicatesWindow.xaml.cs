using CryptoDataBase.CryptoContainer.Models;
using ImageConverter;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
