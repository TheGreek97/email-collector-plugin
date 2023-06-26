using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using Microsoft.Office.Interop.Outlook;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Windows.Forms;
using System;
using System.Net;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using System.Security.Cryptography;

namespace PhishingDataCollector
{
    public partial class ThisAddIn
    {
        public static HttpClient HTTPCLIENT = new HttpClient();

        private static List<MailData> MailList = new List<MailData>(); // Initialize empty array to store the features of each email
        private static string outputFile = @"output\test.txt";
        private static bool executeInParallel = false;

        private LaunchRibbon taskPaneControl;

        private void ThisAddIn_Startup(object sender, System.EventArgs e)
        {
            string workingDir= Directory.GetCurrentDirectory();
            string rootDir = Directory.GetParent(workingDir).Parent.FullName;
            var dotenv = Path.Combine(rootDir, ".env");
            DotEnv.Load(dotenv);
            outputFile = Environment.GetEnvironmentVariable("DEBUG_OUTPUT_FILE");
            //var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            taskPaneControl = Globals.Ribbons.LaunchRibbon;
            taskPaneControl.RibbonType = "Microsoft.Outlook.Explorer";
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            //ServicePointManager.ServerCertificateValidationCallback += (s, cert, chain, sslPolicyErrors) => true;
            //ExecuteAddIn();
        }

        public static async void ExecuteAddIn()
        {
            
            var dispatcher = Dispatcher.CurrentDispatcher;
            // Get the mail list
            MAPIFolder inbox = Globals.ThisAddIn.Application.Session.GetDefaultFolder(OlDefaultFolders.olFolderInbox);  
            MAPIFolder junk = Globals.ThisAddIn.Application.Session.GetDefaultFolder(OlDefaultFolders.olFolderJunk); 
            IEnumerable<MailItem> mailList = from MailItem mail in inbox.Items select mail; 
            IEnumerable<MailItem> mailListJunk = from MailItem mail in junk.Items select mail; 
            /*MessageBox.Show("L'esportazione dei dati inizierà ora. Sono presenti " + mailList.Count() + " mail da analizzare. \n" +
            "È preferibile non interagire con la casella di posta elettronica per tutta la durata dell'esportazione. " +
            "Al termine di quest'ultima, sarà mostrata una notifica.", "Phishing Data Collector");*/

            List<RawMail> rawMailList = new List<RawMail>();
            int k = 0;
            foreach (MailItem m in mailList)
            {
                // TODO: Salva su file gli ID delle email già computate e, prima di ri-estrarre le feature, controlla che l'ID non sia presente su quel file    
                if (k < 20)  // Limiter = 20 mails
                {
                    RawMail raw = ExtractRawDataFromMailItem(m);
                    rawMailList.Add(raw);
                    k++;
                }
            }
            var cts = new CancellationTokenSource();
            var po = new ParallelOptions
            {
                CancellationToken = cts.Token,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };
            try
            {
                int tot_n_mails = rawMailList.Count();
                int progress = 1;
                var batchSize = 5;
                int numBatches = (int)Math.Ceiling((double)tot_n_mails / batchSize);
                if (executeInParallel)
                {
                    Parallel.For(0, numBatches, i =>
                    {
                        Debug.WriteLine("Batch {0}/{1}", i + 1, numBatches);
                        Parallel.ForEach(rawMailList.Skip(i * batchSize).Take(batchSize), po,
                            async m =>
                            {
                                cts.Token.ThrowIfCancellationRequested();
                                Debug.WriteLine("Processing mail with ID: " + m.EntryID, progress);

                                MailData data = new MailData(m);
                                await Task.Run(() => data.ComputeFeatures()).
                                ContinueWith((prevTask) =>
                                {
                                    MailList.Add(data);
                                    Debug.WriteLine("Processed mail with ID: " + data.ID);
                                    Debug.WriteLine("{0} Remaining", tot_n_mails - progress);
                                    if (tot_n_mails - progress == 0)
                                    {
                                        dispatcher.Invoke(() =>
                                        {
                                            MessageBox.Show("Esportazione dei dati completata! Grazie", "Phishing Mail Data Collector");
                                            WriteMailsToFile();
                                        });
                                    }
                                    progress++;
                                    return;
                                });
                            }
                        );
                    });
                } else
                {
                    foreach (RawMail m in rawMailList) { 
                        MailData data = new MailData(m);
                        data.ComputeFeatures();
                        MailList.Add(data);
                        Debug.WriteLine("Processed mail with ID: " + data.ID);
                        Debug.WriteLine("{0} Remaining", tot_n_mails - progress);
                        progress++;
                    }
                    MessageBox.Show("Esportazione dei dati completata! Grazie", "Phishing Mail Data Collector");
                    WriteMailsToFile();
                }
            }
            catch (System.Exception e)
            {
                Debug.WriteLine(e.Message);
            }
            finally
            {
                cts.Dispose();
            }
            return;
        }

        private static RawMail ExtractRawDataFromMailItem(MailItem mail)
        {
            // Get headers from MailItem
            string[] mail_headers;
            try
            {
                string mail_headers_string = mail.PropertyAccessor.GetProperty("http://schemas.microsoft.com/mapi/proptag/0x007D001E"); //:subject, :sender 
                Regex headers_re = new Regex(@"\n([^\s])");
                List<string> headers = new List<string>();
                string[] header_rows = headers_re.Split(mail_headers_string);
                headers.Add(header_rows[0]);  // First one is already complete
                for (int i = 1; i < header_rows.Length - 1; i += 2)
                {
                    // Subsequent ones are pairs to be joined together: 
                    // header_rows[1] = "R", header_rows[2] = "eceived: xxx@outlook.com",
                    // header_rows[3] = "F", header_rows[4] = "rom: example@mail.com"...
                    headers.Add(header_rows[i] + header_rows[i + 1]);
                }
                mail_headers = headers.ToArray();
            }
            catch (System.Runtime.InteropServices.COMException err)
            {
                Debug.WriteLine("Add-in COMException: ");
                mail_headers = new string[0];
                Debug.WriteLine($"{err.Message}");
            }
            catch (System.Exception err)
            {
                Debug.WriteLine("Add-in Generic Exception: ");
                mail_headers = new string[0];
                Debug.WriteLine($"{err.Message}");
            }

            // Get attachments representation from MailItem (Hash)
            AttachmentData[] attachments;
            List<AttachmentData> attachments_list = new List<AttachmentData>();
            foreach (Attachment att in mail.Attachments)
            {
                AttachmentData att_data = AttachmentData.ExtractFeatures(att);
                if (att_data != null)
                {
                    attachments_list.Add(att_data);
                }
            }
            attachments = attachments_list.ToArray();

            RawMail rawMail = new RawMail(
                id: mail.EntryID,
                size: mail.Size,
                subject: mail.Subject,
                body: mail.Body,
                htmlBody: mail.HTMLBody,
                sender: mail.SenderEmailAddress,
                numRecipients: mail.Recipients.Count,
                headers: mail_headers,
                attachments: attachments);
            return rawMail;
        }

        private static void WriteMailsToFile(string outputFile = null)
        {
            if (outputFile == null)
            {
                outputFile = Environment.GetEnvironmentVariable("OUTPUT_FILE");
            }
            var options = new JsonSerializerOptions
            {
                IncludeFields = true
            };
            using (StreamWriter writer = new StreamWriter(outputFile))
            {
                try
                {
                    string json = JsonSerializer.Serialize(MailList, options);
                    //TODO: do not save the enitre MailData object, but just the features
                    writer.WriteLine(json);
                }
                catch (ArgumentException err)
                {
                    Debug.WriteLine(MailList[0]);
                    Debug.WriteLine(err);
                }
                writer.Close();
            }
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
            Startup += new System.EventHandler(ThisAddIn_Startup);
            Shutdown += new System.EventHandler(ThisAddIn_Shutdown);
        }


        #endregion
    }
}
