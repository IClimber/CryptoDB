using CryptoDataBase.CDB;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageConverter
{
	static class ImgConverter
	{
		public static string[] imageExtensions = new string[] { ".bmp", ".jpg", ".jpeg", ".png", ".gif", ".psd", ".tif", ".tiff" };

		[DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool DeleteObject([In] IntPtr hObject);

		public static ImageSource BitmapToImageSource(Bitmap bmp)
		{
			if (bmp == null)
			{
				return null;
			}
			var handle = bmp.GetHbitmap();
			try
			{
				return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
			}
			finally
			{
				DeleteObject(handle);
			}
		}

		public static Bitmap BitmapFromSource(BitmapSource bitmapsource)
		{
			Bitmap bitmap;
			using (MemoryStream outStream = new MemoryStream())
			{
				BitmapEncoder enc = new BmpBitmapEncoder();
				enc.Frames.Add(BitmapFrame.Create(bitmapsource));
				enc.Save(outStream);
				bitmap = new Bitmap(outStream);
			}

			return bitmap;
		}

		public static bool isImage(string FileName)
		{
			return imageExtensions.Contains(Path.GetExtension(FileName).ToLower());
		}

		public static BitmapImage StreamToBitmapImage(Stream stream)
		{
			var bitmap = new BitmapImage();
			try
			{
				stream.Position = 0;
				bitmap.BeginInit();
				bitmap.StreamSource = stream;
				bitmap.CacheOption = BitmapCacheOption.OnLoad;
				bitmap.EndInit();
				bitmap.Freeze();
			}
			catch
			{
				return null;
			}

			return bitmap;
		}

		private static Bitmap _ResizeImage(Bitmap image, int width, int height)
		{
			var destRect = new System.Drawing.Rectangle(0, 0, width, height);
			var destImage = new Bitmap(width, height, image.PixelFormat);

			try
			{
				destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

				using (var graphics = Graphics.FromImage(destImage))
				{
					graphics.CompositingMode = CompositingMode.SourceCopy;
					graphics.CompositingQuality = CompositingQuality.HighQuality;
					graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
					graphics.SmoothingMode = SmoothingMode.HighQuality;
					graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

					using (var wrapMode = new ImageAttributes())
					{
						wrapMode.SetWrapMode(WrapMode.TileFlipXY);
						graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
					}
				}
			}
			catch (Exception m)
			{
				//MessageBox.Show(m.Message);
			}

			return destImage;
		}

		public static Bitmap ResizeImage(Bitmap image, int sideSize)
		{
			double k1 = image.Width / (double)sideSize;
			double k2 = image.Height / (double)sideSize;

			int width = (k1 < 1 || k2 < 1) ? image.Width : (int)(k1 > k2 ? (image.Width / k1) : (image.Width / k2));
			int height = (k1 < 1 || k2 < 1) ? image.Height : (int)(k1 > k2 ? (image.Height / k1) : (image.Height / k2));

			return _ResizeImage(image, width, height);
		}

		public static Bitmap GetIcon(string FileName, int RectSize)
		{
			try
			{
				Bitmap bmp;
				if (imageExtensions.Contains(Path.GetExtension(FileName).ToLower()))
				{
					bmp = new Bitmap(FileName);
				}
				else if (Path.GetExtension(FileName).ToLower() == ".ico")
				{
					bmp = new Icon(FileName, 256, 256).ToBitmap();
				}
				else
				{
					bmp = IconConverter.GetImage(FileName);
				}

				Bitmap result = ResizeImage(bmp, RectSize);
				bmp.Dispose();
				return result;

				//Icon.ExtractAssociatedIcon(FileName).ToBitmap();
			}
			catch
			{
				return null;
			}
		}
	}


	public class BitmapToImageSourceConvert : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			using (Bitmap bmp = (value as Element).Icon)
			{
				if ((bmp == null) && (value is DirElement))
				{
					return ImgConverter.BitmapToImageSource(CryptoDataBase.Properties.Resources.DirIcon);
				}

				return ImgConverter.BitmapToImageSource(bmp);
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return null;
		}
	}
}
