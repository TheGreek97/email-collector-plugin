using Microsoft.Office.Tools.Ribbon;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace PhishingDataCollector
{
    public partial class LaunchRibbon
    {
        private void LaunchRibbon_Load(object sender, RibbonUIEventArgs e)
        {
            Base.Ribbon.Global = true;
            Base.Ribbon.RibbonType = "Microsoft.Outlook.Explorer";
            Debug.WriteLine("Context: " + Base.Ribbon.Context.ToString());
        }

        private void button1_Click(object sender, RibbonControlEventArgs e)
        {
            this.button1.Enabled = false;
            ThisAddIn.ExecuteAddIn();
            this.button1.Enabled = true;
        }

        private void gallery1_Click(object sender, RibbonControlEventArgs e)
        {

        }
    }
}
