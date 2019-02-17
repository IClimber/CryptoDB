using CryptoDataBase.CDB;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;

namespace CryptoDataBase
{
	/// <summary>
	/// Логика взаимодействия для TextWindow.xaml
	/// </summary>
	public partial class TextWindow : Window
	{
		private const int GWL_STYLE = -16,
					  WS_MAXIMIZEBOX = 0x10000,
					  WS_MINIMIZEBOX = 0x20000;
		private IntPtr _windowHandle;
		FileElement element;
		byte[] textHash;

		[DllImport("user32.dll")]
		private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
		[DllImport("user32.dll")]
		private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

		private void MainWindow_SourceInitialized(object sender, EventArgs e)
		{
			_windowHandle = new WindowInteropHelper(this).Handle;

			//disable minimize button
			HideMinimizeAndMaximizeButtons();
		}

		internal void HideMinimizeAndMaximizeButtons()
		{
			if (_windowHandle == IntPtr.Zero)
				return;//throw new InvalidOperationException("The window has not yet been completely initialized");

			SetWindowLong(_windowHandle, GWL_STYLE, GetWindowLong(_windowHandle, GWL_STYLE) & ~WS_MINIMIZEBOX);
		}

		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				Close();
			}

			if ((Keyboard.Modifiers == ModifierKeys.Control) && (e.Key == Key.S))
			{
				e.Handled = true;
				SaveChanges(false);
				return;
			}
		}

		public TextWindow()
		{
			InitializeComponent();
			this.SourceInitialized += MainWindow_SourceInitialized;
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			SaveChanges(true);
			Owner.Activate();
		}

		public TextWindow(FileElement element) : this()
		{
			this.element = element;
			MemoryStream ms = new MemoryStream();
			element.SaveTo(ms);
			TextRange textRange = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
			ms.Position = 0;
			textRange.Load(ms, DataFormats.Text);
			textRange.ApplyPropertyValue(Paragraph.MarginProperty, new Thickness(0));

			//Получаємо хеш файла
			ms.Position = 0;
			textRange = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
			textRange.Save(ms, DataFormats.Text);
			ms.Position = 0;
			textHash = Crypto.GetMD5(ms);
			ms.Dispose();

			richTextBox.Focus();
		}

		private void SaveChanges(bool ShowRequest)
		{
			TextRange textRange = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
			MemoryStream ms = new MemoryStream();
			textRange.Save(ms, DataFormats.Text);
			ms.Position = 0;
			byte[] newHash = Crypto.GetMD5(ms);
			if (Crypto.CompareHash(textHash, newHash))
			{
				ms.Dispose();
				return;
			}

			if ((ShowRequest) && (MessageBox.Show(this, "Сохранить изменения", "Сохранение", MessageBoxButton.OKCancel) != MessageBoxResult.OK))
			{
				ms.Dispose();
				return;
			}

			ms.Position = 0;
			try
			{
				element.ChangeContent(ms);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
			ms.Dispose();
			textHash = newHash;
		}
	}
}
