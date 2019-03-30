using CryptoDataBase.CDB;
using ImageConverter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfAnimatedGif;

namespace CryptoDataBase
{
	/// <summary>
	/// Логика взаимодействия для DocSource.xaml
	/// </summary>
	public partial class ImageViewer : Window
	{
		int currentIndex;
		List<Element> elements;
		private Point origin;  // Original Offset of image
		private Point start;   // Original Position of the mouse
		private double Zoom = 1;
		private System.Windows.Controls.ListView parentListView;
		BitmapImage bmp = null;

		public ImageViewer()
		{
			InitializeComponent();

			MouseWheel += MainWindow_MouseWheel;
			image.MouseLeftButtonDown += image_MouseLeftButtonDown;
			image.MouseLeftButtonUp += image_MouseLeftButtonUp;
			image.MouseMove += image_MouseMove;

			toolPanel.Visibility = Visibility.Hidden;
			prevImg.Visibility = Visibility.Hidden;
			nextImg.Visibility = Visibility.Hidden;
		}

		public ImageViewer(List<Element> elementList, FileElement current, System.Windows.Controls.ListView ParentListView) : this()
		{
			parentListView = ParentListView;
			elements = elementList;
			currentIndex = elementList.IndexOf(current);
			ShowImage();
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

			parentListView.SelectedItem = elements[currentIndex];
			parentListView.ScrollIntoView(parentListView.SelectedItem);
			Title = elements[currentIndex].Name;

			MemoryStream ms = new MemoryStream();
			(elements[currentIndex] as FileElement).SaveTo(ms);
			//image.SnapsToDevicePixels = false;
			image.RenderTransform = new ScaleTransform();
			Zoom = 1;
			//image.Source = img;
			//BitmapFrame bmp = null;

			bmp = null;
			
			try
			{
				ms.Position = 0;
				bmp = ImgConverter.StreamToBitmapImage(ms);
				image.Source = bmp;
				//bmp = BitmapFrame.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
				//ImageBehavior.SetAnimatedSource(image, bmp);
				//bmp = null;
			}
			catch
			{

			}

			ms.Dispose();

			TextBlockStatus1.Text = "Image size: " + FormatingSize(elements[currentIndex].Size);
			TextBlockStatus2.Text = "Image resolution: " + bmp?.PixelWidth + " x " + bmp?.PixelHeight;
			TextBlockStatus3.Text = "DPI: X=" + (int?)bmp?.DpiX + "  Y=" + (int?)bmp?.DpiY;
			TextBlockStatus4.Text = (currentIndex + 1).ToString() + @" / " + elements.Count.ToString();


			try
			{
				if (((bmp.Width * (bmp.DpiX / 96)) > border.RenderSize.Width) || ((bmp.Height * (bmp.DpiY / 96)) > border.RenderSize.Height) || (border.Width == 0) || (bmp.DpiX == 0) || (bmp.DpiY == 0))
				{
					image.Stretch = Stretch.Uniform;
				}
				else
				{
					image.Stretch = Stretch.None;
					image.RenderTransform = new ScaleTransform(bmp.DpiX / 96, bmp.DpiY / 96, bmp.Width / 2, bmp.Height / 2);
				}
			}
			catch
			{ }

			GC.Collect();
		}

		private void image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (image.IsMouseCaptured)
			{
				return;
			}
			image.CaptureMouse();


			start = e.GetPosition(border);
			origin.X = image.RenderTransform.Value.OffsetX;
			origin.Y = image.RenderTransform.Value.OffsetY;
		}

		private void image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			image.ReleaseMouseCapture();
		}

		private void SetOffset(ref double OffsetX, ref double OffsetY)
		{
			double minOffsetX = OffsetX;
			double maxOffsetX = OffsetX;

			if ((image.RenderSize.Width * Zoom) > border.RenderSize.Width)
			{
				minOffsetX = -((image.RenderSize.Width * Zoom - border.RenderSize.Width) + (border.RenderSize.Width - image.RenderSize.Width) / 2);
				maxOffsetX = -((border.RenderSize.Width - image.RenderSize.Width) / 2);
			}
			else
			{
				minOffsetX = -(image.RenderSize.Width * Zoom - image.RenderSize.Width) / 2;
				maxOffsetX = -(image.RenderSize.Width * Zoom - image.RenderSize.Width) / 2;
			}
			OffsetX = OffsetX < minOffsetX ? minOffsetX : OffsetX;
			OffsetX = OffsetX > maxOffsetX ? maxOffsetX : OffsetX;

			double minOffsetY = OffsetY;
			double maxOffsetY = OffsetY;
			if ((image.RenderSize.Height * Zoom) > border.RenderSize.Height)
			{
				minOffsetY = -((image.RenderSize.Height * Zoom - border.RenderSize.Height) + (border.RenderSize.Height - image.RenderSize.Height) / 2);
				maxOffsetY = -((border.RenderSize.Height - image.RenderSize.Height) / 2);
			}
			else
			{
				minOffsetY = -(image.RenderSize.Height * Zoom - image.RenderSize.Height) / 2;
				maxOffsetY = -(image.RenderSize.Height * Zoom - image.RenderSize.Height) / 2;
			}

			OffsetY = OffsetY < minOffsetY ? minOffsetY : OffsetY;
			OffsetY = OffsetY > maxOffsetY ? maxOffsetY : OffsetY;
		}

		private void image_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
		{
			if (!image.IsMouseCaptured) return;
			Point p = e.MouseDevice.GetPosition(border);

			Matrix m = image.RenderTransform.Value;
			m.OffsetX = origin.X + (p.X - start.X);
			m.OffsetY = origin.Y + (p.Y - start.Y);

			double newOffsetX = m.OffsetX, newOffsetY = m.OffsetY;
			SetOffset(ref newOffsetX, ref newOffsetY);
			m.OffsetX = newOffsetX;
			m.OffsetY = newOffsetY;

			image.RenderTransform = new MatrixTransform(m);
			//Title = m.OffsetX.ToString() + "   " + m.OffsetY.ToString();
		}

		private void MainWindow_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			Point p = e.MouseDevice.GetPosition(image);

			Matrix m = image.RenderTransform.Value;
			if (e.Delta > 0)
			{
				if ((Zoom * 1.1) <= 10)
				{
					Zoom *= 1.1;
					m.ScaleAtPrepend(1.1, 1.1, p.X, p.Y);
				}
			}
			else
			{
				if ((Zoom / 1.1) >= 1)
				{
					Zoom /= 1.1;
					m.ScaleAtPrepend(1 / 1.1, 1 / 1.1, p.X, p.Y);
				}
				else
				{
					Zoom = 1;
					image.RenderTransform = new ScaleTransform();
					return;
				}
			}

			double newOffsetX = m.OffsetX, newOffsetY = m.OffsetY;
			SetOffset(ref newOffsetX, ref newOffsetY);
			m.OffsetX = newOffsetX;
			m.OffsetY = newOffsetY;

			image.RenderTransform = new MatrixTransform(m);
			//Title = m.OffsetX.ToString() + "   " + m.OffsetY.ToString();
		}

		private void grid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
		{
			toolPanel.Visibility = (e.GetPosition(sender as IInputElement).Y < 50) ? Visibility.Visible : Visibility.Hidden;

			prevImg.Visibility = (e.GetPosition(sender as IInputElement).X <= prevImgColumn.Width.Value) ? Visibility.Visible : Visibility.Hidden;
			nextImg.Visibility = ((grid.RenderSize.Width - e.GetPosition(sender as IInputElement).X) <= prevImgColumn.Width.Value) ? Visibility.Visible : Visibility.Hidden;
		}

		private void Image_MouseDown(object sender, MouseButtonEventArgs e)
		{
			byte direct = ((sender is Border) ? Convert.ToByte((sender as Border).Tag) : Convert.ToByte((sender as Image).Tag));

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
			sd.DefaultExt = System.IO.Path.GetExtension(elements[currentIndex].Name);
			sd.Filter = "Image (*." + sd.DefaultExt.ToUpper() + ")|*." + sd.DefaultExt.ToUpper();
			if (sd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
			{
				return;
			}

			elements[currentIndex].SaveAs(sd.FileName);
		}

		private void Rename_Click(object sender, RoutedEventArgs e) //провіряти чи MainWindow editable == true
		{
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

		private void Rotate_Click(object sender, RoutedEventArgs e)
		{
			/*Matrix m = image.RenderTransform.Value;
			m.RotateAt(270, m.OffsetX + image.RenderSize.Width / 2, m.OffsetY + image.RenderSize.Height / 2);
			Title = image.RenderSize.Width.ToString();
			image.RenderTransform = new MatrixTransform(m);*/
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			bmp = null;

			GC.Collect();
		}
	}
}
