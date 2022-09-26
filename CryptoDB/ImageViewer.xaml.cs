using CryptoDataBase.CryptoContainer.Models;
using ImageConverter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace CryptoDataBase
{
	/// <summary>
	/// Логика взаимодействия для DocSource.xaml
	/// </summary>
	public partial class ImageViewer : Window
	{
		int currentIndex;
		List<Element> elements;
		public bool IsStretch { get { return _isStretch; } set { _isStretch = value; _isStretchGlobal = value; } }
		private bool _isStretch = false;
		private static bool _isStretchGlobal = false;
		private System.Windows.Controls.ListView parentListView;

		public ImageViewer()
		{
			IsStretch = _isStretchGlobal;

			InitializeComponent();

			toolPanel.Visibility = Visibility.Hidden;
			prevImg.Visibility = Visibility.Hidden;
			nextImg.Visibility = Visibility.Hidden;
		}

		public ImageViewer(List<Element> elementList, FileElement current, System.Windows.Controls.ListView ParentListView) : this()
		{
			parentListView = ParentListView;
			elements = elementList;
			currentIndex = elementList.IndexOf(current);
		}

		private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if ((e.Key == Key.Escape) || (e.Key == Key.Back))
			{
				Close();
			}

			if (e.Key == Key.Left)
			{
				ShowPrevImage();
			}

			if (e.Key == Key.Right)
			{
				ShowNextImage();
			}

			if ((Keyboard.Modifiers == ModifierKeys.Control) && (e.Key == Key.S))
			{
				SaveAs_Click(null, null);
			}

			if (e.Key == Key.F2)
			{
				Rename_Click(null, null);
			}

			if (e.Key == Key.Delete)
			{
				Delete_Click(null, null);
			}
		}

		private void ShowNextImage()
		{
			currentIndex++;
			if (currentIndex >= elements.Count)
			{
				currentIndex = 0;
			}
			ShowImage();
		}

		private void ShowPrevImage()
		{
			currentIndex--;
			if (currentIndex < 0)
			{
				currentIndex = elements.Count - 1;
			}
			ShowImage();
		}

		private string FormatingSize(UInt64 Size)
		{
			string[] sizes = { "B", "KB", "MB", "GB", "TB" };
			double len = Size;
			int order = 0;
			while (len >= 1024 && order < sizes.Length - 1)
			{
				order++;
				len = len / 1024;
			}

			return String.Format("{0:0.##} {1}", len, sizes[order]);
		}

		private void ShowImage()
		{
			if ((currentIndex < 0) || (currentIndex >= elements.Count))
			{
				return;
			}

			FileElement currentElement = elements[currentIndex] as FileElement;
			parentListView.SelectedItem = currentElement;
			parentListView.ScrollIntoView(parentListView.SelectedItem);
			Title = currentElement.Name;

			MemoryStream ms = new MemoryStream();
			currentElement.SaveTo(ms);

			BitmapImage bmp = null;

			try
			{
				ms.Position = 0;
				bmp = ImgConverter.StreamToBitmapImage(ms);
				image.SetSource(bmp);
				image.IsStretch = IsStretch;
				//bmp = BitmapFrame.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
				//ImageBehavior.SetAnimatedSource(image, bmp);
				//bmp = null;
			}
			catch
			{

			}

			ms.Dispose();

			TextBlockStatus1.Text = "Image size: " + FormatingSize(currentElement.Size);
			TextBlockStatus2.Text = "Image resolution: " + bmp?.PixelWidth + " x " + bmp?.PixelHeight;
			TextBlockStatus3.Text = "DPI: X=" + (int?)bmp?.DpiX + "  Y=" + (int?)bmp?.DpiY;
			TextBlockStatus4.Text = (currentIndex + 1).ToString() + @" / " + elements.Count.ToString();
		}

		private void Rotate_Click(object sender, RoutedEventArgs e)
		{
			double angle = int.Parse((string)((System.Windows.Controls.Button)sender).Tag);
			if (angle == 90)
			{
				image.Rotate90();
			}

			if (angle == 270)
			{
				image.Rotate270();
			}
		}

		private void grid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
		{
			toolPanel.Visibility = (e.GetPosition(sender as IInputElement).Y < 50) ? Visibility.Visible : Visibility.Hidden;

			prevImg.Visibility = (e.GetPosition(sender as IInputElement).X <= prevImgColumn.Width.Value) ? Visibility.Visible : Visibility.Hidden;
			nextImg.Visibility = ((grid.RenderSize.Width - e.GetPosition(sender as IInputElement).X) <= prevImgColumn.Width.Value) ? Visibility.Visible : Visibility.Hidden;
		}

		private void Image_MouseDown(object sender, MouseButtonEventArgs e)
		{
			byte direct = (sender is Border) ? Convert.ToByte((sender as Border).Tag) : Convert.ToByte((sender as Image).Tag);

			switch (direct)
			{
				case 0: ShowPrevImage(); break;
				case 1: ShowNextImage(); break;
			}
		}

		private void SaveAs_Click(object sender, RoutedEventArgs e)
		{
			SaveFileDialog sd = new SaveFileDialog();
			sd.FileName = elements[currentIndex].Name;
			sd.DefaultExt = Path.GetExtension(elements[currentIndex].Name);
			sd.Filter = "Image (*." + sd.DefaultExt.ToUpper() + ")|*." + sd.DefaultExt.ToUpper();
			if (sd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
			{
				return;
			}

			elements[currentIndex].SaveAs(sd.FileName);
		}

		private void Rename_Click(object sender, RoutedEventArgs e) //провіряти чи MainWindow editable == true
		{
			if ((Owner as MainWindow).IsReadOnly)
			{
				return;
			}

			RenameWindow renamer = new RenameWindow(elements[currentIndex].Name, elements[currentIndex].Type) { Owner = this };
			if (renamer.ShowDialog() == true)
			{
				try
				{
					elements[currentIndex].Name = renamer.textBox.Text;
				}
				catch (Exception ex)
				{
					System.Windows.MessageBox.Show(ex.Message);
					return;
				}

				Title = elements[currentIndex].Name;
			}
		}

		private void Delete_Click(object sender, RoutedEventArgs e)
		{
			if ((Owner as MainWindow).IsReadOnly)
			{
				return;
			}

			if (System.Windows.MessageBox.Show(this, "Удалить елемент", "Удаление", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
			{
				return;
			}

			if (Owner is MainWindow)
			{
				if ((Owner as MainWindow).DeleteElement(elements[currentIndex]))
				{
					elements.RemoveAt(currentIndex);
					currentIndex--;
					ShowNextImage();
				}

				if ((currentIndex < 0) || (currentIndex >= elements.Count))
				{
					Close();
				}
			}
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			GC.Collect();
		}

		private void IsStretchCheckBox_Click(object sender, RoutedEventArgs e)
		{
			image.IsStretch = IsStretch;
		}

		private void imageViewer_Loaded(object sender, RoutedEventArgs e)
		{
			ShowImage();
		}
	}
}
