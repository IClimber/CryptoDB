using CryptoDataBase.CDB;
using ImageConverter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
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
		DirElement current_root;
		Bitmap bitmap;
		private CallBackResult resultCallback;

		public Finder()
		{
			InitializeComponent();
		}

		public Finder(DirElement Root) : this()
		{
			current_root = Root;
			root = Root.GetRootDir() as DirElement;
		}

		public Finder(DirElement Root, Bitmap thumbnail, CallBackResult Result) : this(Root)
		{
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

		public Finder(DirElement Root, CallBackResult Result) : this(Root)
		{
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

			if ((Keyboard.Modifiers == ModifierKeys.Control) && (e.Key == Key.D))
			{
				resultCallback(FindAllDuplicateImage((byte)slider.Value));
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

		private List<Element> FindAllDuplicateImage(byte sensative = 0)
		{
			Stopwatch sw = Stopwatch.StartNew();
			List<Element> resultList = new List<Element>();
			IList<Element> searchList = new List<Element>();
			_GetAllElementsWithIcon(current_root, searchList);
			Element[] search_array = searchList.ToArray();

			searchList.Clear();
			_GetAllElementsWithIcon(root, searchList);
			Element[] search_in_array = searchList.ToArray();

			searchList = null;
			byte cores_count = 4;
			int count = 1000;
			Object addLock = new Object();

			for (int i = 0; i < search_array.Length; i++)
			{
				bool addFirst = true;

				Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = cores_count }, k =>
				{
					int from = search_in_array.Length / count * k;
					int to = search_in_array.Length / count * (k + 1);

					if (k == (count - 1))
					{
						to = search_in_array.Length;
					}

					for (int j = from; j < to; j++)
					{
						if (Element.ComparePHashes(search_array[i].PHash, search_in_array[j].PHash, sensative) && !search_array[i].Equals(search_in_array[j]))
						{
							lock (addLock)
							{
								if (addFirst)
								{
									addFirst = false;
									resultList.Add(search_array[i]);
								}
							}

							lock (addLock)
							{
								resultList.Add(search_in_array[j]);
							}
						}
					}
				});
			}

			sw.Stop();
			//MessageBox.Show(sw.ElapsedMilliseconds.ToString());

			GC.Collect();
			return resultList;
		}

		private void _GetAllElementsWithIcon(DirElement parent, IList<Element> resultList)
		{
			foreach (Element element in parent.Elements)
			{
				if (element.IconSize > 0)
				{
					resultList.Add(element);
				}

				if (element is DirElement)
				{
					_GetAllElementsWithIcon(element as DirElement, resultList);
				}
			}
		}

		private void _FindAllDuplicateImage(DirElement parent, List<Element> resultList)
		{
			foreach (Element element in parent.Elements)
			{
				if (element.IconSize > 0)
				{
					var result = root.FindAllByPHash(element.PHash, (byte)slider.Value);
					if (result.Count > 1)
					{
						resultList.AddRange(result);
					}
				}

				if (element is DirElement)
				{
					_FindAllDuplicateImage(element as DirElement, resultList);
				}
			}
		}
	}
}
