﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using System.ComponentModel;
using System.Drawing;
using ImageConverter;
using System.Windows.Forms;
using System.Drawing.Imaging;
using ClipboardAssist;

namespace CryptoDataBase
{
	/// <summary>
	/// Логика взаимодействия для MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private static string[] ImageExtensions = new string[] { ".bmp", ".jpg", ".jpeg", ".png", ".gif", ".ico" };
		private static string[] TextExtensions = new string[] { ".txt", ".sql", ".pas", ".cs", ".ini", ".log" };
		private List<Element> CutList = new List<Element>();
		XDB xdb;
		string password;
		string databaseFile;
		public const int thumbnailSize = 200;
		private BackgroundWorker xdbLoadWorker = new BackgroundWorker();
		private BackgroundWorker FileLoadWorker = new BackgroundWorker();
		private BackgroundWorker FileExportWorker = new BackgroundWorker();
		Stopwatch sw = new Stopwatch();
		List<FileItem> addedFilesList = new List<FileItem>();
		Element LastParent;
		private int AddedFilesCount;
		public bool editable = true;
		private string FindText = "";
		private bool isFind = false;
		Stopwatch FindedTimer = new Stopwatch();
		ClipboardMonitor cl = new ClipboardMonitor();
		bool isCompareHash = false;
		bool isCompareImage = false;
		byte imgDuplicateSensative = 0;
		public bool ShowDuplicateMessage
		{
			get { return _ShowDuplicateMessage; }
			set { _ShowDuplicateMessage = value; }
		}
		public bool _ShowDuplicateMessage = true;

		private void MoveTest()
		{
			Random r = new Random();
			List<Element> dirs = (xdb.Elements as IEnumerable<Element>).Where(x => x.Type == ElementType.Dir).ToList();
			foreach (var dir in dirs)
			{
				for (int i = 0; i < dir.Elements.Count; i++)
				{
					if (dir.Elements[i].Type == ElementType.File)
					{
						dir.Elements[i].Parent = dirs[r.Next(dirs.Count)];
					}
				}
			}
		}

		private void RenameTest()
		{
			Random r = new Random();
			List<Element> dirs = (xdb.Elements as IEnumerable<Element>).Where(x => x.Type == ElementType.Dir).ToList();
			foreach (var dir in dirs)
			{
				for (int i = 0; i < dir.Elements.Count; i++)
				{
					if (dir.Elements[i].Type == ElementType.File)
					{
						dir.Elements[i].Name = CryptoRandom.Random(UInt64.MaxValue - 2).ToString();
					}
				}
			}
		}

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
		}

		private void ClipboardChange(object sender, ClipboardChangedEventArgs e)
		{
			InsertImageFromClipboard(false);
		}

		public static bool IsImage(string FileName)
		{
			return ImageExtensions.Contains(Path.GetExtension(FileName).ToLower());
		}

		public static bool IsText(string FileName)
		{
			return TextExtensions.Contains(System.IO.Path.GetExtension(FileName).ToLower());
		}

		private void CountFilesInDir(string dir, ref int count)
		{
			count += Directory.GetFiles(dir).Length;
			foreach (var directory in Directory.GetDirectories(dir))
			{
				CountFilesInDir(directory, ref count);
			}
		}

		private int GetCountSubElements(Element root)
		{
			int result = root.Elements.Count;

			foreach (var item in root.Elements)
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
						duplicates = xdb.FindByHash(Crypto.GetMD5(fs));
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
			this.Dispatcher.Invoke(() => duplicateWindow = new DuplicatesWindow(thumbnail, mainFileName, duplicates) { Owner = this });
			this.Dispatcher.Invoke(() => duplicateWindow.ShowDialog());
		}

		private void AddFile(FileItem item)
		{
			if (item.parentElement.FileExists(Path.GetFileName(item.name)))
			{
				return;
			}

			if (item.type == FileItemType.File)
			{
				Bitmap bmp = ImgConverter.GetIcon(item.name, thumbnailSize);
				var duplicates = GetDuplicates(item.name, bmp);
				if (duplicates.Count > 0)
				{
					ShowDuplicateFilesList(duplicates, item.name, bmp);
					bmp?.Dispose();
					return;
				}

				item.parentElement.AddFile(item.name, Path.GetFileName(item.name), false, bmp, ReportProgress);
				bmp?.Dispose();

				AddedFilesCount++;
			}
			else
			{
				Element newParent = item.parentElement.CreateDir(Path.GetFileName(item.name));

				foreach (FileItem sub_item in item.children)
				{
					sub_item.parentElement = newParent;
					AddFile(sub_item);
				}
			}
		}

		private void AddFiles(List<FileItem> files)
		{
			sw = Stopwatch.StartNew();

			for (int i = 0; i < files.Count; i++)
			{
				AddFile(files[i]);
			}
		}

		private void Files_Drop(object sender, System.Windows.DragEventArgs e)
		{
			Element parent = (Element)listView.Tag;
			if ((xdb == null) || (parent == null))
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
			}
		}

		#region AddFiles worker
		private void FileLoad(object sender, DoWorkEventArgs e)
		{
			if (!editable)
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

			ShowFiles((Element)listView.Tag);
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
					(e.Argument as ExportInfo).Elements[0]?.SaveAs((e.Argument as ExportInfo).FileName, ReportProgress1);
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
				element?.SaveTo((e.Argument as ExportInfo).FileName, ReportProgress1);
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
			sw.Start();
			if (databaseFile == "")
			{
				return;
			}

			xdb = new XDB(databaseFile, e.Argument.ToString(), PerortProgress);
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
			sw.Stop();
			TextBlockStatus1.Text = sw.ElapsedMilliseconds.ToString() + "ms";
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

			Window_Loaded(null, null);
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

		private void ShowFiles(Element element, Element selected = null)
		{
			if (element == null)
			{
				return;
			}

			//Відкриваємо папку
			if (element.Type == ElementType.Dir)
			{
				RefreshPathPanel(element);
				listView.ItemsSource = OrderBy(element.Elements);// new BindingList<Element>(element.Elements.OrderByDescending(x => x.Type).ToList());

				if (selected != null)
				{
					listView.ScrollIntoView(selected);
					listView.SelectedItem = selected;
				}
				else if (element.Elements.Count > 0) //Якщо в папці є елементи, то прокручуємо на початок
				{
					listView.ScrollIntoView((listView.ItemsSource as BindingList<Element>)[0]);
				}
				LastParent = element;
				listView.Tag = element;
				listView.Focus();
				ShowInfo();
				return;
			}

			//Відкриваємо файл
			if (IsImage(element.Name))
			{
				OpenImage(element);
			}
			else if (IsText(element.Name))
			{
				OpenTextDoc(element);
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
			listView.ItemsSource = OrderBy(elements);// new BindingList<Element>(elements.OrderByDescending(x => x.Type).ToList());
			if ((listView.ItemsSource as BindingList<Element>).Count > 0)
			{
				listView.ScrollIntoView((listView.ItemsSource as BindingList<Element>)[0]);
			}
			listView.Tag = null;

			ShowInfo();
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
					ShowFiles((Element)listView.SelectedItem);
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
				OpenParentDir((Element)listView.SelectedItem);
			}
		}

		private void listView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			e.Handled = true;
			var el = ((System.Windows.Controls.ListViewItem)sender).Content as Element;
			ShowFiles(el);
		}

		private void OpenImage(Element file)
		{
			List<Element> list = (listView.ItemsSource as IEnumerable<Element>).Where(x => x.Type == ElementType.File && IsImage(x.Name)).ToList();// ((Element)listView.Tag).Elements.Where(x => x.Type == ElementType.File && isImage(x.Name)).ToList<Element>();
			ImageViewer window = new ImageViewer(list, (Element)listView.SelectedItem, listView) { Owner = this };
			window.Show();
		}

		private void OpenTextDoc(Element file)
		{
			TextWindow window = new TextWindow(file) { Owner = this };
			window.Show();
		}

		private void listView_MouseDown(object sender, MouseButtonEventArgs e)
		{
			listView.SelectedItem = null;
			listView.Focus();
			isFind = false;
		}

		private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (!((e.Key >= Key.D0) && (e.Key <= Key.Z)) && !((e.Key >= Key.NumPad0) && (e.Key <= Key.NumPad9)))
			{
				isFind = false;
			}

			if (e.Key == Key.F5)
			{
				ShowFiles((Element)listView.Tag);
			}

			if (e.Key == Key.Back)
			{
				if (LastParent != null)
				{
					var el = (Element)listView.Tag;
					ShowFiles(LastParent.Parent == null ? LastParent : LastParent.Parent);
					SelectItem(el);
				}
			}

			if (e.Key == Key.Escape)
			{
				isFind = false;
				ShowFiles(LastParent);
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
				Finder f = new Finder(xdb, ShowList);
				f.Owner = this;
				f.Show();
				return;
			}

			if ((Keyboard.Modifiers == ModifierKeys.Control) && (e.Key == Key.D))
			{
				CreateNewDir();
				return;
			}

			if ((Keyboard.Modifiers == ModifierKeys.None) && (((e.Key >= Key.D0) && (e.Key <= Key.Z)) || ((e.Key >= Key.NumPad0) && (e.Key <= Key.NumPad9))))
			{
				isFind = FindedTimer.ElapsedMilliseconds > 3000 ? false : isFind;
				FindedTimer.Restart();
				FindText = isFind ? FindText : "";
				isFind = true;
				FindText += e.Key.ToString()[e.Key.ToString().Length - 1];
				FindAndSelect(FindText);
			}
		}

		private void InsertImageFromClipboard(bool Refresh)
		{
			if ((!editable) || (!System.Windows.Clipboard.ContainsImage()))
			{
				return;
			}

			Element newFile;
			Bitmap tmp = ImgConverter.BitmapFromSource(System.Windows.Clipboard.GetImage());
			using (MemoryStream stream = new MemoryStream())
			{
				//tmp1 через який баг в GDI, не хоче працювати з tmp
				Bitmap tmp1 = new Bitmap(tmp);
				tmp1.Save(stream, ImageFormat.Jpeg);
				tmp1?.Dispose();
				Bitmap icon = ImgConverter.ResizeImage(tmp, thumbnailSize);
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
				newFile = (listView.Tag as Element)?.AddFile(stream, FileName, false, icon);
				icon?.Dispose();
			}
			tmp?.Dispose();

			if (Refresh)
			{
				ShowFiles((Element)listView.Tag, newFile);
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

		private void CutSelected()
		{
			if (!editable)
			{
				//return;
			}

			CutList.Clear();
			foreach (var item in listView.SelectedItems)
			{
				CutList.Add((Element)item);
			}
		}

		private void InsertCut()
		{
			if (!editable)
			{
				//return;
			}

			Element newParent = (Element)listView.Tag;

			foreach (var item in CutList)
			{
				item.Parent = newParent;
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

		private void SaveAs(Element element, bool execute = false)
		{
			SaveFileDialog sd = new SaveFileDialog
			{
				FileName = element.Name,
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
			if ((listView.SelectedItem == null) || (!editable))
			{
				return;
			}

			Element element = (Element)listView.SelectedItem;

			RenameWindow renamer = new RenameWindow(element.Name, element.Type) { Owner = this };
			if (renamer.ShowDialog() == true)
			{
				element.Name = renamer.textBox.Text;
			}
		}

		private void DeleteSelected()
		{
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

			ShowInfo();

			/*if (listView.SelectedItems.Count > 0)
			{
				ShowFiles((Element)listView.Tag);
			}*/
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

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
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

		private void FindDuplicate()
		{
			Bitmap icon;

			if (IsImage(((Element)listView.SelectedItem).Name))
			{
				MemoryStream ms = new MemoryStream();
				((Element)listView.SelectedItem).SaveTo(ms);
				ms.Position = 0;
				Bitmap tmp = new Bitmap(ms);
				icon = ImgConverter.ResizeImage(tmp, thumbnailSize);
				tmp?.Dispose();
				ms.Dispose();
			}
			else
			{
				icon = ((Element)listView.SelectedItem).Icon;
			}

			Finder f = new Finder(xdb, icon, ShowList);
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
				var newDir = ((Element)listView.Tag).CreateDir(newdir.textBox.Text);
				ShowFiles(((Element)listView.Tag));
				SelectItem(newDir);
			}
		}

		private void MenuItem_CreateNewDir(object sender, RoutedEventArgs e)
		{
			CreateNewDir();
		}

		private void MenuItem_CreateNewTextFile(object sender, RoutedEventArgs e)
		{
			if ((Element)listView.Tag != null)
			{

				RenameWindow newDoc = new RenameWindow();// ".txt", ElementType.File);
				newDoc.Owner = this;
				newDoc.Title = "Новый текстовый документ";
				newDoc.label.Content = "Имя файла";
				if ((newDoc.ShowDialog() == true) && (newDoc.textBox.Text != ""))
				{
					Stream ms = Stream.Null;
					Bitmap bmp = ImgConverter.GetIcon(".txt", thumbnailSize);
					var newTextFile = ((Element)listView.Tag).AddFile(ms, newDoc.textBox.Text + ".txt", false, bmp);
					bmp?.Dispose();
					ShowFiles((Element)listView.Tag);
					SelectItem(newTextFile);
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
			listView.ItemsSource = OrderBy(listView.ItemsSource as IList<Element>);
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