using CryptoDataBase.CryptoContainer.Models;
using System.Collections.Generic;
using System.IO;

namespace CryptoDataBase
{
    class FileItem
	{
		public DirElement parentElement;
		public List<FileItem> children = new List<FileItem>();
		public string name;
		public FileItemType type;

		//public uint SubFilesCount { get { return _fileCount; } }
		private uint _fileCount = 0;

		public FileItem(string file, DirElement parentElement = null) : this(file, parentElement, 0)
		{

		}

		private FileItem(string file, DirElement parentElement = null, uint count = 0)
		{
			this.parentElement = parentElement;
			name = file;
			type = FileItemType.File;

			if (Directory.Exists(file))
			{
				type = FileItemType.Dir;

				List<string> items = new List<string>();
				items.AddRange(Directory.GetDirectories(file));
				string[] files = Directory.GetFiles(file);
				items.AddRange(files);

				_fileCount = (uint)files.Length;

				foreach (string item in items)
				{
					children.Add(new FileItem(item, parentElement));
				}
			}
		}

		public int SubFilesCount()
		{
			return _SubFilesCount(this, 0);
		}

		private int _SubFilesCount(FileItem fileItem, int count)
		{
			if (fileItem.type == FileItemType.Dir)
			{
				foreach (var item in children)
				{
					count += item.SubFilesCount();
				}
			}
			else
			{
				count++;
			}

			return count;
		}
	}

	public enum FileItemType
	{
		File = 0,
		Dir = 1
	}
}
