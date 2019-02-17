using CryptoDataBase.CDB;
using ImageConverter;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Input;

namespace CryptoDataBase
{
	/// <summary>
	/// Interaction logic for Finder.xaml
	/// </summary>
	public partial class Finder : Window
	{
		public delegate void CallBackResult(List<Element> result);

		DirElement root;
		Bitmap bitmap;
		private CallBackResult resultCallback;

		public Finder()
		{
			InitializeComponent();
		}

		public Finder(DirElement Root) : this()
		{
			root = Root;
		}

		public Finder(DirElement Root, Bitmap thumbnail, CallBackResult Result) : this()
		{
			root = Root;
			resultCallback = Result;
			SetThumbnail(thumbnail);
		}

		private void SetThumbnail(Bitmap thumbnail)
		{
			image.Source = ImgConverter.BitmapToImageSource(thumbnail);
			bitmap?.Dispose();
			bitmap = thumbnail;
			slider.Focus();
		}

		public Finder(DirElement Root, CallBackResult Result) : this()
		{
			root = Root;
			image.Source = null;
			bitmap?.Dispose();
			resultCallback = Result;
			textBox.Focus();
		}

		private void ButtonFind_Click(object sender, RoutedEventArgs e)
		{
			if (image.Source != null)
			{
				resultCallback(root.FindAllByIcon(bitmap, (byte)slider.Value));
			}
			else
			{
				resultCallback(root.FindByName(textBox.Text, findAsTag.IsChecked == true, allTags.IsChecked == true));
			}
		}

		private void Button_Click_1(object sender, RoutedEventArgs e)
		{
			bitmap?.Dispose();
			image.Source = null;
		}

		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if ((Keyboard.Modifiers == ModifierKeys.Control) && (e.Key == Key.V))
			{
				if (Clipboard.ContainsImage())
				{
					Bitmap tmp = ImgConverter.BitmapFromSource(Clipboard.GetImage());
					SetThumbnail(ImgConverter.ResizeImage(tmp, MainWindow.thumbnailSize));
					tmp?.Dispose();
				}
			}
		}

		private void Window_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				if (sender == buttonClear)
				{
					return;
				}
				else
				{
					ButtonFind_Click(null, null);
				}
			}

			if (e.Key == Key.Escape)
			{
				if (textBox.Text == "")
				{
					Close();
				}
				else
				{
					textBox.Clear();
				}
			}
		}

		private void Window_Drop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

				if ((files.Length > 0) && (MainWindow.IsImage(files[0])))
				{
					Bitmap tmp = new Bitmap(files[0]);
					SetThumbnail(ImgConverter.ResizeImage(tmp, MainWindow.thumbnailSize));
					tmp?.Dispose();
				}
			}
		}
	}
}
