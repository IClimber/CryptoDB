using CryptoDataBase.CryptoContainer.Comparers;
using CryptoDataBase.CryptoContainer.Exceptions;
using CryptoDataBase.CryptoContainer.Helpers;
using CryptoDataBase.CryptoContainer.Models;
using CryptoDataBase.CryptoContainer.Repositories;
using CryptoDataBase.CryptoContainer.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDataBase.CryptoContainer
{
    class CryptoContainer : DirectoryElement, IDisposable
    {
        public const byte CurrentVersion = 5;

        public readonly bool IsReadOnly = true;
        private AesCryptoServiceProvider _aes;
        private HeaderRepository _headerRepository;
        private FileStream _headersFileStream;
        private FileStream _dataFileStream;

        public CryptoContainer(string fileName, string password, HeaderRepository.ProgressCallback progress = null)
        {
            progress?.Invoke(0, "Creating AES key");

            AddElementLocker = new object();
            ChangeElementsLocker = new object();

            string dataFilePath = Path.GetDirectoryName(fileName) + "\\" + Path.GetFileNameWithoutExtension(fileName) + ".Data";

            try
            {
                bool writeVersion = !File.Exists(fileName);
                _headersFileStream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                _dataFileStream = new FileStream(dataFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                if (writeVersion)
                {
                    _headersFileStream.WriteByte(CurrentVersion);
                }

                IsReadOnly = false;
            }
            catch
            {
                if (_headersFileStream != null)
                {
                    _headersFileStream.Close();
                }

                _headersFileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                _dataFileStream = new FileStream(dataFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                IsReadOnly = true;
            }

            byte version = ReadVersion(_headersFileStream);

            try
            {
                _headerRepository = HeaderRepositoryFactory.GetRepositoryByVersion(version, _headersFileStream, password);
            }
            catch (Exception exception)
            {
                _headersFileStream.Close();
                _dataFileStream.Close();

                throw exception;
            }

            _aes = _headerRepository.GetDek();
            DataRepository = new DataRepository(_dataFileStream, _aes);
            Header = new Header(_headerRepository, ElementType.Directory);

            try
            {
                ReadFileStruct(progress);
            }
            catch (Exception)
            {
                throw new ReadingDataException("Wrong password");
            }
        }

        public void ExportStructToFile(string fileName, string password)
        {
            var stream = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            var repository = HeaderRepositoryFactory.GetRepositoryByVersion(CurrentVersion, stream, password, _aes.Key);
            List<Element> allElements = new List<Element>();
            AddElementsToList(Elements, allElements);
            allElements.Sort(new TimeComparer());
            repository.ExportStructToFile(allElements);
            allElements.Clear();
            stream.Close();
        }

        public bool CanChangePassword()
        {
            return _headerRepository.CanChangePassword();
        }

        public void ChangePassword(string newPassword)
        {
            _headerRepository.ChangePassword(newPassword);
        }

        private void AddElementsToList(IList<Element> inputElementsList, List<Element> outputElementsList)
        {
            outputElementsList.AddRange(inputElementsList);

            foreach (Element element in inputElementsList)
            {
                if (element is DirectoryElement)
                {
                    AddElementsToList((element as DirectoryElement).Elements, outputElementsList);
                }
            }
        }

        private byte ReadVersion(Stream stream)
        {
            stream.Position = 0;
            byte[] buf = new byte[1];
            stream.Read(buf, 0, 1);

            return buf[0];
        }

        private void ReadFileStruct(HeaderRepository.ProgressCallback progress)
        {
            Dictionary<ulong, DirectoryElement> directories = new Dictionary<ulong, DirectoryElement>();
            Dictionary<ulong, List<Element>> elements = new Dictionary<ulong, List<Element>>();
            directories.Add(Id, this);

            List<Header> headers = _headerRepository.ReadFileStruct(progress);
            int index = 0;
            double percent = 0;
            int lastProgress = 0;
            foreach (Header header in headers)
            {
                AddElementByHeader(directories, elements, header);
                index++;

                percent = index / (double)headers.Count * 100.0;
                if ((progress != null) && (lastProgress != (int)percent))
                {
                    progress(percent, "Parsing elements");
                    lastProgress = (int)percent;
                }
            }

            FillParents(directories, elements, progress);
            elements.Clear();
            directories.Clear();
        }

        private void AddElementByHeader(Dictionary<ulong, DirectoryElement> directoriesList, Dictionary<ulong, List<Element>> elementList, Header header)
        {
            Element element = null;
            if (header.ElementType == ElementType.File)
            {
                element = new FileElement(header, DataRepository, AddElementLocker, ChangeElementsLocker);
            }
            else if (header.ElementType == ElementType.Directory)
            {
                element = new DirectoryElement(header, DataRepository, AddElementLocker, ChangeElementsLocker);
            }

            List<Element> foundElement;
            if (!elementList.TryGetValue(element.ParentId, out foundElement))
            {
                foundElement = new List<Element>();
                elementList.Add(element.ParentId, foundElement);
            }

            foundElement.Add(element);

            if (element is DirectoryElement)
            {
                var dirElement = element as DirectoryElement;
                directoriesList.Add(dirElement.Id, dirElement);
            }

            if ((element is FileElement) && ((element as FileElement).Size > 0))
            {
                DataRepository.RemoveFreeSpace((element as FileElement).FileStartPos, MathHelper.GetMod16((element as FileElement).Size));
            }

            if (element.IconSize > 0)
            {
                DataRepository.RemoveFreeSpace(element.IconStartPosition, MathHelper.GetMod16(element.IconSize));
            }
        }

        private void FillParents(Dictionary<ulong, DirectoryElement> directoriesList, Dictionary<ulong, List<Element>> elementList, HeaderRepository.ProgressCallback progress)
        {
            int index = 0;
            int count = directoriesList.Count;
            int lastProgress = 0;

            foreach (var directory in directoriesList.Values)
            {
                if (elementList.TryGetValue(directory.Id, out List<Element> foundElements))
                {
                    foreach (var element in foundElements)
                    {
                        element.SetVirtualParent(directory);
                    }

                    index++;

                    double percent = index / (double)count * 100.0;
                    if ((progress != null) && (lastProgress != (int)percent))
                    {
                        progress(percent, "Creating elements structure");
                        lastProgress = (int)percent;
                    }
                }
            }
        }

        public void Dispose()
        {
            DataRepository.Dispose();
            _headerRepository.Dispose();
        }
    }
}