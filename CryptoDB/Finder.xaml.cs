using CryptoDataBase.CDB;
using ImageConverter;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

		DirElement search_dir;
		Bitmap _bitmap;
		private BackgroundWorker searchWorker = new BackgroundWorker();
		private CallBackResult resultCallback;
		private const int FIND_BY_NAME = 0;
		private const int FIND_BY_ICON = 1;
		private const int FIND_DUPLICATE_BY_ICON = 2;

		private byte _sensative = 0;
		private string _search_text = "";
		private bool _find_in_all_dirs = true;
		private bool _find_as_tag = false;
		private bool _all_tags = false;
		private static int threadsCount = Environment.ProcessorCount;

		List<Element> resultList;

		private Finder()
		{
			InitializeComponent();

			resultList = new List<Element>();
			//searchWorker.WorkerReportsProgress = true;
			searchWorker.WorkerSupportsCancellation = true;
			searchWorker.DoWork += new DoWorkEventHandler(SearchWork);
			//searchWorker.ProgressChanged += new ProgressChangedEventHandler(XDBProgress);
			searchWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(SearchComplete);
		}

		private Finder(DirElement Root) : this()
		{
			search_dir = Root;
		}

		public Finder(DirElement Root, Bitmap thumbnail, CallBackResult Result) : this(Root)
		{
			resultCallback = Result;
			SetThumbnail(thumbnail);
		}

		private void SetThumbnail(Bitmap thumbnail)
		{
			image.Source = ImgConverter.BitmapToImageSource(thumbnail);
			_bitmap?.Dispose();
			_bitmap = thumbnail;
			slider.Focus();
		}

		public Finder(DirElement Root, CallBackResult Result) : this(Root)
		{
			image.Source = null;
			_bitmap?.Dispose();
			resultCallback = Result;
			textBox.Focus();
		}

		private void _setSearchParams()
		{
			_sensative = (byte)slider.Value;
			_search_text = textBox.Text;
			_find_in_all_dirs = findInAllDirs.IsChecked == true;
			_find_as_tag = findAsTag.IsChecked == true;
			_all_tags = allTags.IsChecked == true;
			Opacity = 0.4;
		}

		private DirElement getSearchableDir()
		{
			var dir = _find_in_all_dirs ? search_dir.GetRootDir() as DirElement : search_dir;
			return _find_in_all_dirs ? search_dir.GetRootDir() as DirElement : search_dir;
		}

		private void SearchWork(object sender, DoWorkEventArgs e)
		{
			switch (e.Argument)
			{
				case FIND_BY_ICON:
					resultList = getSearchableDir().FindAllByIcon(_bitmap, _sensative);
					break;
				case FIND_DUPLICATE_BY_ICON:
					resultList = FindAllDuplicateImage(_sensative);
					break;
				default:
					resultList = getSearchableDir().FindByName(_search_text, _find_as_tag, _all_tags);
					break;
			}
		}

		private void SearchComplete(object sender, RunWorkerCompletedEventArgs e)
		{
			resultCallback(resultList);
			Opacity = 1;
		}

		private void ButtonFind_Click(object sender, RoutedEventArgs e)
		{
			int findBy = FIND_BY_NAME;

			if (image.Source != null)
			{
				findBy = FIND_BY_ICON;
			}
			else
			{
				findBy = FIND_BY_NAME;
			}

			if (!searchWorker.IsBusy)
			{
				_setSearchParams();
				searchWorker.RunWorkerAsync(findBy);
			}
		}

		private void Button_Click_1(object sender, RoutedEventArgs e)
		{
			_bitmap?.Dispose();
			image.Source = null;
		}

		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if ((Keyboard.Modifiers == ModifierKeys.Control) && (e.Key == Key.V))
			{
				if (Clipboard.ContainsImage())
				{
					Bitmap tmp = ImgConverter.BitmapFromSource(Clipboard.GetImage());
					SetThumbnail(ImgConverter.ResizeImage(tmp, MainWindow.THUMBNAIL_SIZE));
					tmp?.Dispose();
				}
			}
		}

		private void Window_KeyDown(object sender, KeyEventArgs e)
		{
			//Enter
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

			//Escape
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

			//Ctrl+D
			if ((Keyboard.Modifiers == ModifierKeys.Control) && (e.Key == Key.D) && !searchWorker.IsBusy)
			{
				_setSearchParams();
				searchWorker.RunWorkerAsync(FIND_DUPLICATE_BY_ICON);
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
					SetThumbnail(ImgConverter.ResizeImage(tmp, MainWindow.THUMBNAIL_SIZE));
					tmp?.Dispose();
				}
			}
		}

		private List<Element> FindAllDuplicateImage(byte sensative = 0)
		{
			Stopwatch sw = Stopwatch.StartNew();
			List<Element> resultList = new List<Element>();
			IList<Element> searchList = new List<Element>();
			_GetAllElementsWithIcon(search_dir, searchList);
			Element[] search_array = searchList.ToArray();

			searchList.Clear();
			_GetAllElementsWithIcon(getSearchableDir(), searchList);
			Element[] search_in_array = searchList.ToArray();

			searchList = null;
			int count = 1000;
			Object addLock = new Object();

			for (int i = 0; i < search_array.Length; i++)
			{
				bool addFirst = true;

				Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = threadsCount }, k =>
				{
					int from = search_in_array.Length / count * k;
					int to = search_in_array.Length / count * (k + 1);

					if (k == (count - 1))
					{
						to = search_in_array.Length;
					}

					for (int j = from; j < to; j++)
					{
						if (Element.ComparePHashes(search_array[i].IconPHash, search_in_array[j].IconPHash, sensative) && !search_array[i].Equals(search_in_array[j]))
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
				if (element.IconSize > 0 && MainWindow.IsImage(element.Name))
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
					var result = getSearchableDir().FindAllByPHash(element.IconPHash, (byte)slider.Value);
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
