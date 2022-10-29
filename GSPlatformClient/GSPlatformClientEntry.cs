using AMLCore.Injection.GSO;
using AMLCore.Injection.Native;
using AMLCore.Logging;
using AMLCore.Misc;
using AMLCore.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GSPlatformClient
{
    public class GSPlatformClientEntry : IEntryPointGSO
    {
        public static readonly Logger Logger = new Logger("GSPlatform");
        internal static IntPtr _movedSocket;
        internal static bool _replaceSocket;

        public void Run()
        {
            //WindowsHelper.MessageBox("start");
            Logger.Info("Starting GSPlatform window");

            PostGSOInjection.Run(() =>
            {
                //Erase WS_VISIBLE from gso window style.
                CodeModification.Modify("gso", 0x2710, 0x00, 0x00, 0xCA, 0x00);

                new GSOWindowCreated();
                new CreateSocket();
                new BindSocket();
            });
        }

        private static IntPtr _hwnd;

        public static void StartGame()
        {
            var h = GetDlgItem(_hwnd, 0x4009);
            //SendMessageW(GetDlgItem(_hwnd, 0x4009), 0xF1, 1, IntPtr.Zero);
            PostMessageW(_hwnd, 0x111, 0x4009, default);
        }

        private sealed class GSOWindowCreatedOld : CodeInjection
        {
            public GSOWindowCreatedOld()
                : base(AddressHelper.Code("gso", 0x20C4), 7)
            {
            }

            protected override void Triggered(NativeEnvironment env)
            {
                _hwnd = env.GetParameterP(0);
                var h = GetDlgItem(_hwnd, 0x4009);
            }
        }

        private sealed class GSOWindowCreated : CodeInjection
        {

            public GSOWindowCreated()
                : base(AddressHelper.Code("gso", 0x2728), 6)
                //: base(AddressHelper.Code("gso", 0x20C4), 7)
            {
                new GSOWindowCreatedOld();
            }

            protected override void Triggered(NativeEnvironment env)
            {
                //_hwnd = env.GetRegister(Register.EAX);

                WindowsHelper.Run(() =>
                {
                    var server = new IniFile("GSPlatform").Read("Servers", "ServerList", "");

                    var form = new MainWindow(server.Split(';'));
                    if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    {
                        Environment.Exit(0);
                    }

                    //var connection = form.Connection;
                    //if (form.DialogResult != System.Windows.Forms.DialogResult.OK || connection == null)
                    //{
                    //    Logger.Info("Not joining any room");
                    //    Environment.Exit(0);
                    //}
                    //
                    //if (!connection.IsHost && connection.HostEndPoint == null)
                    //{
                    //    Logger.Info("Invalid connection: no host info");
                    //    return;
                    //}

                    //connection code removed from here

                    //_hwnd = env.GetParameterP(0);

                    //
                    //if (!connection.IsHost)
                    //{
                    //    Logger.Info("Starting gso in client mode");
                    //    Marshal.WriteInt32(AddressHelper.Code("gso", 0x26E78), 0);
                    //    SendMessageW(GetDlgItem(_hwnd, 4001), 0xF1, 1, IntPtr.Zero);
                    //}
                    //else
                    //{
                    //    Logger.Info("Starting gso in host mode");
                    //    Marshal.WriteInt32(AddressHelper.Code("gso", 0x26E78), 1);
                    //    SendMessageW(GetDlgItem(_hwnd, 4000), 0xF1, 1, IntPtr.Zero);
                    //}
                    //
                    //SetEnabled(0x4000, false);
                    //SetEnabled(0x4001, false);
                    //SetEnabled(0x4002, false);
                    //SetEnabled(0x4003, false);
                    //SetEnabled(0x4004, false);
                    //SetEnabled(0x4005, false);
                    //SetEnabled(0x4006, false);
                    //SetVisible(0x4006, false);
                    //SetEnabled(0x4007, true);
                    //SetVisible(0x4007, true);
                    //if (connection.IsHost)
                    //{
                    //    SetEnabled(0x4009, true);
                    //}

                });
            }

            private void SetEnabled(int id, bool value)
            {
                EnableWindow(GetDlgItem(_hwnd, id), value ? 1 : 0);
            }

            private void SetVisible(int id, bool value)
            {
                ShowWindow(GetDlgItem(_hwnd, id), value ? 1 : 0);
            }

        }

        [DllImport("User32.dll", SetLastError = true)]
        private static extern IntPtr GetDlgItem(IntPtr hDlg, int nIDDlgItem);

        [DllImport("User32.dll")]
        private static extern bool EnableWindow(IntPtr hWnd, int bEnable);

        [DllImport("User32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("User32.dll")]
        private static extern bool PostMessageW(IntPtr hwnd, int msg, int w, IntPtr l);

        [DllImport("User32.dll")]
        private static extern IntPtr SendMessageW(IntPtr hwnd, int msg, int w, IntPtr l);

        private delegate IntPtr SocketDelegate(int a, int b, int c);
        private delegate int BindDelegate(IntPtr a, IntPtr b, int c);

        private sealed class CreateSocket : FunctionPointerInjection<SocketDelegate>
        {
            public CreateSocket() : base(AddressHelper.Code("gso", 0x1C268))
            {
            }

            protected override void Triggered(NativeEnvironment env)
            {
                if (_replaceSocket)
                {
                    _replaceSocket = false;
                    Logger.Info($"Replaced socket handle {_movedSocket}.");
                    env.SetReturnValue(_movedSocket);
                }
                else
                {
                    Logger.Info($"Calling original socket().");
                    env.SetReturnValue(Original(env.GetParameterI(0), env.GetParameterI(1), env.GetParameterI(2)));
                }
            }
        }

        private sealed class BindSocket : FunctionPointerInjection<BindDelegate>
        {
            private bool _first = true;

            public BindSocket() : base(AddressHelper.Code("gso", 0x1C26C))
            {
            }

            protected override void Triggered(NativeEnvironment env)
            {
                if (_first)
                {
                    _first = false;
                    var s = env.GetParameterP(0);
                    if (s != _movedSocket)
                    {
                        Logger.Error("First call to bind() does not match replaced socket handle.");
                        env.SetReturnValue(Original(env.GetParameterP(0), env.GetParameterP(1), env.GetParameterI(2)));
                    }
                    else
                    {
                        Logger.Info($"Bind socket handle {_movedSocket}.");

                        //Do nothing.
                        env.SetReturnValue(0);
                    }
                }
                else
                {
                    Logger.Info($"Calling original bind().");
                    env.SetReturnValue(Original(env.GetParameterP(0), env.GetParameterP(1), env.GetParameterI(2)));
                }
            }
        }
    }
}
