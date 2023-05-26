using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Outlook = Microsoft.Office.Interop.Outlook;
using Office = Microsoft.Office.Core;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace PhisingDataCollector
{
    public partial class ThisAddIn
    {
        List<MailData> dataList = new List<MailData>();
        private void ThisAddIn_Startup(object sender, System.EventArgs e)
        {
            Outlook.MAPIFolder inbox = Globals.ThisAddIn.Application.Session.DefaultStore.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderInbox);

            foreach (Outlook.MailItem item in inbox.Items)
            {
                if (item != null)
                {
                    getMailData(item);
                }
            }

            var options = new JsonSerializerOptions
            {
                IncludeFields = true,
            };
            StreamWriter writer = new StreamWriter(@"C:\Users\dnlpl\Desktop\RecivedEmail1.txt");
            string json = JsonSerializer.Serialize(dataList, options);
            writer.WriteLine(json);
            writer.Close();
        }

        private void getMailData(Outlook.MailItem pMail)
        {
            MailData md = new MailData(pMail);
            dataList.Add(md);
        }

        private void ThisAddIn_Shutdown(object sender, System.EventArgs e)
        {
            // Nota: Outlook non genera più questo evento. Se è presente codice che 
            //    deve essere eseguito all'arresto di Outlook, vedere https://go.microsoft.com/fwlink/?LinkId=506785
        }

        #region Codice generato da VSTO

        /// <summary>
        /// Metodo richiesto per il supporto della finestra di progettazione. Non modificare
        /// il contenuto del metodo con l'editor di codice.
        /// </summary>
        private void InternalStartup()
        {
            this.Startup += new System.EventHandler(ThisAddIn_Startup);
            this.Shutdown += new System.EventHandler(ThisAddIn_Shutdown);
        }
        
        #endregion
    }
}
