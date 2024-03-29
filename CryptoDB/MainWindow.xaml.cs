﻿using ClipboardAssist;
using CryptoDataBase.CryptoContainer.Comparers;
using CryptoDataBase.CryptoContainer.Exceptions;
using CryptoDataBase.CryptoContainer.Helpers;
using CryptoDataBase.CryptoContainer.Models;
using CryptoDataBase.CryptoContainer.Types;
using CryptoDataBase.Services;
using ImageConverter;
using SevenZip;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;

namespace CryptoDataBase
{
	/// <summary>
	/// Логика взаимодействия для MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		private static string[] ImageExtensions = new string[] { ".bmp", ".jpg", ".jpeg", ".png", ".gif", ".ico", ".jfif" };
		private static string[] TextExtensions = new string[] { ".txt", ".sql", ".pas", ".cs", ".ini", ".log" };
		private static string[] ArchiveExtensions = new string[] { ".zip", ".rar", ".7z", ".001", ".tar", ".gzip", ".cbr" };
		private List<Element> CutList = new List<Element>();
		CryptoContainer.CryptoContainer xdb;
		string password;
		string databaseFile;
		public const int THUMBNAIL_SIZE = 200;
		private BackgroundWorker xdbLoadWorker = new BackgroundWorker();
		private BackgroundWorker FileLoadWorker = new BackgroundWorker();
		private BackgroundWorker FileExportWorker = new BackgroundWorker();
		Stopwatch sw = new Stopwatch();
		List<FileItem> addedFilesList = new List<FileItem>();
		MultithreadImageResizer multithreadImageResizer;
		DirectoryElement LastParent;
		FileElement movedElementFrom = null;
		FileElement movedElementTo = null;
		private int AddedFilesCount;
		public bool editable = true;
		private IEnumerable<Element> temp_search_list = null;
		ClipboardMonitor cl = new ClipboardMonitor();
		bool isCompareHash = false;
		bool isCompareImage = false;
		bool save_real_file_name = false;
		byte imgDuplicateSensative = 0;
		public bool ExtractArchiveWhenAdd
		{
			get { return _ExtractArchiveWhenAdd; }
			set { _ExtractArchiveWhenAdd = value; }
		}
		public bool _ExtractArchiveWhenAdd = false;
		public bool ShowDuplicateMessage
		{
			get { return _ShowDuplicateMessage; }
			set { _ShowDuplicateMessage = value; }
		}
		public bool _ShowDuplicateMessage = true;

		public Visibility CanChangePassword
		{
			get { return _CanChangePassword; }
			set
			{
				_CanChangePassword = value;
				PropertyChanged(this, new PropertyChangedEventArgs("CanChangePassword"));
			}
		}
		public Visibility _CanChangePassword = Visibility.Collapsed;
		private bool _faildedOpen = false;

		public event PropertyChangedEventHandler PropertyChanged;

		public bool IsReadOnly => xdb.IsReadOnly;

		public MainWindow(string DatabaseFile)
		{
			InitializeComponent();

			databaseFile = DatabaseFile;

			xdbLoadWorker.WorkerReportsProgress = true;
			xdbLoadWorker.WorkerSupportsCancellation = true;
			xdbLoadWorker.DoWork += new DoWorkEventHandler(XDBLoad);
			xdbLoadWorker.ProgressChanged += new ProgressChangedEventHandler(XDBProgress);
			xdbLoadWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(XDBLoadCompleted);


			FileLoadWorker.WorkerReportsProgress = true;
			FileLoadWorker.WorkerSupportsCancellation = false;
			FileLoadWorker.DoWork += new DoWorkEventHandler(FileLoad);
			FileLoadWorker.ProgressChanged += new ProgressChangedEventHandler(FileProgress);
			FileLoadWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(FileLoadCompleted);

			FileExportWorker.WorkerReportsProgress = true;
			//FileLoadWorker.WorkerSupportsCancellation = false;
			FileExportWorker.DoWork += new DoWorkEventHandler(ExportFiles);
			FileExportWorker.ProgressChanged += new ProgressChangedEventHandler(ExportProgress);
			FileExportWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(FileExportCompleted);

			search_text_box.KeyDown += SearchTextBox_KeyDown;
			search_text_box.TextChanged += SearchTextBox_TextChanged;
		}

		private void ClipboardChange(object sender, ClipboardChangedEventArgs e)
		{
			InsertImageFromClipboard(true);
		}

		public static bool IsImage(string FileName)
		{
			return ImageExtensions.Contains(Path.GetExtension(FileName).ToLower());
		}

		public static bool IsText(string FileName)
		{
			return TextExtensions.Contains(System.IO.Path.GetExtension(FileName).ToLower());
		}

		private void CountFilesInDirectory(string directory, ref int count)
		{
			count += Directory.GetFiles(directory).Length;
			foreach (var subDirectory in Directory.GetDirectories(directory))
			{
				CountFilesInDirectory(subDirectory, ref count);
			}
		}

		private int GetCountSubElements(Element root)
		{
			if (root is FileElement)
			{
				return 1;
			}

			int result = (root as DirectoryElement).Elements.Count;

			foreach (var item in (root as DirectoryElement).Elements)
			{
				result += GetCountSubElements(item);
			}

			return result;
		}

		private List<Element> GetDuplicates(string FileName, Bitmap thumbnail = null)
		{
			List<Element> duplicates = new List<Element>();

			try
			{
				if ((isCompareImage) && (IsImage(FileName)))
				{
					//Bitmap thumbnail = ImgConverter.GetIcon(FileName, thumbnailSize);
					duplicates = xdb.FindAllByIcon(thumbnail, imgDuplicateSensative);
					//thumbnail?.Dispose();
				}
				else if (isCompareHash)
				{
					using (FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read))
					{
						duplicates = xdb.FindByHash(HashHelper.GetMD5(fs));
					}
				}
			}
			catch (Exception e)
			{
				System.Windows.MessageBox.Show(e.Message);
			}

			return duplicates;
		}

		private void ShowDuplicateFilesList(List<Element> duplicates, string mainFileName = "", Bitmap thumbnail = null)
		{
			if (!_ShowDuplicateMessage)
			{
				return;
			}

			DuplicatesWindow duplicateWindow = null;
			Dispatcher.Invoke(() => duplicateWindow = new DuplicatesWindow(thumbnail, mainFileName, duplicates) { Owner = this });
			Dispatcher.Invoke(() => duplicateWindow.ShowDialog());
		}

		private string GetUserPassword(string title)
		{
			string password = null;
			PassWindow passwordWindow = null;
			Dispatcher.Invoke(() =>
			{
				passwordWindow = new PassWindow() { Owner = this, Title = title };
				passwordWindow.ShowDialog();
				if (passwordWindow.DialogResult == true)
				{
					password = passwordWindow.Password;
				}
			});

			return password;
		}

		private DirectoryElement ForceDirectories(DirectoryElement parent, string path)
		{
			var directories = path.Split(Path.DirectorySeparatorChar);
			DirectoryElement currentParent = parent;

			foreach (var directory in directories)
			{
				if (directory == "")
				{
					break;
				}

				Element element = currentParent.FindByName(directory);

				if (element == null)
				{
					currentParent = currentParent.CreateDirectory(directory);
				}
				else if (element is DirectoryElement)
				{
					currentParent = (DirectoryElement)element;
				}
				else
				{
					return null;
				}
			}

			return currentParent;
		}

		private void AddArchive(FileItem item)
		{
			SevenZipExtractor extractor = Archive.OpenArchive(item.name, GetUserPassword);

			foreach (var file in extractor.ArchiveFileData)
			{
				if (file.IsDirectory)
				{
					try
					{
						ForceDirectories(item.parentElement, file.FileName);
					}
					catch (Exception exception)
					{
						extractor.Dispose();
						throw exception;
					}
				}
				else
				{
					try
					{
						using (MemoryStream outMemStream = new MemoryStream())
						{
							extractor.ExtractFile(file.Index, outMemStream);
							outMemStream.Position = 0;
							Bitmap icon = ImgConverter.GetIcon(file.FileName, outMemStream, THUMBNAIL_SIZE);

							var parentDirectory = Path.GetDirectoryName(file.FileName);
							var parent = ForceDirectories(item.parentElement, parentDirectory);
							outMemStream.Position = 0;
							parent.AddFile(outMemStream, Path.GetFileName(file.FileName), false, icon, ReportProgress);
						}
					}
					catch (Exception exception)
					{
						extractor.Dispose();
						throw exception;
					}
				}
			}

			extractor.Dispose();
		}

		private void AddFile(FileItem item)
		{
			/*if (item.parentElement.FileExists(Path.GetFileName(item.name)))
			{
				return;
			}*/

			if (item.type == FileItemType.File)
			{
				AddedFilesCount++;

				if (ExtractArchiveWhenAdd && ArchiveExtensions.Contains(Path.GetExtension(item.name).ToLower()))
				{
					AddArchive(item);
				}
				else
				{
					using (Bitmap bmp = multithreadImageResizer.GetBitmap(item.name))
					{
						//Bitmap bmp = ImgConverter.GetIcon(item.name, thumbnailSize);
						var duplicates = GetDuplicates(item.name, bmp);
						if (duplicates.Count > 0)
						{
							ShowDuplicateFilesList(duplicates, item.name, bmp);
							bmp?.Dispose();
							return;
						}

						try
						{
							item.parentElement.AddFile(item.name, Path.GetFileName(item.name), false, bmp, ReportProgress);
						}
						catch (DuplicatesFileNameException)
						{

						}
					}
				}
			}
			else
			{
				DirectoryElement newParent;

				try
				{
					var element = item.parentElement.FindByName(Path.GetFileName(item.name));
					if (element != null && element is DirectoryElement)
					{
						newParent = (DirectoryElement)element;
					}
					else
					{
						newParent = item.parentElement.CreateDirectory(Path.GetFileName(item.name));
					}
				}
				catch (Exception e)
				{
					System.Windows.MessageBox.Show(e.Message);
					return;
				}

				foreach (FileItem sub_item in item.children)
				{
					sub_item.parentElement = newParent;
					AddFile(sub_item);
				}
			}
		}

		private Bitmap GetIcon(string FileName)
		{
			return ImgConverter.GetIcon(FileName, THUMBNAIL_SIZE);
		}

		private void AddFiles(List<FileItem> files)
		{
			sw = Stopwatch.StartNew();

			if (multithreadImageResizer == null)
			{
				multithreadImageResizer = new MultithreadImageResizer(files, GetIcon);
			}
			else
			{
				multithreadImageResizer.UpdateFilesList(files);
			}

			for (int i = 0; i < files.Count; i++)
			{
				try
				{
					AddFile(files[i]);
				}
				catch (Exception exception)
				{
					System.Windows.MessageBox.Show(exception.Message);
				}
			}

			multithreadImageResizer = null;
			GC.Collect();
		}

		private void Files_Drop(object sender, System.Windows.DragEventArgs e)
		{
			DirectoryElement parent = (listView.Tag as DirectoryElement);
			if ((xdb == null) || (parent == null) || IsReadOnly)
			{
				return;
			}

			if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
			{
				string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);

				foreach (var file in files)
				{
					var exists = false;
					foreach (var item in addedFilesList)
					{
						if (item.name == file && item.parentElement == parent)
						{
							exists = true;
						}
					}

					if (!exists)
					{
						addedFilesList.Add(new FileItem(file, parent));
					}
				}

				int count = 0;
				foreach (var item in addedFilesList)
				{
					count += item.SubFilesCount();
				}

				TextBlockStatus4.Text = count.ToString();

				if (!FileLoadWorker.IsBusy)
				{
					AddedFilesCount = 0;
					statusBlock3.Visibility = Visibility.Visible;
					TextBlockStatus3.Text = AddedFilesCount.ToString();
					FileLoadWorker.RunWorkerAsync(parent);
				}
				else
				{
					if (multithreadImageResizer != null)
					{
						multithreadImageResizer.UpdateFilesList(addedFilesList);
					}
				}
			}
		}

		#region AddFiles worker
		private void FileLoad(object sender, DoWorkEventArgs e)
		{
			if (IsReadOnly || !editable)
			{
				return;
			}

			editable = false;
			AddFiles(addedFilesList);
		}

		private void ReportProgress(double progress)
		{
			FileLoadWorker.ReportProgress((int)progress);
		}

		private void FileProgress(object sender, ProgressChangedEventArgs e)
		{
			progressbar1.Value = e.ProgressPercentage;
			if (TextBlockStatus3.Text != AddedFilesCount.ToString())
			{
				TextBlockStatus3.Text = AddedFilesCount.ToString();
			}
		}

		private void FileLoadCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			editable = true;
			Title = sw.ElapsedMilliseconds.ToString();
			addedFilesList.Clear();
			statusBlock3.Visibility = Visibility.Collapsed;
			//TextBlockStatus1.Text = (AddedFilesCount++).ToString();
			progressbar1.Value = 0;

			ShowFiles(listView.Tag as Element);
		}
		#endregion

		#region ExportFiles worker
		private void ExportFiles(object sender, DoWorkEventArgs e)
		{
			e.Result = e.Argument;

			if ((e.Argument as ExportInfo).Elements.Count == 1)
			{
				//sw.Restart();
				if ((e.Argument as ExportInfo).SaveAs)
				{
					if (save_real_file_name)
					{
						(e.Argument as ExportInfo).Elements[0]?.SaveAs((e.Argument as ExportInfo).FileName, ReportProgress1);
					}
					else
					{
						(e.Argument as ExportInfo).Elements[0]?.SaveAs((e.Argument as ExportInfo).FileName, ReportProgress1, GetRandomName);
					}
				}
				else
				{
					(e.Argument as ExportInfo).Elements[0]?.SaveTo((e.Argument as ExportInfo).FileName, ReportProgress1);
				}
				//System.Windows.MessageBox.Show(sw.ElapsedMilliseconds.ToString());

				return;
			}

			foreach (var element in (e.Argument as ExportInfo).Elements)
			{
				if (save_real_file_name)
				{
					element?.SaveTo((e.Argument as ExportInfo).FileName, ReportProgress1);
				}
				else
				{
					element?.SaveAs((e.Argument as ExportInfo).FileName + "\\" + GetRandomName(Path.GetExtension(element.Name)), ReportProgress1, GetRandomName);
				}
			}
		}

		private void ReportProgress1(double progress)
		{
			FileExportWorker.ReportProgress((int)progress);
		}

		private void ExportProgress(object sender, ProgressChangedEventArgs e)
		{
			progressbar1.Value = e.ProgressPercentage;
			/*if (TextBlockStatus1.Text != AddedFilesCount.ToString())
			{
				TextBlockStatus1.Text = AddedFilesCount.ToString();
			}*/
		}

		private void FileExportCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			//TextBlockStatus1.Text = (AddedFilesCount++).ToString();
			progressbar1.Value = 0;

			if ((e.Result as ExportInfo).RunAfterCompleted)
			{
				Process.Start((e.Result as ExportInfo).FileName);
			}
		}
		#endregion

		#region XDB worker
		private void XDBLoad(object sender, DoWorkEventArgs e)
		{
			sw.Restart();
			if (databaseFile == "")
			{
				return;
			}

			try
			{
				_faildedOpen = true;
				xdb?.Dispose();
				xdb = null;
				GC.Collect();

				CanChangePassword = Visibility.Collapsed;
				xdb = new CryptoContainer.CryptoContainer(databaseFile, e.Argument.ToString(), this.PerortProgress);
				CanChangePassword = xdb.CanChangePassword() && !IsReadOnly ? Visibility.Visible : Visibility.Collapsed;
				_faildedOpen = false;
			}
			catch (ReadingDataException ex)
			{
				System.Windows.MessageBox.Show(ex.Message);
			}
			catch (UnsupportedVersionException)
			{
				System.Windows.MessageBox.Show("Unsupported version");
			}
			catch (Exception ex)
			{
				System.Windows.MessageBox.Show(ex.Message);
			}
		}

		private void PerortProgress(double progress, string message)
		{
			xdbLoadWorker.ReportProgress((int)progress, message);
		}

		private void XDBProgress(object sender, ProgressChangedEventArgs e)
		{
			this.progressbar1.Value = e.ProgressPercentage;
			if (TextBlockStatus1.Text != (string)e.UserState)
			{
				TextBlockStatus1.Text = (string)e.UserState;
			}
		}

		private void XDBLoadCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if (_faildedOpen)
			{
				OpenOrCreateCDB();
				return;
			}

			sw.Stop();
			Title = sw.ElapsedMilliseconds.ToString() + "ms" + (xdb.IsReadOnly ? " (readonly)" : "");
			progressbar1.Value = 0;

			ShowFiles(xdb);
		}
		#endregion

		public void SelectItem(object element)
		{
			listView.ScrollIntoView(element);
			listView.UpdateLayout();
			var item = listView.ItemContainerGenerator.ContainerFromItem(element);
			(item as UIElement)?.Focus();
		}

		private void button1_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog op = new OpenFileDialog();
			op.DefaultExt = ".cdb";
			op.Filter = "Crypto database (*.CDB)|*.cdb";
			op.CheckFileExists = false;
			if (op.ShowDialog() != System.Windows.Forms.DialogResult.OK)
			{
				return;
			}

			databaseFile = op.FileName;

			OpenOrCreateCDB();
		}

		private void ShowInfo()
		{
			TextBlockStatus1.Text = "All: " + listView.Items.Count.ToString();
			TextBlockStatus2.Text = "Selected: " + listView.SelectedItems.Count.ToString();
		}

		private BindingList<Element> OrderBy(IList<Element> list)
		{
			if (list == null)
			{
				return null;
			}

			List<Element> result;
			if (TypeSort.IsChecked == true)
			{
				result = ((TypeSort.Tag as bool?) == false)
					? list.OrderByDescending(x => x, new ExtComparer()).ToList()
					: list.OrderBy(x => x, new ExtComparer()).ToList();
			}
			else if (SizeSort.IsChecked == true)
			{
				result = ((SizeSort.Tag as bool?) == false)
					? list.OrderByDescending(x => x, new SizeComparer()).ToList()
					: list.OrderBy(x => x, new SizeComparer()).ToList();
			}
			else if (DateSort.IsChecked == true)
			{
				result = ((DateSort.Tag as bool?) == false)
					? list.OrderByDescending(x => x, new TimeComparer()).ToList()
					: list.OrderBy(x => x, new TimeComparer()).ToList();
			}
			else //if (NameSort.IsChecked == true)
			{
				result = ((NameSort.Tag as bool?) == false)
					? list.OrderByDescending(x => x, new NameComparer()).ToList()
					: list.OrderBy(x => x, new NameComparer()).ToList();
			}

			return new BindingList<Element>(result.OrderByDescending(x => x.Type).ToList());
		}

		private void SetViewsElement(DirectoryElement parent, BindingList<Element> elements, Element selected = null)
		{
			RefreshPathPanel(parent != null ? parent : LastParent);
			listView.ItemsSource = elements;

			if (parent != null)
			{
				listView.Focus();
			}

			if (selected != null)
			{
				SelectItem(selected);
			}

			temp_search_list = elements;

			LastParent = parent == null ? LastParent : parent;
			listView.Tag = parent;
			ShowInfo();
		}

		private void ShowFiles(Element element, Element selected = null, bool select_first = false)
		{
			if (element == null)
			{
				return;
			}

			//Відкриваємо папку
			if (element is DirectoryElement)
			{
                var elements = OrderBy((element as DirectoryElement).Elements);

                Element select = select_first ? elements.FirstOrDefault() : selected;
				SetViewsElement((element as DirectoryElement), elements, select);

				return;
			}

			//Відкриваємо файл
			if (IsImage(element.Name))
			{
				OpenImage(element);
			}
			else if (IsText(element.Name))
			{
				OpenTextDoc(element as FileElement);
			}
			else
			{
				if (listView.SelectedItems.Count == 1)
				{
					SaveSelectedAs(true);
				}
			}
		}

		public void ShowList(List<Element> elements)
		{
            SetViewsElement(null, new BindingList<Element>(elements), null);
		}

		private void listView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			TextBlockStatus2.Text = "Selected: " + listView.SelectedItems.Count.ToString();
		}

		private void OpenParentDir(Element element)
		{
			if (element.Parent == null)
			{
				return;
			}

			ShowList(element.Parent.Elements.ToList());
			SelectItem(element);
			LastParent = element.Parent;
			listView.Tag = element.Parent;
		}

		private void ShowPropertiesWindow(List<Element> elements)
		{
			PropertiesWindow property = new PropertiesWindow(elements);
			property.Owner = this;
			property.Show();
		}

		private void listView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if ((Keyboard.Modifiers == ModifierKeys.Control) && (e.Key == Key.Enter))
			{
				e.Handled = true;
				ShowPropertiesWindow(listView.SelectedItems.Cast<Element>().ToList());
				return;
			}

			if (e.Key == Key.Enter)
			{
				e.Handled = true;
				if (listView.SelectedItem != null)
				{
					ShowFiles(listView.SelectedItem as Element, null, true);
				}
			}

			if (e.Key == Key.Delete)
			{
				DeleteSelected();
				e.Handled = true;
			}

			if (e.Key == Key.F2)
			{
				Rename();
			}

			if ((Keyboard.Modifiers == ModifierKeys.Control) && (e.Key == Key.P))
			{
				OpenParentDir(listView.SelectedItem as Element);
			}

			if (e.Key == Key.C && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == (ModifierKeys.Control | ModifierKeys.Alt)
				&& (listView.SelectedItem != null) && listView.SelectedItem is FileElement)
			{
				movedElementFrom = listView.SelectedItem as FileElement;
			}

			if (e.Key == Key.V && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == (ModifierKeys.Control | ModifierKeys.Alt)
				&& (listView.SelectedItem != null) && (movedElementFrom != null) && listView.SelectedItem is FileElement)
			{
				movedElementTo = listView.SelectedItem as FileElement;
				MoveElementWithChange();
			}
		}

		private void MoveElementWithChange()
		{
			if (IsReadOnly)
			{
				return;
			}

			if (movedElementFrom == null || movedElementTo == null || movedElementFrom == movedElementTo)
			{
				return;
			}

			DirectoryElement newParent = movedElementTo.Parent;
			BindingList<Element> bindingList = (listView.ItemsSource as BindingList<Element>);
			int index = 0;

			if (movedElementTo.Delete())
			{
				index = bindingList.IndexOf(movedElementTo);
				bindingList.Remove(movedElementTo);
			}
			else
			{
				System.Windows.MessageBox.Show("Не вдається перемістити елемент");
				return;
			}

			if (!newParent.IsExists)
			{
				movedElementTo.Restore();
				bindingList.Insert(index, movedElementTo);
				System.Windows.MessageBox.Show("Не вдається перемістити елемент");
				return;
			}

			try
			{
				movedElementFrom.Parent = newParent;
				movedElementFrom.Name = movedElementTo.Name;
			}
			catch
			{
				movedElementTo.Restore();
				bindingList.Insert(index, movedElementTo);
				System.Windows.MessageBox.Show("Не вдається перемістити елемент");
				return;
			}

			int oldIndex = bindingList.IndexOf(movedElementFrom);
			bindingList.Remove(movedElementFrom);
			if (index > oldIndex)
			{
				index = (index - 1) >= 0 ? index - 1 : 0;
			}
			bindingList.Insert(index, movedElementFrom);

			int i = 0;
			while (i < bindingList.Count)
			{
				if (!bindingList[i].IsExists)
				{
					bindingList.RemoveAt(i);
					i--;
				}

				i++;
			}

			movedElementFrom = null;
			movedElementTo = null;
		}

		private void listView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			e.Handled = true;
			var el = ((System.Windows.Controls.ListViewItem)sender).Content as Element;
			ShowFiles(el, null, true);
		}

		private void OpenImage(Element file)
		{
			List<Element> list = (listView.ItemsSource as IEnumerable<Element>).Where(x => x.Type == ElementType.File && IsImage(x.Name)).ToList();
			ImageViewer window = new ImageViewer(list, listView.SelectedItem as FileElement, listView) { Owner = this };
			window.Show();

			list = null;
			GC.Collect();
		}

		private void OpenTextDoc(FileElement file)
		{
			TextWindow window = new TextWindow(file) { Owner = this };
			window.Show();
		}

		private void listView_MouseDown(object sender, MouseButtonEventArgs e)
		{
			listView.SelectedItem = null;
			listView.Focus();
		}

		private void SearchTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				search_text_box.Visibility = Visibility.Hidden;
				listView.Focus();
			}
		}

		private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
		{
			SetFilterElementByName(search_text_box.Text);
		}

		private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if ((Keyboard.Modifiers == ModifierKeys.None) && (((e.Key >= Key.D0) && (e.Key <= Key.Z)) || ((e.Key >= Key.NumPad0) && (e.Key <= Key.NumPad9))))
			{
				if (search_text_box.Visibility == Visibility.Hidden)
				{
					search_text_box.Clear();
				}
				search_text_box.Visibility = Visibility.Visible;
				search_text_box.Focus();
			}

			if (e.Key == Key.F5)
			{
				ShowFiles(listView.Tag as DirectoryElement);
			}

			if (search_text_box.Visibility == Visibility.Hidden)
			{
				if (e.Key == Key.Back)
				{
					if (LastParent != null)
					{
						var el = listView.Tag as Element;
						ShowFiles(LastParent.Parent == null ? LastParent : LastParent.Parent, el);
					}
				}

				if (e.Key == Key.Escape)
				{
					search_text_box.Visibility = Visibility.Hidden;
					ShowFiles(LastParent);
				}
			}

			if ((Keyboard.Modifiers == ModifierKeys.Control) && (e.Key == Key.S))
			{
				SaveSelectedAs();
				return;
			}

			if ((Keyboard.Modifiers == ModifierKeys.Control) && (e.Key == Key.X))
			{
				CutSelected();
				return;
			}

			if ((Keyboard.Modifiers == ModifierKeys.Control) && (e.Key == Key.V))
			{
				Insert_Click(null, null);
				return;
			}

			if ((Keyboard.Modifiers == ModifierKeys.Control) && (e.Key == Key.F))
			{
				DirectoryElement directory = (listView.Tag as DirectoryElement) != null ? (listView.Tag as DirectoryElement) : xdb;
				Finder f = new Finder(directory, ShowList);
				f.Owner = this;
				f.Show();
				return;
			}

			if (e.Key == Key.D && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == (ModifierKeys.Control | ModifierKeys.Alt))
			{
				Bitmap icon;

				if ((listView.SelectedItem is FileElement) && IsImage((listView.SelectedItem as Element).Name))
				{
					MemoryStream ms = new MemoryStream();
					(listView.SelectedItem as FileElement).SaveTo(ms);
					ms.Position = 0;
					Bitmap tmp = new Bitmap(ms);
					icon = ImgConverter.ResizeImage(tmp, THUMBNAIL_SIZE);
					tmp?.Dispose();
					ms.Dispose();
				}
				else
				{
					icon = (listView.SelectedItem as Element).Icon;
				}

				ShowList(xdb.FindAllByIcon(icon, 0));

				return;
			}

			if ((Keyboard.Modifiers == ModifierKeys.Control) && (e.Key == Key.D))
			{
				CreateNewDir();
				return;
			}
		}

		private void InsertImageFromClipboard(bool Refresh)
		{
			if ((!editable) || (!System.Windows.Clipboard.ContainsImage()))
			{
				return;
			}

			Element newFile = null;
			ushort maxTries = 3;

			do
			{
				try
				{
					Bitmap tmp = ImgConverter.BitmapFromSource(System.Windows.Clipboard.GetImage());
					using (MemoryStream stream = new MemoryStream())
					{
						//tmp1 через який баг в GDI, не хоче працювати з tmp
						Bitmap tmp1 = new Bitmap(tmp);
						tmp1.Save(stream, ImageFormat.Jpeg);
						tmp1?.Dispose();
						Bitmap icon = ImgConverter.ResizeImage(tmp, THUMBNAIL_SIZE);
						string FileName = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss.fff") + ".jpg";

						if (isCompareImage)
						{
							var duplicates = GetDuplicates(FileName, icon);
							if (duplicates.Count > 0)
							{
								ShowDuplicateFilesList(duplicates, FileName, icon);
								icon?.Dispose();
								tmp?.Dispose();
								return;
							}
						}

						stream.Position = 0;
						try
						{
							newFile = (listView.Tag as DirectoryElement)?.AddFile(stream, FileName, false, icon);
						}
						catch (Exception e)
						{
							Refresh = false;
							System.Windows.MessageBox.Show(e.Message);

						}
						icon?.Dispose();
						maxTries = 0;
					}
					tmp?.Dispose();
				}
				catch (ExternalException)
				{
					maxTries--;
				}
			} while (maxTries > 0);

			if (Refresh && newFile != null)
			{
				ShowFiles(listView.Tag as Element, newFile);
			}

			if (clipboardClean_CheckBox.IsChecked == true)
			{
				System.Windows.Clipboard.Clear();
			}
		}

		private void FindAndSelect(string name)
		{
			if (listView.ItemsSource == null)
			{
				return;
			}

			List<Element> list = (listView.ItemsSource as IEnumerable<Element>).Where(x => x.Name.ToLower().IndexOf(name.ToLower()) >= 0).ToList();
			listView.SelectedItems.Clear();

			if (list.Count > 0)
			{
				SelectItem(list[0]);
				listView.ScrollIntoView(list[0]);
			}

			foreach (var item in list)
			{
				listView.SelectedItems.Add(item);
			}
		}

		private void SetFilterElementByName(string name)
		{
			DirectoryElement parent = (listView.Tag as DirectoryElement);

			if ((xdb == null) || (parent == null))
			{
				if (listView.ItemsSource == null)
				{
					return;
				}
			}

			List<Element> list;

			if (parent != null)
			{
				list = parent.Elements.Where(x => x.Name.ToLower().IndexOf(name.ToLower()) >= 0).ToList();
			}
			else
			{
				list = temp_search_list.Where(x => x.Name.ToLower().IndexOf(name.ToLower()) >= 0).ToList();
			}

			listView.ItemsSource = OrderBy(list);

			ShowInfo();
		}

		private void CutSelected()
		{
			if (IsReadOnly)
			{
				return;
			}

			if (!editable)
			{
				//return;
			}

			CutList.Clear();
			foreach (var item in listView.SelectedItems)
			{
				CutList.Add(item as Element);
			}
		}

		private void InsertCut()
		{
			if (IsReadOnly)
			{
				return;
			}

			if (!editable)
			{
				//return;
			}

			DirectoryElement newParent = (listView.Tag as DirectoryElement);

			foreach (var item in CutList)
			{
				try
				{
					item.Parent = newParent;
				}
				catch (Exception e)
				{
                    System.Windows.MessageBox.Show(e.Message);
				}
			}

			if (CutList.Count > 0)
			{
				CutList.Clear();
				ShowFiles(newParent);
			}
		}

		private void SaveSelectedAs(bool execute = false)
		{
			if (listView.SelectedItems.Count == 0)
			{
				return;
			}

			List<Element> ExportElements = listView.SelectedItems.Cast<Element>().ToList();

			if (listView.SelectedItems.Count == 1)
			{
				SaveAs(ExportElements[0], execute);
				return;
			}

			using (var fbd = new FolderBrowserDialog())
			{
				if ((!FileExportWorker.IsBusy) && (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath)))
				{
					FileExportWorker.RunWorkerAsync(new ExportInfo(ExportElements, fbd.SelectedPath, false, false));
				}
			}
		}

		public static string GetRandomName(int length)
		{
			byte[] buf = new byte[length];

			RandomHelper.GetBytes(buf);
			return BitConverter.ToString(buf).Replace("-", String.Empty).Substring(0, length);
		}

		public static string GetRandomName(string file_ext = "")
		{
			int length = (int)RandomHelper.Random(64) + 32;
			return GetRandomName(length) + file_ext;
		}

		private void SaveAs(Element element, bool execute = false)
		{
			string element_name = element.Name;
			if (!save_real_file_name)
			{
				element_name = GetRandomName();
			}

			SaveFileDialog sd = new SaveFileDialog
			{
				FileName = element_name,
				DefaultExt = Path.GetExtension(element.Name)
			};

			if (element.Type == ElementType.File)
			{
				sd.Filter = "File (*." + sd.DefaultExt.ToUpper() + ")|*." + sd.DefaultExt;
			}
			else
			{
				sd.Filter = "Directory|*.*";
			}

			if ((!FileExportWorker.IsBusy) && (sd.ShowDialog() == System.Windows.Forms.DialogResult.OK))
			{
				FileExportWorker.RunWorkerAsync(new ExportInfo(new List<Element>() { element }, sd.FileName, true, execute));
			}
		}

		private void MenuItem_Click(object sender, RoutedEventArgs e)
		{
			CutSelected();
		}

		public void Rename()
		{
			if (IsReadOnly)
			{
				return;
			}

			if (listView.SelectedItem == null)
			{
				return;
			}

			Element element = listView.SelectedItem as Element;

			RenameWindow renamer = new RenameWindow(element.Name, element.Type) { Owner = this };
			if (renamer.ShowDialog() == true)
			{
				try
				{
					element.Name = renamer.textBox.Text;
				}
				catch (Exception ex)
				{
					System.Windows.MessageBox.Show(ex.Message);
					return;
				}
			}
		}

		private void DeleteSelected()
		{
			if (IsReadOnly)
			{
				return;
			}

			if (!editable)
			{
				//return;
			}

			if (System.Windows.MessageBox.Show("Удалить елемент", "Удаление", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
			{
				return;
			}

			List<Element> itemsToRemove = listView.SelectedItems.Cast<Element>().ToList();
			for (int i = 0; i < itemsToRemove.Count(); i++)
			{
				DeleteElement(itemsToRemove[i]);
			}

			var bindingList = (listView.ItemsSource as BindingList<Element>);
			int j = 0;
			while (j < bindingList.Count)
			{
				if (!bindingList[j].IsExists)
				{
					bindingList.RemoveAt(j);
					j--;
				}

				j++;
			}

			ShowInfo();
		}

		public bool DeleteElement(Element element)
		{
			if (element.Delete())
			{
				(listView.ItemsSource as BindingList<Element>).Remove(element);
				return true;
			}

			return false;
		}

		private void OpenOrCreateCDB()
		{
			_faildedOpen = false;

			if ((databaseFile == "") || (!editable))
			{
				return;
			}

			PassWindow pass = new PassWindow();
			pass.Owner = this;
			if (pass.ShowDialog() != true)
			{
				return;
			}

			password = pass.Password;
			if (!xdbLoadWorker.IsBusy)
			{
				xdbLoadWorker.RunWorkerAsync(password);
			}
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			OpenOrCreateCDB();
		}

		private void FindDuplicate()
		{
			Bitmap icon;

			if ((listView.SelectedItem is FileElement) && IsImage((listView.SelectedItem as Element).Name))
			{
				MemoryStream ms = new MemoryStream();
				(listView.SelectedItem as FileElement).SaveTo(ms);
				ms.Position = 0;
				Bitmap tmp = new Bitmap(ms);
				icon = ImgConverter.ResizeImage(tmp, THUMBNAIL_SIZE);
				tmp?.Dispose();
				ms.Dispose();
			}
			else
			{
				icon = (listView.SelectedItem as Element).Icon;
			}

			DirectoryElement directory = (listView.Tag as DirectoryElement) != null ? (listView.Tag as DirectoryElement) : xdb;
			Finder f = new Finder(directory, icon, ShowList);
			icon = null;
			f.Owner = this;
			f.Show();
		}

		private void MenuItem_Click_1(object sender, RoutedEventArgs e)
		{
			FindDuplicate();
		}

		private void CreateNewDir()
		{
			RenameWindow newdir = new RenameWindow();
			newdir.Owner = this;
			newdir.Title = "Новая папка";
			newdir.label.Content = "Имя папки";
			if ((newdir.ShowDialog() == true) && (newdir.textBox.Text != ""))
			{
				DirectoryElement newDir;
				try
				{
					newDir = (listView.Tag as DirectoryElement).CreateDirectory(newdir.textBox.Text);
				}
				catch (Exception ex)
				{
					System.Windows.MessageBox.Show(ex.Message);
					return;
				}

				ShowFiles(listView.Tag as Element, newDir);
			}
		}

		private void MenuItem_CreateNewDir(object sender, RoutedEventArgs e)
		{
			CreateNewDir();
		}

		private void MenuItem_CreateNewTextFile(object sender, RoutedEventArgs e)
		{
			if ((listView.Tag as Element) != null)
			{

				RenameWindow newDoc = new RenameWindow();// ".txt", ElementType.File);
				newDoc.Owner = this;
				newDoc.Title = "Новый текстовый документ";
				newDoc.label.Content = "Имя файла";
				if ((newDoc.ShowDialog() == true) && (newDoc.textBox.Text != ""))
				{
					Stream ms = Stream.Null;
					Bitmap bmp = ImgConverter.GetIcon(".txt", THUMBNAIL_SIZE);
					FileElement newTextFile = null;
					try
					{
						newTextFile = (listView.Tag as DirectoryElement).AddFile(ms, newDoc.textBox.Text + ".txt", false, bmp);
					}
					catch (Exception ex)
					{
						System.Windows.MessageBox.Show(ex.Message);
					}
					bmp?.Dispose();
					ShowFiles(listView.Tag as Element, newTextFile);
				}
			}
		}

		private void RefreshPathPanel(Element Current)
		{
			List<System.Windows.Controls.Button> buttons = new List<System.Windows.Controls.Button>();
			Element element = Current;
			while (element != null)
			{
				System.Windows.Controls.Button button = new System.Windows.Controls.Button();
				button.Tag = element;
				button.Content = element.Name;
				button.Focusable = false;
				button.Padding = new Thickness(10, 0, 10, 0);
				button.Width = Double.NaN;
				button.MinWidth = 40;
				button.Click += PathButton_Click;
				buttons.Add(button);
				element = element.Parent;
			}

			pathPanel.Children.Clear();
			buttons[buttons.Count - 1].Content = "Root";
			for (int i = buttons.Count - 1; i >= 0; i--)
			{
				pathPanel.Children.Add(buttons[i]);
			}

			buttons.Clear();
		}

		private void PathButton_Click(object sender, RoutedEventArgs e)
		{
			ShowFiles((sender as System.Windows.Controls.Button).Tag as Element);
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			cl.Dispose();
		}

		private void AutoInsert(object sender, RoutedEventArgs e)
		{
			if ((sender as System.Windows.Controls.CheckBox).IsChecked == true)
			{
				cl.ClipboardChanged += ClipboardChange;
			}
			else
			{
				cl.ClipboardChanged -= ClipboardChange;
			}
		}

		private void CompareFileHash_Click(object sender, RoutedEventArgs e)
		{
			isCompareHash = (sender as System.Windows.Controls.CheckBox).IsChecked == true;
		}

		private void CompareImagePHash_Click(object sender, RoutedEventArgs e)
		{
			isCompareImage = (sender as System.Windows.Controls.CheckBox).IsChecked == true;
		}

		private void DuplicateSensative_Change(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			imgDuplicateSensative = (byte)(sender as Slider).Value;
		}

		private void Sort_Click(object sender, RoutedEventArgs e)
		{
			bool sortDirect = ((sender as System.Windows.Controls.RadioButton).Tag as bool?) == true;
			(sender as System.Windows.Controls.RadioButton).Tag = !sortDirect;

			var elements = new BindingList<Element>(listView.ItemsSource as IList<Element>);
            SetViewsElement((listView.Tag as DirectoryElement), OrderBy(elements));
		}

		private void Insert_Click(object sender, RoutedEventArgs e)
		{
			if (CutList.Count > 0)
			{
				InsertCut();
			}
			else
			{
				InsertImageFromClipboard(true);
			}
		}

		private void SaveRealFileName_Click(object sender, RoutedEventArgs e)
		{
			save_real_file_name = (sender as System.Windows.Controls.CheckBox).IsChecked == true;
		}

		private void MenuItem_Info(object sender, RoutedEventArgs e)
		{

		}

		private void search_text_box_LostFocus(object sender, RoutedEventArgs e)
		{
			search_text_box.Visibility = Visibility.Hidden;
		}

		private void ExportButton_Click(object sender, RoutedEventArgs e)
		{
			if (xdb == null)
			{
				return;
			}

			OpenFileDialog op = new OpenFileDialog();
			op.DefaultExt = ".cdb";
			op.Filter = "Crypto database (*.CDB)|*.cdb";
			op.CheckFileExists = false;
			if (op.ShowDialog() != System.Windows.Forms.DialogResult.OK)
			{
				return;
			}

			string password = GetUserPassword("Введите новый пароль");

			if (password != null)
			{
				xdb.ExportStructToFile(op.FileName, password);
			}
		}

		private void mainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.XButton1)
			{
				if (LastParent != null)
				{
					var selectedElement = listView.Tag as Element;
					var parent = LastParent.Parent == null ? LastParent : LastParent.Parent;
					ShowFiles(parent, selectedElement);
				}
			}
		}

		private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string password = GetUserPassword("Введите новый пароль");
				if (password != null)
				{
					xdb.ChangePassword(password);
				}
			}
			catch (UnsupportedMethodException)
			{
				System.Windows.MessageBox.Show("You can't change the password. Unsupported method");
			}
			catch (Exception exception)
			{
				System.Windows.MessageBox.Show(exception.Message);
			}
		}
	}

	class ExportInfo
	{
		public IList<Element> Elements { get { return _Elements.AsReadOnly(); } }
		public List<Element> _Elements;
		public bool SaveAs { get { return _SaveAs; } }
		private bool _SaveAs;
		public string FileName { get { return _NewName; } }
		private string _NewName;
		public bool RunAfterCompleted { get { return _RunAfterCompleted; } }
		private bool _RunAfterCompleted;

		public ExportInfo(List<Element> list, string Name, bool SaveAs, bool RunAfterCompleted)
		{
			_NewName = Name;
			_SaveAs = SaveAs;
			_Elements = list;
			_RunAfterCompleted = RunAfterCompleted;
		}
	}
}