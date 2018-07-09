using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace CryptoDataBase
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
	{
		const string FILE_EXTENSION = ".CDB";

		private void Application_Startup(object sender, StartupEventArgs e)
		{
			Associate();
			MainWindow window = new MainWindow(e.Args.Length > 0 ? e.Args[0] : "");
			window.Show();
		}

		public static void Associate()
		{
			try
			{
				Registry.ClassesRoot.CreateSubKey(FILE_EXTENSION).SetValue("", "CDBfile");

				using (RegistryKey key = Registry.ClassesRoot.CreateSubKey("CDBfile"))
				{
					key.CreateSubKey("DefaultIcon").SetValue("", System.Reflection.Assembly.GetExecutingAssembly().Location + ",0");
					key.CreateSubKey(@"Shell\Open\Command").SetValue("", System.Reflection.Assembly.GetExecutingAssembly().Location + " \"%1\"");
				}
			}
			catch
			{ }
		}
	}
}
