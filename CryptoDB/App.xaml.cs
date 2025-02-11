using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace CryptoDataBase
{
	/// <summary>
	/// Логика взаимодействия для App.xaml
	/// </summary>
	public partial class App : Application
	{
		const string FILE_EXTENSION = ".CDB";

        private static List<CultureInfo> m_Languages = new List<CultureInfo>();
        public static event EventHandler LanguageChanged;

        public static List<CultureInfo> Languages
        {
            get
            {
                return m_Languages;
            }
        }

        public App()
        {
            m_Languages.Clear();
            m_Languages.Add(new CultureInfo("en-US"));
            m_Languages.Add(new CultureInfo("uk-UA")); // Default localization

            App.LanguageChanged += App_LanguageChanged;
        }

        private void Application_Startup(object sender, StartupEventArgs e)
		{
			Associate();
			MainWindow window = new MainWindow(e.Args.Length > 0 ? e.Args[0] : "");
			window.Show();
		}

		public static void Associate()
		{
			if (!IsDebugRelease)
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

        public static CultureInfo Language
        {
            get
            {
                return System.Threading.Thread.CurrentThread.CurrentUICulture;
            }
            set
            {
                if (value == null) throw new ArgumentNullException("value");
                if (value == System.Threading.Thread.CurrentThread.CurrentUICulture) return;

                System.Threading.Thread.CurrentThread.CurrentUICulture = value;

                ResourceDictionary dict = new ResourceDictionary();
                switch (value.Name)
                {
                    case "uk-UA":
                        dict.Source = new Uri(String.Format("Resources/lang.{0}.xaml", value.Name), UriKind.Relative);
                        break;
                    default:
                        dict.Source = new Uri("Resources/lang.en-US.xaml", UriKind.Relative);
                        break;
                }

                ResourceDictionary oldDict = (from d in Application.Current.Resources.MergedDictionaries
                                              where d.Source != null && d.Source.OriginalString.StartsWith("Resources/lang.")
                                              select d).First();
                if (oldDict != null)
                {
                    int ind = Application.Current.Resources.MergedDictionaries.IndexOf(oldDict);
                    Application.Current.Resources.MergedDictionaries.Remove(oldDict);
                    Application.Current.Resources.MergedDictionaries.Insert(ind, dict);
                }
                else
                {
                    Application.Current.Resources.MergedDictionaries.Add(dict);
                }

                LanguageChanged(Application.Current, new EventArgs());
            }
        }

        private void Application_LoadCompleted(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            Language = CryptoDataBase.Properties.Settings.Default.DefaultLanguage;
        }

        private void App_LanguageChanged(Object sender, EventArgs e)
        {
            CryptoDataBase.Properties.Settings.Default.DefaultLanguage = Language;
            CryptoDataBase.Properties.Settings.Default.Save();
        }

        public static bool IsDebugRelease
		{
			get
			{
				#if DEBUG
				return true;
				#else
				return false;
				#endif
			}
		}
	}
}
