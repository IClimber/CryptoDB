using SevenZip;
using System;
using System.IO;

namespace CryptoDataBase.Services
{
	class Archive
	{
		public delegate string GetUserPasswordCallback(string title);

		public static SevenZipExtractor OpenArchive(string fileName, GetUserPasswordCallback getUserPasswordCallback)
		{
			// Toggle between the x86 and x64 bit dll
			string path = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, Environment.Is64BitProcess ? "x64" : "x86", "7z.dll");
			SevenZipBase.SetLibraryPath(path);
			string title = Path.GetFileName(fileName);

			var extractor = new SevenZipExtractor(fileName);
			try
			{
				foreach (var file in extractor.ArchiveFileData)
				{
					if (file.Encrypted)
					{
						extractor = new SevenZipExtractor(fileName, getUserPasswordCallback(title));
						break;
					}
				}
			}
			catch (Exception)
			{
				try
				{
					extractor = new SevenZipExtractor(fileName, getUserPasswordCallback(title));
					var data = extractor.ArchiveFileData;
				}
				catch (Exception exception)
				{
					extractor.Dispose();
					System.Windows.MessageBox.Show(exception.Message);
					throw exception;
				}
			}

			return extractor;
		}
	}
}
