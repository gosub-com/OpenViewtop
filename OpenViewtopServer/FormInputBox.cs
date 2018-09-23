using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gosub.OpenViewtopServer
{
    public partial class FormInputBox : Form
    {
        public FormInputBox()
        {
            InitializeComponent();
        }

        static public string ShowDialog(IWin32Window owner, string text)
        {
            var inputBox = new FormInputBox();
            inputBox.labelText.Text = text;
            if (inputBox.ShowDialog(owner) == DialogResult.Cancel)
                return "";
            return inputBox.textInput.Text;
        }

    }
}
