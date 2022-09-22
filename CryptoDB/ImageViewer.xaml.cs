using CryptoDataBase.CryptoContainer.Models;
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

namespace CryptoDataBase
{
	/// <summary>
	/// Логика взаимодействия для DocSource.xaml
	/// </summary>
	public partial class ImageViewer : Window
	{
		public const int STANDART_DPI = 96;

		int currentIndex;
		List<Element> elements;
		private Point origin;  // Original Offset of image
		private Point start;   // Original Position of the mouse
		private int rotate = 0;
		private double Zoom = 1;
		private double ZoomStep = 1.1;
		private Transform originalTransform;
		private readonly int Dpi = STANDART_DPI;
		public bool IsStretch { get { return _isStretch; } set { _isStretch = value; _isStretchGlobal = value; } }
		private bool _isStretch = false;
		private static bool _isStretchGlobal = false;

		private System.Windows.Controls.ListView parentListView;
		BitmapImage bmp = null;

		public ImageViewer()
		{
			IsStretch = _isStretchGlobal;

			InitializeComponent();
			Dpi = (int)(STANDART_DPI * (Screen.PrimaryScreen.Bounds.Width / SystemParameters.PrimaryScreenWidth));

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
			image.Width = 0;
			image.Height = 0;
			image.RenderTransform = new ScaleTransform();
			rotate = 0;
			image.Stretch = Stretch.None;
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

			TextBlockStatus1.Text = "Image size: " + FormatingSize(currentElement.Size);
			TextBlockStatus2.Text = "Image resolution: " + bmp?.PixelWidth + " x " + bmp?.PixelHeight;
			TextBlockStatus3.Text = "DPI: X=" + (int?)bmp?.DpiX + "  Y=" + (int?)bmp?.DpiY;
			TextBlockStatus4.Text = (currentIndex + 1).ToString() + @" / " + elements.Count.ToString();

			image.Stretch = Stretch.Uniform;
			originalTransform = image.RenderTransform;
			InitImageSize();

			//GC.Collect();
		}

		private void InitImageSize()
		{
			try
			{
				double imgWidth = CalculateImageWidth();
				double imgHeight = CalculateImageHeight();
				double imgRotatedWidth = imgWidth;
				double imgRotatedHeight = imgHeight;
				bool needSwap = (rotate == 90 || rotate == 270);

                if (needSwap)
				{
					(imgRotatedHeight, imgRotatedWidth) = (imgRotatedWidth, imgRotatedHeight);
				}

                if (IsStretch || (imgRotatedWidth > border.RenderSize.Width) || (imgRotatedHeight > border.RenderSize.Height))
                {
                    imgWidth = border.RenderSize.Width;
                    imgHeight = border.RenderSize.Height;

					//TODO
                    if (needSwap && imgRotatedWidth < imgRotatedHeight)
                    {
                        (imgHeight, imgWidth) = (imgWidth, imgHeight);
                    }
                }

				image.Width = imgWidth;
				image.Height = imgHeight;
			}
			catch
			{ }

			Matrix m = image.RenderTransform.Value;
			var newOffsetX = m.OffsetX;
			var newOffsetY = m.OffsetY;
			SetOffset(ref newOffsetX, ref newOffsetY);
			m.OffsetX = newOffsetX;
			m.OffsetY = newOffsetY;
			image.RenderTransform = new MatrixTransform(m);
		}

		private double getOriginalDpiX()
		{
			return bmp.DpiX > 0 ? bmp.DpiX : STANDART_DPI;
		}

		private double getOriginalDpiY()
		{
			return bmp.DpiY > 0 ? bmp.DpiY : STANDART_DPI;
		}

		private double CalculateImageWidth()
		{
			return bmp.Width * getOriginalDpiX() / Dpi;
		}

		private double CalculateImageHeight()
		{
			return bmp.Height * getOriginalDpiY() / Dpi;
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
			double imageWidth = image.Width;
			double imageHeight = image.Height;
			double borderWidth = border.RenderSize.Width;
			double borderHeight = border.RenderSize.Height;
			double a = imageWidth / CalculateImageWidth();
			double b = imageHeight / CalculateImageHeight();
			if (a < b)
			{
				imageHeight = CalculateImageHeight() * a;
			}
			else
			{
				imageWidth = CalculateImageWidth() * b;
			}

			if (rotate == 90 || rotate == 270)
			{
				var x = imageWidth;
				imageWidth = imageHeight;
				imageHeight = x;
			}

			var imgXMinCenter = ((borderWidth - imageWidth * Zoom) / 2) - ((borderWidth - imageWidth) / 2);
			var imgXMaxCenter = imgXMinCenter;
			var imgYMinCenter = ((borderHeight - imageHeight * Zoom) / 2) - ((borderHeight - imageHeight) / 2);
			var imgYMaxCenter = imgYMinCenter;

			if ((imageWidth * Zoom) > border.RenderSize.Width)
			{
				imgXMinCenter = -((imageWidth * Zoom - borderWidth) + (borderWidth - imageWidth) / 2);
				imgXMaxCenter = -((borderWidth - imageWidth) / 2);
			}

			if ((imageHeight * Zoom) > borderHeight)
			{
				imgYMinCenter = -((imageHeight * Zoom - borderHeight) + (borderHeight - imageHeight) / 2);
				imgYMaxCenter = -((borderHeight - imageHeight) / 2);
			}

			double xOffset = 0;
			double yOffset = 0;
			double minXCenter = imgXMinCenter;
			double maxXCenter = imgXMaxCenter;
			double minYCenter = imgYMinCenter;
			double maxYCenter = imgYMaxCenter;

			switch (rotate)
			{
				case 90:
					{
						xOffset = imageHeight + (imageWidth - imageHeight) / 2;
						yOffset = (imageWidth - imageHeight) / 2;
						minXCenter = -imgXMaxCenter;
						maxXCenter = -imgXMinCenter;
						break;
					}
				case 180:
					{
						xOffset = imageWidth;
						yOffset = imageHeight;
						minXCenter = -imgXMaxCenter;
						maxXCenter = -imgXMinCenter;
						minYCenter = -imgYMaxCenter;
						maxYCenter = -imgYMinCenter;
						break;
					}
				case 270:
					{
						xOffset = (imageHeight - imageWidth) / 2;
						yOffset = imageWidth + (imageHeight - imageWidth) / 2;
						minYCenter = -imgYMaxCenter;
						maxYCenter = -imgYMinCenter;
						break;
					}
			}

			double minOffsetX = xOffset + minXCenter;
			double maxOffsetX = xOffset + maxXCenter;
			double minOffsetY = yOffset + minYCenter;
			double maxOffsetY = yOffset + maxYCenter;

			OffsetX = OffsetX < minOffsetX ? minOffsetX : OffsetX;
			OffsetX = OffsetX > maxOffsetX ? maxOffsetX : OffsetX;

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

			double newOffsetX = m.OffsetX;
			double newOffsetY = m.OffsetY;
			SetOffset(ref newOffsetX, ref newOffsetY);
			m.OffsetX = newOffsetX;
			m.OffsetY = newOffsetY;

			image.RenderTransform = new MatrixTransform(m);
            //Title = "Width: " + image.Width.ToString() + " Height: " + image.Height.ToString() + " Xoffset: " + m.OffsetX.ToString() + " Yoffset: " + m.OffsetY.ToString();
        }

		private void MainWindow_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			Point p = e.MouseDevice.GetPosition(image);

			Matrix m = image.RenderTransform.Value;
			if (e.Delta > 0)
			{
				if ((Zoom * ZoomStep) <= 10)
				{
					Zoom *= ZoomStep;
					m.ScaleAtPrepend(ZoomStep, ZoomStep, p.X, p.Y);
				}
			}
			else
			{
				if ((Zoom / ZoomStep) >= 1)
				{
					Zoom /= ZoomStep;
					m.ScaleAtPrepend(1 / ZoomStep, 1 / ZoomStep, p.X, p.Y);
				}
				else
				{
					Zoom = 1;
					image.RenderTransform = originalTransform;

					m = image.RenderTransform.Value;
					m.RotateAt(rotate, image.RenderSize.Width / 2, image.RenderSize.Height / 2);
					image.RenderTransform = new MatrixTransform(m);

					return;
				}
			}

			double newOffsetX = m.OffsetX;
			double newOffsetY = m.OffsetY;
			SetOffset(ref newOffsetX, ref newOffsetY);
			m.OffsetX = newOffsetX;
			m.OffsetY = newOffsetY;
			image.RenderTransform = new MatrixTransform(m);
            //Title = "Width: " + image.Width.ToString() + " Height: " + image.Height.ToString() + " Xoffset: " + m.OffsetX.ToString() + " Yoffset: " + m.OffsetY.ToString();
        }

		private void Rotate_Click(object sender, RoutedEventArgs e)
		{
			double angle = int.Parse((string)((System.Windows.Controls.Button)sender).Tag);
			rotate += (angle == 90 ? 90 : -90);
			rotate = rotate < 0 ? 270 : (rotate > 270 ? 0 : rotate);
            Matrix m = image.RenderTransform.Value;
			m.RotateAt(angle, image.RenderSize.Width / 2, image.RenderSize.Height / 2);
			image.RenderTransform = new MatrixTransform(m);
            InitImageSize();
            //Title = "Width: " + image.Width.ToString() + " Height: " + image.Height.ToString() + " Xoffset: " + m.OffsetX.ToString() + " Yoffset: " + m.OffsetY.ToString();
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
			bmp = null;

			GC.Collect();
		}

		private void IsStretchCheckBox_Click(object sender, RoutedEventArgs e)
		{
			InitImageSize();
		}

		private void imageViewer_Loaded(object sender, RoutedEventArgs e)
		{
			ShowImage();
		}
	}
}
