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
    public partial class RoomPanel : UserControl
    {
        public RoomPanel()
        {
            InitializeComponent();
            if (LicenseManager.UsageMode != LicenseUsageMode.Designtime)
            {
                panel2.Controls.Clear();
            }
            RoomImage = GSPlatformClient.RoomImage.BitmapData;
            button1.Click += (sender, e) => { DisableButtons(); Join?.Invoke(RoomId); };
            button2.Click += (sender, e) => { DisableButtons(); Exit?.Invoke(RoomId); };
            button3.Click += (sender, e) => { DisableButtons(); Launch?.Invoke(RoomId); };
        }

        private void DisableButtons()
        {
            button1.Enabled = false;
            button2.Enabled = false;
            button3.Enabled = false;
        }

        public event Action<string> Join;
        public event Action<string> Exit;
        public event Action<string> Launch;

        public string RoomId { get; set; }

        public string RoomName
        {
            get => label1.Text;
            set => label1.Text = value;
        }

        private string _roomOwner;
        public string RoomOwner
        {
            get => _roomOwner;
            set => label2.Text = $"By {_roomOwner = value}";
        }

        public string RoomDescription
        {
            get => label3.Text;
            set
            {
                label3.Text = value;
                label3.Visible = !string.IsNullOrWhiteSpace(value);
            }
        }

        private string _roomImage;
        public string RoomImage
        {
            get => _roomImage;
            set
            {
                if (_roomImage == value) return;
                _roomImage = value;

                var last = panel4.BackgroundImage;
                panel4.BackgroundImage = null;
                last?.Dispose();

                try
                {
                    panel4.BackgroundImage = new Bitmap(new MemoryStream(Convert.FromBase64String(value)));
                }
                catch
                {
                    panel4.BackgroundImage = null;
                }
            }
        }

        private int _maxCount;
        public int MaxPeerCount
        {
            get => _maxCount;
            set
            {
                _maxCount = value;
                UpdatePeerCountUI();
            }
        }

        private int _currentCount;
        public int CurrentPeerCount
        {
            get => _currentCount;
            set
            {
                _currentCount = value;
                UpdatePeerCountUI();
            }
        }

        private void UpdatePeerCountUI()
        {
            button1.Text = $"加入 ({_currentCount}/{_maxCount})";
            button2.Text = $"退出 ({_currentCount}/{_maxCount})";
        }

        public void ResetPeers(IEnumerable<PeerPanel> peers)
        {
            panel2.SuspendLayout();
            try
            {
                panel2.Controls.Clear();
                foreach (var p in peers)
                {
                    p.Dock = DockStyle.Top;
                    panel2.Controls.Add(p);
                }
            }
            finally
            {
                panel2.ResumeLayout();
            }
        }

        public void SetupPeers<TData>(IEnumerable<TData> data, Action<int, PeerPanel, TData, bool> action)
        {
            panel2.SuspendLayout();
            try
            {
                int i = 0;
                foreach (var d in data)
                {
                    PeerPanel p;
                    bool isNew;
                    if (panel2.Controls.Count <= i)
                    {
                        p = new PeerPanel
                        {
                            Dock = DockStyle.Top,
                        };
                        panel2.Controls.Add(p);
                        isNew = true;
                    }
                    else
                    {
                        p = (PeerPanel)panel2.Controls[i];
                        isNew = false;
                    }
                    action(i, p, d, isNew);

                    i += 1;
                }
                for (; i < panel2.Controls.Count; ++i)
                {
                    panel2.Controls.RemoveAt(i);
                }
            }
            finally
            {
                panel2.ResumeLayout();
            }
        }

        public bool IsJoined
        {
            get => button2.Visible;
            set => button1.Visible = !(button2.Visible = value);
        }

        public bool CanJoin
        {
            get => button1.Enabled;
            set => button1.Enabled = value;
        }

        public bool CanExit
        {
            get => button2.Enabled;
            set => button2.Enabled = value;
        }

        public bool IsHost
        {
            get => button3.Visible;
            set => button3.Visible = value;
        }

        public bool CanLaunch
        {
            get => button3.Enabled;
            set => button3.Enabled = value;
        }
    }
}
