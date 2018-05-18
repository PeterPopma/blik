using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using dshow;
using dshow.Core;

namespace Blik
{
    public partial class FormCaptureDevice : Form
    {
        FilterCollection filters;
        private string device;

        // Device
        public string Device
        {
            get { return device; }
        }

        public FormCaptureDevice()
        {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            //
            try
            {
                filters = new FilterCollection(FilterCategory.VideoInputDevice);

                if (filters.Count == 0)
                    throw new ApplicationException();

                // add all devices to combo
                foreach (Filter filter in filters)
                {
                    deviceCombo.Items.Add(filter.Name);
                }
            }
            catch (ApplicationException)
            {
                deviceCombo.Items.Add("No local capture devices");
                deviceCombo.Enabled = false;
                okButton.Enabled = false;
            }

            deviceCombo.SelectedIndex = 0;
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            device = filters[deviceCombo.SelectedIndex].MonikerString;
        }
    }
}
