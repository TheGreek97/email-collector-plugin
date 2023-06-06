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
using System.Diagnostics;
using System.Collections.Specialized;
using Microsoft.Office.Interop.Outlook;

namespace PhishingDataCollector
{
    public partial class ThisAddIn
    {
        List<MailData> mailList = new List<MailData>(); // Initialize empty array to store the features of each email
        string outputFile = @"C:\Users\franc\source\repos\email-collector-plugin\PhishingDataCollector\output\test.txt";

        private void ThisAddIn_Startup(object sender, System.EventArgs e)
        {
            Outlook.MAPIFolder inbox = Globals.ThisAddIn.Application.Session.DefaultStore.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderInbox);


            foreach (Outlook.MailItem mail in inbox.Items)
            {
                if (mail != null)
                {
                    MailData md = computeMailFeatures(in mail);
                    mailList.Add(md);
                }
            }

            Debug.WriteLine(mailList);

            var options = new JsonSerializerOptions
            {
                IncludeFields = true
            };

            StreamWriter writer = new StreamWriter(outputFile);
            
            try {
                string json = JsonSerializer.Serialize(mailList, options);
                writer.WriteLine(json);
            } catch (System.ArgumentException err)
            {
                Debug.WriteLine(mailList[0]);
                Debug.WriteLine(err);
            }
            writer.Close();
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

        private MailData computeMailFeatures(in MailItem mail)
        {
            MailData md = new MailData(
                id: mail.EntryID, 
                size: mail.Size, 
                subject: mail.Subject, 
                body: mail.Body.ToUpper(),
                htmlBody: mail.HTMLBody.ToUpper(), 
                sender: mail.SenderEmailAddress, 
                n_recipients: mail.Recipients.Count
            );
            
            return md;
        }

        #endregion
    }
}
