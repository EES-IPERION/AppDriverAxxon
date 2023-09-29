/*
 * ***********************
 *   AppDriverAxxon
 * ***********************
 * @Version : 1.0
 * @Date    : 26/07/2022
 * @company : IPERION 
 * @author  : BONNES Romain
 * 
 */

using AppDriverAxxon;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Prysm.AppVision.Common;
using Prysm.AppVision.Data;
using Prysm.AppVision.SDK;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace AppDriverSample
{
    public partial class MainWindow : Window
    {
        // Déclaration des variables globales au driver
        string _serverHostname = "";
        public static AppServerForDriver _appServer;
        public static VariableRow _variableProtocol;
        public static MainWindow _Trace;
        private String _username, _password;
        private String[] _address;
        public List<Camera> camerasSelectedbuffers = new List<Camera>();
        public List<Camera> cameraSelecteds = new List<Camera>();
        public List<Camera> cameraAvailables = new List<Camera>();
        public List<Camera> cameraMarkedAsDelete = new List<Camera>();
        string idCameraPattern = @"[.]([0-9]*)[\/]";
        public IEnumerable<VariableRow> variablesAxxon;
        private String protocolName = "Axxon";
        TraceWindow traceWindow;
        bool started = false;
        private ClientWebSocket ws;
        private String[] cameraToSubscribe;
        Dictionary<String, int> states = new Dictionary<String, int>
        {
            {"signal restored", 1},
            {"signal lost", 2}
        };

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                traceWindow = new TraceWindow();
                _Trace = this;
                Title = Helper.ProductDescription + " " + Helper.ProductVersion;

                _appServer = new AppServerForDriver();
                _appServer.VariableManager.StateChanged += VariableManager_StateChanged;
                _appServer.ControllerManager.Closed += AppServer_Closed;
                _appServer.ControllerManager.Restart += AppServer_Restart;
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;		// pour le formatage des doubles
            }
            catch (Exception error)
            {
                Trace($"Error : {error.Message}");
                Trace(error.StackTrace);
            }
        }

        #region Window_Loaded()

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // load protocol name from Appvision
                var protocolName = loadProtocolName();
                connectToAppvision(protocolName);

                // Définition des paramètres d'authentification que nous récupérons dans les paramètres du protocole
                getCredentialsParamsInProtocol();

                //call fun every x ms
                TimeSpan periodTimeSpan = TimeSpan.FromSeconds(20);

                var task2 = Task.Run(async () =>
                {
                    //while (true)
                    //{
                    try
                    {
                        ws = await ConnectToAxxonAsync();
                        await getAxxonDataAsync();
                        subscribePushAlertAsync();
                    }
                    catch (Exception ex)
                    {
                        Trace(ex.Message);
                        Trace(ex.StackTrace);
                        Error("An exception occurred: " + ex.ToString());
                    }
                    //await Task.Delay(periodTimeSpan);
                    //}
                });
            }
            catch (Exception error)
            {
                Trace($"Error : {error.Message}");
                Trace(error.StackTrace);
            }
        }

        private async Task subscribePushAlertAsync()
        {
            Trace("Abonnement aux événement");
            String cameraToSubscribeString = "[\"";
            foreach (String camera in cameraToSubscribe)
            {
                if (camera != cameraToSubscribe.First())
                {
                    cameraToSubscribeString += "\",\"";
                }
                cameraToSubscribeString += camera;
            }
            cameraToSubscribeString += "\"]";
            try
            {
                var sendmsgBytes = Encoding.UTF8.GetBytes("{\"include\":" + cameraToSubscribeString + ",\"exclude\":[]}");
                ws.SendAsync(new ArraySegment<byte>(sendmsgBytes, 0, sendmsgBytes.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Trace($"Error: {ex.Message}");
                throw;
            }
            Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        try
                        {
                            if (ws.State == WebSocketState.Aborted)
                            {
                                Trace("websocket connection has crashed");
                                ws.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "Connection with the Axxon server was interrupted", CancellationToken.None);
                                Thread.Sleep(5000);
                                ws = await ConnectToAxxonAsync();
                                continue;
                            }
                            var rcvBytes = new byte[1024];
                            var rcvBuffer = new ArraySegment<byte>(rcvBytes);
                            WebSocketReceiveResult rcvResult = await ws.ReceiveAsync(rcvBuffer, CancellationToken.None);
                            byte[] msgBytes = rcvBuffer.Skip(rcvBuffer.Offset).Take(rcvResult.Count).ToArray();
                            var json = Encoding.UTF8.GetString(msgBytes);
                            //var test = json.objects;
                            //Trace(json);
                            ObjectsList eventList = JsonConvert.DeserializeObject<ObjectsList>(json, new JsonSerializerSettings
                            {
                                Error = delegate (object sender, ErrorEventArgs args)
                                {
                                    //errors.Add(args.ErrorContext.Error.Message);
                                    args.ErrorContext.Handled = true;
                                },
                                Converters = { new IsoDateTimeConverter() }
                            });

                            if (eventList == null)
                                continue;

                            try
                            {
                                foreach (var item in eventList.objects)
                                {
                                    //Trace(item.toString());
                                    if (item.type == "devicestatechanged")
                                    {
                                        Trace("-------------------------------------");
                                        Trace(item.toString());
                                        Match m = Regex.Match(item.name, idCameraPattern);
                                        item.id = m.Groups[1].Value;
                                        try
                                        {
                                            UpdateVariable($"$V.Axxon.{item.id}.state", states[item.state]);
                                        }
                                        catch (Exception err)
                                        {
                                            Trace(err.Message);
                                            Trace(err.StackTrace);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Trace($"error: {ex.Message}");
                            }
                        }
                        catch (Exception err)
                        {
                            Trace("Error: " + err.Message);
                            Trace(err.StackTrace);
                        }
                    }
                }
                catch (Exception e)
                {
                    Trace(e.Message);
                    Trace(e.StackTrace);
                    throw;
                }

            });

        }

        private async Task<ClientWebSocket> ConnectToAxxonAsync()
        {
            var ws = new ClientWebSocket();
            try
            {
                String headerVal = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}"));
                ws.Options.SetRequestHeader("Authorization", "Basic " + headerVal);

                await ws.ConnectAsync(new Uri($"ws://{_address[0]}:{_address[1]}/events"), CancellationToken.None);
            }
            catch (Exception e)
            {
                Trace($"erreur : {e.Message}");
                Trace(e.StackTrace);
                throw;
            }
            Trace("Connection websocket réussi");
            return ws;
        }

        private async Task getAxxonDataAsync()
        {
            Trace("Récupération des caméras Axxon");
            HttpClient client = new HttpClient();
            String headerVal = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}"));
            AuthenticationHeaderValue header = new AuthenticationHeaderValue("Basic", headerVal);
            client.DefaultRequestHeaders.Authorization = header;
            HttpResponseMessage response = await client.GetAsync($"http://{_address[0]}:{_address[1]}/video-origins");
            response.EnsureSuccessStatusCode();
            String json = await response.Content.ReadAsStringAsync();
            var cameras = JObject.Parse(json);
            int camerasCount = cameras.Children().Count();
            cameraToSubscribe = new string[camerasCount];
            for (int i = 0; i < camerasCount; i++)
            {
                JProperty camera = (JProperty)cameras.Children().ToArray()[i];
                cameraToSubscribe[i] = "hosts/" + camera.Name;
            }
        }

        private IEnumerable<VariableRow> getAllVariablesByProtocol()
        {
            return _appServer.VariableManager.GetRowsByFilter(filters: new string[] { "$V.Axxon.*", "Type=Node" }).ForEach(variable => { variable.Name = variable.Name.Replace("Axxon.", ""); });
        }

        private void deleteVariable(Camera camera)
        {
            try
            {
                _appServer.VariableManager.DeleteVariable($"Axxon.{camera.friendlyNameShort}");

            }
            catch (Exception err)
            {

                Console.WriteLine($"error : {err.Message}\n{err.StackTrace}");
            }
        }

        private string loadProtocolName()
        {
            string protocolName = "";
            try
            {
                // Appvision
                string[] args = Environment.GetCommandLineArgs();

                if (args == null || args.Length < 2)
                {
                    Trace("!!! Usage: AppDriver Axxon protocol_name missing !!!");
                    MessageBox.Show("Usage: AppDriver Axxon protocol_name missing");
                    Thread.Sleep(15000);
                    Environment.Exit(0);
                }

                // protocolName@hostname or protocolName
                string[] ss = args[1].Split('@');
                protocolName = ss[0]; //Axxon

                if (ss.Length > 1)
                {
                    _serverHostname = ss[1];
                }
                else
                {
                    _serverHostname = "localhost";
                }
                Trace("Server connection ...");
                return protocolName;
            }
            catch (Exception ex)
            {
                Trace("Erreur lors de la récupération des arguments: " + ex.Message);
                MessageBox.Show("Erreur lors de la récupération des arguments : " + ex.Message);
                return null;
            }
        }

        private void connectToAppvision(String protocolName)
        {
            try
            {
                _appServer.Open(_serverHostname);
                _appServer.Login(protocolName); //$P.Kuzzle

                if (_appServer.CurrentProtocol == null)
                {
                    Trace("No protocol " + protocolName);
                    throw new ApplicationException("No protocol " + protocolName);
                }
                else
                {
                    Trace("Protocol : " + protocolName);
                }

                _variableProtocol = _appServer.GetVariablesByProtocol(protocolName).FirstOrDefault();

                if (_variableProtocol == null)
                {
                    Trace("No variables connected to protocol " + protocolName);
                    throw new ApplicationException("No variables connected to protocol " + protocolName);
                }
                // to get output command from server
                _appServer.AddFilterNotifications($"$V.{protocolName}");
                _appServer.StartNotifications(true);
                _appServer.ProtocolSynchronized();
                Trace("Connected");

                //_timer.Start();
            }
            catch (Exception ex)
            {
                Trace("Loaded: " + ex.Message);
                Error("Loaded: " + ex.Message);
                //Close();
            }
        }

        private void getCredentialsParamsInProtocol()
        {
            Trace($"Récupération des paramètres d'authentification indiqués dans les paramètres du protocole {protocolName} (username & password)");
            try
            {
                //get params from appvision Kuzzle parameters
                _username = _variableProtocol.Parameters.GetParameter<String>("username", "");
                _password = _variableProtocol.Parameters.GetParameter<String>("password", "");

                _address = _variableProtocol.Address.Replace("http://", "").Split(':');
            }
            catch (Exception)
            {
                Trace("Erreur lors de la récupération des paramètre de communication, Veuillez indiquer les username & password en tant que paramètres du protocole Kuzzle.");
                Thread.Sleep(15000);
            }
            Trace("Using credentials : username = " + _username + ";password = " + _password);
        }

        static String getSetting(string key)
        {
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings[key] ?? "Not Found";
            }
            catch (ConfigurationErrorsException)
            {
                //Trace("Error reading app settings");
            }
            return null;
        }
        #endregion

        #region Window_Closed()
        private void Window_Closed(object sender, EventArgs e)
        {
            closeWebSocketAsync();
            App.closingState = true;
            Console.WriteLine("closed");
            traceWindow.Close();
            try
            {
                if (_appServer.IsConnected)
                {
                    _appServer.Logout();
                    _appServer.Close();
                }
            }
            catch (Exception ex)
            { }

            if (_restart)
            {
                var args = Environment.GetCommandLineArgs();
                Process.Start(args[0], args.Skip(1).Join(" "));
            }
        }

        private async Task closeWebSocketAsync()
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }
        #endregion

        #region VariableManager_StateChanged()
        void VariableManager_StateChanged(VariableState state)
        {
            Dispatcher.Invoke(new Action(
                delegate
                {
                    string text = "Variable changed: " + state.Name + "=" + state.Value;
                    Trace(text);
                }));
        }
        #endregion

        #region UpdateVariable()
        public static void UpdateVariable(string var, int newValue)
        {

            try
            {
                _appServer.VariableManager.Set(var, newValue);
            }
            catch (Exception ex)
            {
                throw new Exception("!!! Error during var update : " + ex.ToString() + " !!!");
            }

        }

        public static void UpdateStringVariable(string var, string newValue)
        {

            try
            {

                _appServer.VariableManager.Set(var, newValue);

            }
            catch (Exception ex)
            {
                throw new Exception("!!! Error during var update : " + ex.ToString() + " !!!");
            }

        }

        public static void UpdateDoubleVariable(string var, Double newValue)
        {
            MainWindow._Trace.Trace(var);
            MainWindow._Trace.Trace(newValue.ToString());


            try
            {
                //MainWindow._Trace.Trace(_appServer.VariableManager.GetStateByName(var).Value.ToString());

                // On remplace la variable
                _appServer.VariableManager.Set(var, newValue);
            }
            catch (Exception ex)
            {
                throw new Exception("!!! Error during var update : " + ex.ToString() + " !!!");
            }

        }

        public static void UpdateDecimalVariable(string var, Decimal newValue)
        {
            MainWindow._Trace.Trace(var);
            MainWindow._Trace.Trace(newValue.ToString());


            try
            {
                //MainWindow._Trace.Trace(_appServer.VariableManager.GetStateByName(var).Value.ToString());

                // On remplace la variable
                _appServer.VariableManager.Set(var, newValue);
            }
            catch (Exception ex)
            {
                throw new Exception("!!! Error during var update : " + ex.ToString() + " !!!");
            }

        }
        #endregion

        #region appServer_Restart(),appServer_Closed()
        void AppServer_Restart()
        {
            _restart = true;
            Close();            // close driver
        }
        bool _restart = false;

        void AppServer_Closed()
        {
            Close();            // close driver
        }
        #endregion

        #region Trace,Error,Log
        private void Error(string text)
        {
            try
            {
                TraceWindow("Error: " + text);
                Log("Error: " + text);
                /*if (_appServer.IsConnected)
                    _appServer.AlarmManager.AddAlarm(10, text, _appServer.CurrentProtocol.Description);*/
            }
            catch { }
        }
        public void Trace(string text)
        {
            TraceWindow(text);
            if (_appServer.LogLevel > 0)
                Log(text);
        }
        void Log(string line)
        {
            _appServer.Log(line);
        }

        void TraceWindow(string text)
        {
            Dispatcher.Invoke(new Action(
                delegate
                {
                    while (listBox1.Items.Count > 250)
                        listBox1.Items.RemoveAt(0);
                    text = DateTime.Now.ToString("hh:mm.fff ") + text;
                    listBox1.Items.Add(text);
                    listBox1.ScrollIntoView(text);
                }));
        }
        #endregion

    }
}
