using CryptoDataBase.CDB;
using ImageConverter;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

namespace CryptoDataBase
{
	/// <summary>
	/// Логика взаимодействия для PropertiesWindow.xaml
	/// </summary>
	public partial class PropertiesWindow : Window
	{
		Element element;

		public PropertiesWindow()
		{
			InitializeComponent();
		}

		public PropertiesWindow(List<Element> elements) : this()
		{
			Title = "Свойства: ";
			for (int i = 0; i < Math.Min(elements.Count, 3); i++)
			{
				Title += (i < (Math.Min(elements.Count, 3) - 1)) ? elements[i].Name + ", " : elements.Count > 3 ? elements[i].Name + "..." : elements[i].Name;
			}

			ulong data_size = GetSize(elements);
			ParentLabel.Text = GetPath(elements);
			FilesCountLabel.Text = GetFilesCount(elements).ToString("#,0");
			DirsCountLabel.Text = GetDirsCount(elements).ToString("#,0");
			SizeLabel.Text = SizeToStr(data_size) + " (" + data_size.ToString("#,0") + " байт)";

			if (elements.Count == 1)
			{
				element = elements[0];
				Thumbnail.Source = ImgConverter.BitmapToImageSource(element.Icon);
			}
			else
			{
				IconRow.Height = new GridLength(0, GridUnitType.Pixel);
			}
		}

		public static string SizeToStr(ulong size)
		{
			string[] suf = { "байт", "КБ", "МБ", "ГБ", "ТБ" };
			if (size == 0)
				return "0 " + suf[0];
			int place = Convert.ToInt32(Math.Floor(Math.Log(size, 1024)));
			double num = Math.Round(size / Math.Pow(1024, place), 2);
			return (Math.Sign((long)size) * num).ToString() + " " + suf[place];
		}

		private string GetPath(List<Element> elements)
		{
			string path = elements[0].FullPath;
			for (int i = 1; i < elements.Count; i++)
			{
				if (string.Compare(path, elements[i].FullPath, true) != 0)
				{
					path = "разное";
					break;
				}
			}

			return path;
		}

		private int GetFilesCount(List<Element> elements)
		{
			int result = elements.Where(x => x.Type == ElementType.File).Count();

			foreach (DirElement item in elements.Where(x => x.Type == ElementType.Dir))
			{
				result += GetFilesCount(item.Elements.ToList());
			}

			return result;
		}

		private int GetDirsCount(List<Element> elements)
		{
			int result = elements.Where(x => x.Type == ElementType.Dir).Count();

			foreach (DirElement item in elements.Where(x => x.Type == ElementType.Dir))
			{
				result += GetDirsCount(item.Elements.ToList());
			}

			return result;
		}

		private UInt64 GetSize(List<Element> elements)
		{
			UInt64 size = 0;
			for (int i = 0; i < elements.Count; i++)
			{
				size += elements[i].Size;
			}

			return size;
		}

		private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if ((e.Key == Key.Escape) || (e.Key == Key.Back))
			{
				Close();
			}
		}

		private void ChangeIcon_Button_Click(object sender, RoutedEventArgs e)
		{
			if (element == null)
			{
				return;
			}

			OpenFileDialog op = new OpenFileDialog();
			if (op.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				Bitmap bmp = ImgConverter.GetIcon(op.FileName, MainWindow.thumbnailSize);
				element.Icon = bmp;
				Thumbnail.Source = ImgConverter.BitmapToImageSource(bmp);
				bmp?.Dispose();
			}
			op.Dispose();
		}

		private void ClearIcon_Button_Click(object sender, RoutedEventArgs e)
		{
			if (element != null)
			{
				element.Icon = null;
				Thumbnail.Source = null;
			}
		}
	}
}
