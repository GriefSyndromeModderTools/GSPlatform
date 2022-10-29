using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GSPlatformClient
{
    public partial class PeerPanel : UserControl
    {
        public PeerPanel()
        {
            InitializeComponent();
            PeerEndPoint = new IPEndPoint(IPAddress.None, 0);
            label2.Click += (sender, e) => RelayRequest?.Invoke(RoomId, _peerEndPoint);
        }

        public event Action<string, IPEndPoint> RelayRequest;

        public string RoomId { get; set; }

        public Color PeerStatusColor
        {
            get => label1.ForeColor;
            set => label1.ForeColor = value;
        }

        private string _peerName = "未知";
        private IPEndPoint _peerEndPoint = new IPEndPoint(IPAddress.None, 0);
        public string PeerName
        {
            get => _peerName;
            set => label5.Text = $"{_peerName = value} ({_peerEndPoint})";
        }
        public IPEndPoint PeerEndPoint
        {
            get => _peerEndPoint;
            set => label5.Text = $"{_peerName} ({_peerEndPoint = value})";
        }

        private float _delay;
        public float PeerDelay
        {
            get => _delay;
            set => label3.Text = $"{_delay = value:F0} ms";
        }

        private float _connectivity;
        public float PeerConnectivity
        {
            get => _connectivity;
            set => label4.Text = $"{100 * (_connectivity = value):F0}%";
        }

        private bool _relayEnabled;
        public bool RelayEnabled
        {
            get => _relayEnabled;
            set
            {
                _relayEnabled = value;
                UpdateRelayState();
            }
        }

        private bool _canRelay;
        public bool CanRelay
        {
            get => _canRelay;
            set
            {
                _canRelay = value;
                UpdateRelayState();
            }
        }

        private bool _isRelay;
        public bool IsRelay
        {
            get => _isRelay;
            set
            {
                _isRelay = value;
                UpdateRelayState();
            }
        }

        private void UpdateRelayState()
        {
            label2.Visible = _relayEnabled;
            label2.Enabled = CanRelay || IsRelay;
            label2.ForeColor =
                IsRelay ?
                    Color.Green :
                    CanRelay ? Color.Blue : Color.Gray;
            label2.Text = IsRelay ? "已启用转发" : "转发模式";
        }
    }
}
