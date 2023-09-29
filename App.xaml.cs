using AppDriverAxxon;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

namespace AppDriverSample
{
    /// <summary>
    /// Logique d'interaction pour App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static bool closingState = false;
        void App_Startup(object sender, StartupEventArgs e)
        {
            // Application is running
            MainWindow mainWindow = new MainWindow();
            //TraceWindow traceWindow = new TraceWindow();
            //window1.Show();
            mainWindow.Show();
        }
    }
}
