using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GSPlatformClient
{
    public partial class ServerPanel : UserControl
    {
        public ServerPanel()
        {
            InitializeComponent();
            if (LicenseManager.UsageMode != LicenseUsageMode.Designtime)
            {
                panel1.Controls.Clear();
            }
            button1.Click += (sender, e) => Register?.Invoke();
            button2.Click += (sender, e) => NewRoom?.Invoke();
        }

        public event Action Register;
        public event Action NewRoom;

        private static readonly Bitmap DefaultImage = LoadDefaultBitmap();
        private static Bitmap LoadDefaultBitmap()
        {
            //Must not dispose stream!
            var s = typeof(ServerPanel).Assembly.GetManifestResourceStream("GSPlatformClient.unknown.png");
            {
                return new Bitmap(s);
            }
        }

        private string _serverImageStr = "\0";
        public string ServerImage
        {
            get => _serverImageStr;
            set
            {
                if (_serverImageStr != value)
                {
                    _serverImageStr = value;

                    var last = panel4.BackgroundImage;
                    panel4.BackgroundImage = null;
                    if (!ReferenceEquals(last, DefaultImage))
                    {
                        last?.Dispose();
                    }

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        panel4.BackgroundImage = DefaultImage;
                    }
                    else
                    {
                        try
                        {
                            panel4.BackgroundImage = new Bitmap(new MemoryStream(Convert.FromBase64String(value)));
                        }
                        catch
                        {
                            panel4.BackgroundImage = DefaultImage;
                        }
                    }
                }
            }
        }

        public string ServerName
        {
            get => label1.Text;
            set => label1.Text = value;
        }

        private bool _notRegistered;
        public bool ServerNotRegistered
        {
            get => _notRegistered;
            set
            {
                _notRegistered = value;
                UpdateConnectionStatus();
            }
        }

        private int? _online;
        public int? OnlineUsers
        {
            get => _online;
            set
            {
                _online = value;
                label2.Text = $"{value ?? 0} 人在线";
            }
        }

        private int _delay;
        public int ServerDelay
        {
            get => _delay;
            set
            {
                _delay = value;
                UpdateConnectionStatus();
            }
        }

        private int _roomCount;
        public int RoomCount
        {
            get => _roomCount;
            private set
            {
                _roomCount = value;
                UpdateConnectionStatus();
            }
        }

        private bool _connectionFailed;
        public bool ConnectionFailed
        {
            get => _connectionFailed;
            set
            {
                _connectionFailed = value;
                UpdateConnectionStatus();
            }
        }

        private void UpdateConnectionStatus()
        {
            label2.Visible = label3.Visible = !_notRegistered || _connectionFailed;
            label4.Visible = button1.Visible = _notRegistered && !_connectionFailed;
            label3.Text = _connectionFailed ? "无法连接" : $"已连接：{_delay} ms，{_roomCount} 个房间";
        }

        public bool CanAddRoom
        {
            get => button2.Enabled;
            set => button2.Enabled = value;
        }

        public bool CanRegister
        {
            get => button1.Enabled;
            set => button1.Enabled = value;
        }

        public string RegisterText
        {
            get => button1.Text;
            set => button1.Text = value;
        }

        public void ResetRooms(IEnumerable<RoomPanel> rooms)
        {
            panel1.SuspendLayout();
            try
            {
                panel1.Controls.Clear();

                int count = 0;
                foreach (var r in rooms)
                {
                    count += 1;
                    r.Dock = DockStyle.Top;
                    panel1.Controls.Add(r);
                }
                RoomCount = count;
            }
            finally
            {
                panel1.ResumeLayout();
            }
        }

        public void SetupRoomPanels<TData>(IEnumerable<TData> data,
            Action<int, RoomPanel, TData, bool> action)
        {
            panel1.SuspendLayout();
            try
            {
                int i = 0;
                foreach (var d in data)
                {
                    RoomPanel r;
                    bool isNew;
                    if (panel1.Controls.Count <= i)
                    {
                        r = new RoomPanel
                        {
                            Dock = DockStyle.Top,
                        };
                        panel1.Controls.Add(r);
                        isNew = true;
                    }
                    else
                    {
                        r = (RoomPanel)panel1.Controls[i];
                        isNew = false;
                    }
                    action(i, r, d, isNew);

                    i += 1;
                }
                RoomCount = i;
                for (; i < panel1.Controls.Count; ++i)
                {
                    panel1.Controls.RemoveAt(i);
                }
            }
            finally
            {
                panel1.ResumeLayout();
            }
        }
    }
}
