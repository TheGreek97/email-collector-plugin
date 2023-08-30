using Microsoft.Office.Tools.Ribbon;

namespace PhishingDataCollector
{
    public partial class LaunchRibbon
    {
        private void LaunchRibbon_Load(object sender, RibbonUIEventArgs e)
        {
            Base.Ribbon.Global = true;
            Base.Ribbon.RibbonType = "Microsoft.Outlook.Explorer";
        }

        private void LaunchButton_Click(object sender, RibbonControlEventArgs e)
        {
            ThisAddIn.ExecuteAddIn();
        }


        private void StateButton_Click(object sender, RibbonControlEventArgs e)
        {
            ThisAddIn.ShowStatus();
        }

        private void AboutButton_Click(object sender, RibbonControlEventArgs e)
        {
            ThisAddIn.ShowClientID();
        }

        private void LimitResourcesCheckbox_Click(object sender, RibbonControlEventArgs e)
        {
            ThisAddIn.SetMultiThreadExecution(LimitResourcesCheckbox.Checked);
        }
    }
}
