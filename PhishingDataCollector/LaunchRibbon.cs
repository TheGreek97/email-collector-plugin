using Microsoft.Office.Tools.Ribbon;
using System;
using System.Windows.Forms;

namespace PhishingDataCollector
{
    public partial class LaunchRibbon
    {
        private void LaunchRibbon_Load(object sender, RibbonUIEventArgs e)
        {
            Base.Ribbon.Global = true;
            Base.Ribbon.RibbonType = "Microsoft.Outlook.Explorer";
            editBox1.Text = "10000";
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

        // Send logs button
        private void button2_Click(object sender, RibbonControlEventArgs e)
        {
            if (ThisAddIn.InExecution || ThisAddIn.UploadingFiles)
            {
                MessageBox.Show("L'add-in è in esecuzione. Potrai inviare i dati di diagnostica al termine del processo.", "Add-in in esecuzione", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } else
            {
                ThisAddIn.SendLogs();
            }
        }

        private void editBox1_TextChanged(object sender, RibbonControlEventArgs e)
        {

            RibbonEditBox textBox = (RibbonEditBox)sender;
            if (! ThisAddIn.InExecution)
            {
                int new_value;
                const int MAX_EMAILS = 20000;
                if (Int32.TryParse(textBox.Text, out new_value) && new_value > 0 && new_value <= MAX_EMAILS)
                {
                    ThisAddIn.Logger.Error("New limit of email to process set to " + new_value);
                    ThisAddIn.EMAIL_LIMIT = new_value;
                    /*MessageBox.Show($"Il limite massimo di email da processare è stato impostato a {new_value}. Cliccare sul tasto {LaunchPluginBtn.Label} per iniziare il processo di esportazione.", "Nuovo limite impostato!",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);*/
                } else
                {
                    MessageBox.Show("Inserisci un numero valido tra 1 e " + MAX_EMAILS, "Errore", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            } else
            {
                MessageBox.Show("L'add-in è in esecuzione, puoi modificare questo parametro solo prima di lanciare il processo. " +
                    "Per modificare il limite di email da processare, aspetta che il processo termini, o riavvia Outlook.", 
                    "Add-in in esecuzione", MessageBoxButtons.OK, MessageBoxIcon.Error);
                textBox.Text = "10000";  // reset the value
            }
            
        }

        // Send residual emails button
        private void button1_Click(object sender, RibbonControlEventArgs e)
        {
            if (!ThisAddIn.InExecution)
            {
                ThisAddIn.SendResidualEmails();
            }
            else
            {
                MessageBox.Show("L'add-in è già in esecuzione.", "Add-in in esecuzione", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
