using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gosub.Viewtop
{
    public partial class FormPassword : Form
    {
        public FormPassword()
        {
            InitializeComponent();
        }

        public string Message
        {
            get { return labelMessage.Text; }
            set { labelMessage.Text = value; }
        }

        public bool Accepted { get; set; }

        public string Password { get { return textPassword1.Text; } }

        public string UserName
        {
            get { return textUserName.Text;  }
            set { textUserName.Text = value; }
        }

        public bool UserNameReadOnly
        {
            get { return textUserName.Enabled; }
            set
            {
                textUserName.Enabled = !value;
                (textUserName.Enabled ? textUserName : textPassword1).Focus();
            }
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            if (textUserName.Text == "")
            {
                MessageBox.Show(this, "User name must not be blank", App.Name);
                return;
            }
            if (!UserFile.ValidUserName(textUserName.Text))
            {
                MessageBox.Show(this, "Invalid user name.  The user name may only contain letters and numbers.", App.Name);
                return;
            }
            if (textPassword1.Text != textPassword2.Text)
            {
                MessageBox.Show(this, "ERROR: Passwords must match", App.Name);
                return;
            }
            if (textPassword1.Text == "")
            {
                if (MessageBox.Show(this, "Do you really want to allow a blank password?", App.Name, MessageBoxButtons.YesNo) == DialogResult.No)
                    return;
            }
            Accepted = true;
            Close();
        }
    }
}
