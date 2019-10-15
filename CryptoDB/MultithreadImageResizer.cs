using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Threading;
using System.ComponentModel;

namespace CryptoDataBase
{
	class MultithreadImageResizer: IDisposable
	{
		public delegate Bitmap CallBackConvert(string FileName);
		private CallBackConvert converter;
		private List<FileItem> list = new List<FileItem>();
		private Object addLock = new Object();
		private IDictionary<string, Bitmap> images = new Dictionary<string, Bitmap>();
		private int images_count = 0;
		private BackgroundWorker ResizeWorker = new BackgroundWorker();
        private static int threadsCount = (int)Math.Ceiling(Environment.ProcessorCount / 2.0);

        private IEnumerable<bool> Infinite()
		{
			while (true)
			{
				yield return true;
			}
		}

		public MultithreadImageResizer(List<FileItem> list, CallBackConvert callBackConverter)
		{
			converter = callBackConverter;
			UpdateFilesList(list);

			ResizeWorker.DoWork += new DoWorkEventHandler(ConvertImage);
			ResizeWorker.RunWorkerAsync();
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
			Object sleepLock = new Object();
			Object incLock = new Object();
			int i = 0;

			Parallel.ForEach(Infinite(), new ParallelOptions { MaxDegreeOfParallelism = threadsCount }, (k, loopState) =>
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

				Bitmap bmp = converter(list[l].name);
				lock (addLock)
				{
					images.Add(list[l].name, bmp);
					images_count = images.Count;
				}

				lock (sleepLock)
				{
					while (images_count > threadsCount)
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

		public void Dispose()
		{
			list.Clear();
			images.Clear();
		}
	}
}
