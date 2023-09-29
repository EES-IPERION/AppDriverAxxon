using AppDriverSample;
using Prysm.AppVision.Common;
using Prysm.AppVision.Data;
using Prysm.AppVision.SDK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AppDriverAxxon
{
    /// <summary>
    /// Logique d'interaction pour Window1.xaml
    /// </summary>
    public partial class TraceWindow : Window
    {

        public static AppServerForDriver _appServer;
        string _serverHostname = "";
        public static VariableRow _variableProtocol;
        public static TraceWindow _Trace;
        private String _username, _password;

        public Action<VariableState> VariableManager_StateChanged { get; }
        public Action AppServer_Closed { get; }
        public Action AppServer_Restart { get; }

        //private TechnicalClient naClient = new TechnicalClient();


        public TraceWindow()
        {
            InitializeComponent();
            _Trace = this;
            Title = Helper.ProductDescription + " " + Helper.ProductVersion;

            _appServer = new AppServerForDriver();
            _appServer.VariableManager.StateChanged += VariableManager_StateChanged;
            _appServer.ControllerManager.Closed += AppServer_Closed;
            _appServer.Restart += AppServer_Restart;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;		// pour le formatage des doubles
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (!App.closingState)
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        #region Trace,Error,Log
        private void Error(string text)
        {
            try
            {
                logWindow("Error: " + text);
                Log("Error: " + text);
                if (_appServer.IsConnected)
                    _appServer.AlarmManager.AddAlarm(10, text, _appServer.CurrentProtocol.Description);
            }
            catch { }
        }
        public void Trace(string text)
        {
            logWindow(text);
            if (_appServer.LogLevel > 0)
                Log(text);
        }
        void Log(string line)
        {
            _appServer.Log(line);
        }
        void logWindow(string text)
        {
            Dispatcher.Invoke(new Action(
                delegate
                {
                    while (listBox1.Items.Count > 250)
                        listBox1.Items.RemoveAt(0);
                    text = DateTime.Now.ToString("hh:mm.fff ") + text;
                    this.listBox1.Items.Add(text);
                    listBox1.ScrollIntoView(text);
                }));
        }
        #endregion
    }
}
