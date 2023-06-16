using System;
using System.Collections.Generic;
/*
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Outlook = Microsoft.Office.Interop.Outlook;
using Office = Microsoft.Office.Core;
*/
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using System.Collections.Specialized;
using Microsoft.Office.Interop.Outlook;
using System.Text.RegularExpressions;
using Microsoft.Office.Tools.Ribbon;

namespace PhishingDataCollector
{
    public partial class ThisAddIn
    {
        static List<MailData> mailList = new List<MailData>(); // Initialize empty array to store the features of each email
        const string outputFile = @"C:\Users\franc\source\repos\email-collector-plugin\PhishingDataCollector\output\test.txt";

        private LaunchRibbon taskPaneControl;

        private void ThisAddIn_Startup(object sender, System.EventArgs e)
        {
            taskPaneControl = Globals.Ribbons.LaunchRibbon;
            taskPaneControl.RibbonType = "Microsoft.Outlook.Explorer";
            Debug.WriteLine("---------- Global: " + taskPaneControl.Global);
        }


        public static void ExecuteAddIn ()
        {
            MAPIFolder inbox = Globals.ThisAddIn.Application.Session.DefaultStore.GetDefaultFolder(OlDefaultFolders.olFolderInbox);

            foreach (MailItem mail in inbox.Items)
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

            try
            {
                string json = JsonSerializer.Serialize(mailList, options);
                writer.WriteLine(json);
            }
            catch (System.ArgumentException err)
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

        private static MailData computeMailFeatures(in MailItem mail) {
            string[] mail_headers;
            try
            {
                string mail_headers_string = mail.PropertyAccessor.GetProperty("http://schemas.microsoft.com/mapi/proptag/0x007D001E"); //:subject, :sender 
                Regex headers_re = new Regex(@"\n([^\s])");
                List<string> headers = new List<string>();
                string [] header_rows = headers_re.Split(mail_headers_string);
                headers.Add(header_rows[0]);  // First one is already complete
                for (int i=1; i< header_rows.Length-1; i+=2)
                {
                    // Subsequent ones are pairs to be joined together: 
                    // header_rows[1] = "R", header_rows[2] = "eceived: xxx@outlook.com",
                    // header_rows[3] = "F", header_rows[4] = "rom: example@mail.com"...
                    headers.Add(header_rows[i] + header_rows[i+1]);
                }
                mail_headers = headers.ToArray();
            }
            catch (System.Runtime.InteropServices.COMException err)
            {
                mail_headers = new string[0];
                Debug.WriteLine($"{err.Message}");
            }catch (System.Exception err)
            {
                mail_headers = new string[0];
                Debug.WriteLine($"{err.Message}");
            }

            MailData md = new MailData(
                id: mail.EntryID, 
                size: mail.Size, 
                subject: mail.Subject, 
                body: mail.Body.ToUpper(),  // non credo dovremmo mettere tutto in caps: abbiamo feature che si basano sul numero di
                                            // caratteri lower/upper-case, in più non vorrei si rompesse qualche regex
                htmlBody: mail.HTMLBody.ToUpper(), // stessa cosa qui
                sender: mail.SenderEmailAddress, 
                num_recipients: mail.Recipients.Count,
                headers: mail_headers,
                attachments: mail.Attachments
                //Add fields possibly required to compute features (e.g., attachments, headers)
            );
            
            return md;
        }

        #endregion
    }
}
