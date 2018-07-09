using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;

namespace ImageConverter
{
	public static class IconConverter
	{
		private static SysImageList _imgList = new SysImageList(SysImageListSize.jumbo);

		private static Bitmap loadJumbo(string lookup)
		{
			_imgList.ImageListSize = isVistaUp() ? SysImageListSize.jumbo : SysImageListSize.extraLargeIcons;
			Icon icon = _imgList.Icon(_imgList.IconIndex(lookup, false));
			Bitmap bitmap = icon.ToBitmap();
			icon.Dispose();

			System.Drawing.Color empty = System.Drawing.Color.FromArgb(0, 0, 0, 0);

			if (bitmap.GetPixel(100, 100) == empty && bitmap.GetPixel(200, 200) == empty && bitmap.GetPixel(200, 200) == empty)
			{
				_imgList.ImageListSize = SysImageListSize.largeIcons;
				bitmap = _imgList.Icon(_imgList.IconIndex(lookup)).ToBitmap();
			}

			return bitmap;
		}


		public static bool isVistaUp()
		{
			return (Environment.OSVersion.Version.Major >= 6);
		}

		public static Bitmap GetImage(string fileName)
		{
			return loadJumbo(fileName);
		}
	}
}
