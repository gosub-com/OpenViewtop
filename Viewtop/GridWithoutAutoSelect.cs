using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gosub.Viewtop
{
    /// <summary>
    /// Disable the annoying auto select
    /// </summary>
    public partial class GridWithoutAutoSelect : DataGridView
    {
        bool mDisableAutoSelect = true;


        protected override void OnSelectionChanged(EventArgs e)
        {
            if (mDisableAutoSelect)
            {
                mDisableAutoSelect = false;
                ClearSelection();
                return;
            }
            base.OnSelectionChanged(e);
        }

        protected override void OnRowsRemoved(DataGridViewRowsRemovedEventArgs e)
        {
            base.OnRowsRemoved(e);
            if (Rows.Count == 0)
                mDisableAutoSelect = true;
        }

        public GridWithoutAutoSelect()
        {
            InitializeComponent();
        }
    }
}
