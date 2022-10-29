using AMLCore.Injection.GSO;
using AMLCore.Injection.Native;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GSPlatformClient
{
    public partial class LaunchStatusDialog : Form
    {
        private static int? _clients;

        private static readonly List<KeyValuePair<string, Func<Func<int>>>> _preparations = new List<KeyValuePair<string, Func<Func<int>>>>()
        {
            new KeyValuePair<string, Func<Func<int>>>("等待GSO客户端的连接", WaitForClients),
            //new KeyValuePair<string, Func<Func<int>>>("Delay 10s", Delay10s),
        };

        private static List<KeyValuePair<string, Func<Func<int>>>> GetPreparations()
        {
            var t = typeof(GSOConnectionStatus).Assembly.GetType("AMLCore.Injection.GSO.GSOPreparation");
            if (t != null)
            {
                //GSOPreparation.GetPreparation(version: 1) -> object
                var m = t.GetMethod("GetPreparations", BindingFlags.Static | BindingFlags.Public);
                var l = m.Invoke(null, new object[] { 1 });
                if (l != null)
                {
                    return (List<KeyValuePair<string, Func<Func<int>>>>)l;
                }
            }
            return _preparations;
        }

        private static Func<int> WaitForClients()
        {
            return () =>
            {
                if (!_clients.HasValue && GSOConnectionStatus.ClientStatus != null)
                {
                    return 0;
                }
                var ss = GSOConnectionStatus.ServerStatus;
                if (_clients.HasValue && ss != null)
                {
                    int count = 0;
                    foreach (var p in ss.Clients)
                    {
                        if (p != null) ++count;
                    }
                    if (count == _clients)
                    {
                        return 0;
                    }
                }
                return -1;
            };
        }

        private static Func<int> Delay10s()
        {
            DateTime end = DateTime.Now + TimeSpan.FromSeconds(10);
            return () => DateTime.Now >= end ? 0 : -1;
        }

        //====

        private readonly RoomClient _connection;

        internal LaunchStatusDialog(RoomClient connection)
        {
            _connection = connection;
            InitializeComponent();
        }

        private async Task StartUpdate()
        {
            List<KeyValuePair<string, Func<int>>> list = new List<KeyValuePair<string, Func<int>>>();
            foreach (var pair in GetPreparations())
            {
                list.Add(new KeyValuePair<string, Func<int>>(pair.Key, pair.Value()));
            }

            while (!IsDisposed && Visible)
            {
                int index = 0;
                bool allReady = true;
                foreach (var pair in list)
                {
                    var status = pair.Value();
                    if (status < 0) allReady = false;
                    Update(index++, status, pair.Key);
                }
                if (allReady)
                {
                    DialogResult = DialogResult.OK;
                    break;
                }

                await Task.Delay(500);
            }
        }

        private void Update(int i, int status, string text)
        {
            LaunchStatusTaskPanel p;
            if (i >= panel1.Controls.Count)
            {
                p = new LaunchStatusTaskPanel()
                {
                    Dock = DockStyle.Top,
                };
                panel1.Controls.Add(p);
            }
            else
            {
                p = (LaunchStatusTaskPanel)panel1.Controls[i];
            }
            p.Icon = status;
            p.Text = text;
        }

        private async void WaitForConnectionReady()
        {
            if (_connection.IsHost)
            {
                _clients = _connection.GetPeers().Length;
            }
            else
            {
                _clients = null;
            }

            //Wait for clients and AML preparation.
            await StartUpdate();

            //Start the game.
            //var func = (StartGameDelegate)Marshal.GetDelegateForFunctionPointer(AddressHelper.Code("gso", 0x81E0), typeof(StartGameDelegate));
            //var ret = func();
            //if (ret == 0)
            //{
            //    AMLCore.Misc.WindowsHelper.MessageBox("error");
            //    //DialogResult = DialogResult.Cancel;
            //    return;
            //}
            ////DialogResult = DialogResult.OK;
            GSPlatformClientEntry.StartGame();
            DialogResult = DialogResult.OK;
        }

        private delegate int StartGameDelegate();

        private void LaunchStatusDialog_Shown(object sender, EventArgs e)
        {
            WaitForConnectionReady();
        }
    }
}
