using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GSPlatformClient
{
    public partial class LaunchStatusTaskPanel : UserControl
    {
        public LaunchStatusTaskPanel()
        {
            InitializeComponent();
        }

        private int _icon;
        public int Icon
        {
            get => _icon;
            set
            {
                _icon = value;
                //Note: about the value: <0 means pending, >=0 means finished.
                pictureBox1.Visible = value == -1;
                pictureBox2.Visible = value == 0;
                pictureBox3.Visible = value == 1;
            }
        }

        public new string Text
        {
            get => label1.Text;
            set => label1.Text = value;
        }
    }
}
