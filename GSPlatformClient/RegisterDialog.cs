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
    public partial class RegisterDialog : Form
    {
        public RegisterDialog()
        {
            InitializeComponent();
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            button1.Enabled = textBox2.Text.Length > 0;
        }

        public bool InvitationEnabled
        {
            get => textBox1.Enabled;
            set => textBox1.Enabled = value;
        }

        public string Code
        {
            get => textBox1.Text;
            set => textBox1.Text = value;
        }

        public string UserName
        {
            get => textBox2.Text;
            set => textBox2.Text = value;
        }
    }
}
