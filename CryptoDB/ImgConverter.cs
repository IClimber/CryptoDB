using CryptoDataBase.CryptoContainer.Models;
using CryptoDataBase.Helpers;
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
        public static string[] imageExtensions = new string[] { "bmp", "jpg", "jpeg", "png", "gif", "psd", "tif", "tiff", "jfif", "webp" };
        public static string[] videoExtensions = new string[] { "mkv", "mp4", "m2ts", "3gp", "webm", "flv", "vob", "wmv", "mpg", "mpeg", "m4v", "rm", "ts", "avi", "mov", "3gpp", "rmvb", "divx", "mts" };

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

        public static bool IsImage(string FileName)
        {
            return imageExtensions.Contains(Path.GetExtension(FileName).Replace(".", "").ToLower());
        }

        public static BitmapImage StreamToBitmapImage(Stream stream)
        {
            var bitmap = new BitmapImage();
            try
            {
                bitmap.BeginInit();
                bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
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
            double k = Math.Max(image.Width, image.Height) / (double)sideSize;

            double width = k <= 1 ? image.Width : image.Width / k;
            double height = k <= 1 ? image.Height : image.Height / k;

            return _ResizeImage(image, (int)width, (int)height);
        }

        public static Bitmap GetIcon(string FileName, int RectSize)
        {
            try
            {
                Bitmap bmp = null;

                if (imageExtensions.Contains(Path.GetExtension(FileName).Replace(".", "").ToLower()))
                {
                    return GetImageThumbFromFile(FileName, RectSize);
                }
                else if (Path.GetExtension(FileName).ToLower() == ".ico")
                {
                    bmp = new Icon(FileName, 256, 256).ToBitmap();
                }
                else if (videoExtensions.Contains(Path.GetExtension(FileName).Replace(".", "").ToLower()))
                {
                    try
                    {
                        bmp = VideoHelper.GetFrameWithWaterMark(FileName);
                    }
                    catch
                    {
                    }
                }

                if (bmp == null)
                {
                    bmp = IconConverter.GetImage(FileName);
                }

                Bitmap result = ResizeImage(bmp, RectSize);
                bmp.Dispose();

                return result;

                //Icon.ExtractAssociatedIcon(FileName).ToBitmap();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static Bitmap GetIcon(string FileName, Stream sourceStream, int RectSize)
        {
            try
            {
                Bitmap bmp;
                if (imageExtensions.Contains(Path.GetExtension(FileName).Replace(".", "").ToLower()))
                {
                    return GetImageThumbFromStream(sourceStream, RectSize);
                }
                else if (Path.GetExtension(FileName).ToLower() == ".ico")
                {
                    bmp = new Icon(sourceStream, 256, 256).ToBitmap();
                }
                else
                {
                    bmp = IconConverter.GetImage(FileName);
                }

                Bitmap result = ResizeImage(bmp, RectSize);
                bmp.Dispose();

                return result;
            }
            catch
            {
                return null;
            }
        }

        public static Bitmap GetImageThumbFromFile(string FileName, int RectSize)
        {
            using (var inputStream = File.OpenRead(FileName))
            {
                return GetImageThumbFromStream(inputStream, RectSize);
            }
        }

        public static Bitmap GetImageThumbFromStream(Stream sourceStream, int RectSize)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                using (var resized = NetVips.Image.ThumbnailStream(sourceStream, width: RectSize, height: RectSize))
                {
                    resized.PngsaveStream(outStream, q: 100, keep: NetVips.Enums.ForeignKeep.Icc);
                }

                using (Bitmap temp = new Bitmap(outStream))
                {
                    return new Bitmap(temp);
                }
            }
        }

        // Deprecated
        private static Bitmap BitmapImage2Bitmap(BitmapImage bitmapImage)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);

                using (Bitmap bitmap = new Bitmap(outStream))
                {
                    return new Bitmap(bitmap);
                }
            }
        }
    }


    public class BitmapToImageSourceConvert : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            using (Bitmap bmp = (value as Element).Icon)
            {
                if ((bmp == null) && (value is DirectoryElement))
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
