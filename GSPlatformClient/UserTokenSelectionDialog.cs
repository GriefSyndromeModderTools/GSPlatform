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
    public partial class UserTokenSelectionDialog : Form
    {
        public UserTokenSelectionDialog(IEnumerable<string> items)
        {
            InitializeComponent();
            foreach (var i in items)
            {
                listBox1.Items.Add(i);
            }
            listBox1.SelectedIndex = 0;
        }

        public int SelectedIndex => listBox1.SelectedIndex;
    }
}
