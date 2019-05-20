using ImageConverter;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.ComponentModel;

namespace CryptoDataBase
{
	class MultithreadImageResizer
	{
		private List<FileItem> list = new List<FileItem>();
		private Object addLock = new Object();
		private int thumbnailSize;
		int initCount = 0;
		private IDictionary<string, Bitmap> images = new Dictionary<string, Bitmap>();
		private int images_count = 0;
		private BackgroundWorker ResizeWorker = new BackgroundWorker();

		private IEnumerable<bool> Infinite()
		{
			while (true)
			{
				yield return true;
			}
		}

		public MultithreadImageResizer(List<FileItem> list, int thumbnailSize)
		{
			UpdateFilesList(list);
			initCount = this.list.Count;

			this.thumbnailSize = thumbnailSize;
			ResizeWorker.DoWork += new DoWorkEventHandler(ConvertImage);

			if (!ResizeWorker.IsBusy)
			{
				ResizeWorker.RunWorkerAsync();
			}
		}

		public void UpdateFilesList(List<FileItem> newList)
		{
			ConvertFractalListToLinearList(newList, ref list);
		}

		private void ConvertFractalListToLinearList(List<FileItem> sourceList, ref List<FileItem> distList)
		{
			for (int i = 0; i < sourceList.Count; i++)
			{
				if (sourceList[i].type == FileItemType.File)
				{
					if (!distList.Contains(sourceList[i]))
					{
						distList.Add(sourceList[i]);
					}
				}
				else
				{
					ConvertFractalListToLinearList(sourceList[i].children, ref distList);
				}
			}
		}

		private void ConvertImage(object sender, DoWorkEventArgs e)
		{
			StartConvert();
		}

		private void StartConvert()
		{
			byte cores_count = 4;
			Object sleepLock = new Object();
			Object incLock = new Object();
			int i = 0;

			Parallel.ForEach(Infinite(), new ParallelOptions { MaxDegreeOfParallelism = cores_count }, (k, loopState) =>
			{
				int l;

				lock (incLock)
				{
					l = i;
					i++;
				}

				if (l >= list.Count)
				{
					loopState.Stop();
					return;
				}

				Bitmap bmp = ImgConverter.GetIcon(list[l].name, thumbnailSize);
				lock (addLock)
				{
					images.Add(list[l].name, bmp);
					images_count = images.Count;
				}
				//bmp.Dispose();

				lock (sleepLock)
				{
					while (images_count > cores_count)
					{
						Thread.Sleep(1);
					};
				}
			});
		}

		public Bitmap GetBitmap(string fileName)
		{
			Bitmap bitmap;

			while (!images.ContainsKey(fileName))
			{
				Thread.Sleep(1);
			}

			lock (addLock)
			{
				bitmap = images[fileName];
				images.Remove(fileName);
				images_count = images.Count;
			}

			return bitmap;
		}
	}
}
