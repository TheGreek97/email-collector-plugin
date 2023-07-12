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
using javax.crypto;

namespace PhishingDataCollector
{
    public partial class ThisAddIn
    {
        public static System.Net.Http.HttpClient HTTPCLIENT = new System.Net.Http.HttpClient();

        private static readonly List<MailData> MailList = new List<MailData>(); // Initialize empty array to store the features of each email
        private static readonly bool _executeInParallel = false;
        private static readonly string AppName = "Phishing Mail Data Collector";
        //private static string outputFile = @"output\test.txt";

        private LaunchRibbon taskPaneControl;

        private void ThisAddIn_Startup(object sender, System.EventArgs e)
        {
            string workingDir= Directory.GetCurrentDirectory();
            string rootDir = Directory.GetParent(workingDir).Parent.FullName;
            var dotenv = Path.Combine(rootDir, ".env");
            DotEnv.Load(dotenv);
            taskPaneControl = Globals.Ribbons.LaunchRibbon;
            taskPaneControl.RibbonType = "Microsoft.Outlook.Explorer";
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            //var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            //ServicePointManager.ServerCertificateValidationCallback += (s, cert, chain, sslPolicyErrors) => true;
            //ExecuteAddIn();
        }

        public static async void ExecuteAddIn()
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            // Get the list of already processed emails (if the plugin was previously executed)
            string[] ExistingEmails = GetExistingEmails();
            
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
            int test_limiter = 10;  // TEST ONLY: Limiter = 20 mails
            foreach (MailItem m in mailList.Concat(mailListJunk))  // See both inbox and junk emails
            {
                // Checks that the mail has not already been computed previously
                if (! ExistingEmails.Contains(m.EntryID))
                {
                    if (k < test_limiter)  
                    {
                        RawMail raw = ExtractRawDataFromMailItem(m);
                        rawMailList.Add(raw);
                        k++;
                    }
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
                if (_executeInParallel)
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
                                    Debug.WriteLine("Processed mail with ID: " + data.GetID());
                                    Debug.WriteLine("{0} Remaining", tot_n_mails - progress);
                                    /*if (tot_n_mails - progress == 0)
                                    {
                                        dispatcher.Invoke(() =>
                                        {
                                            SaveMails(MailList.ToArray());
                                        });
                                    }*/
                                    progress++;
                                    SaveMail(data);
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
                        Debug.WriteLine("Processed mail with ID: " + data.GetID());
                        Debug.WriteLine("{0} Remaining", tot_n_mails - progress);
                        SaveMail(data);
                        progress++;
                    }
                }
                MessageBox.Show("Esportazione dei dati estratti dalle email completata! I dati saranno ora " +
                    "mandati ai nostri server per scopi di ricerca e trattati ai sensi della GDPR.\n " +
                    "I dati raccolti risultano da un processo di elaborazione delle email della tua casella di posta e sono completamente anonimi, " +
                    "in quanto non è possibile risalire al contenuto originale delle email o ai soggetti coinvolti.",
                    AppName);

                // Data trasmission over HTTPS
                try { 
                    var url = "http://127.0.0.1:8000/api/mail";
                    ExistingEmails = GetExistingEmails();
                    MessageBox.Show("Upload dei dati iniziato.", AppName);
                    await FileUploader.UploadFiles(url, ExistingEmails, cts, Environment.GetEnvironmentVariable("OUTPUT_FOLDER"))
                        .ContinueWith(task => {
                            if (task.IsCompleted) {  // FIXME: stampa sempre un messaggio di successo, nonostante le eccezioni lanciate in UploadFiles()
                                MessageBox.Show("I dati sono stati trasmessi con successo! Grazie", AppName);
                            } else
                            {
                                MessageBox.Show("Problema nella trasmissione dei dati. Ti preghiamo di riprovare più tardi.", AppName);
                            }
                        });
                } catch (System.Exception e)
                {
                    MessageBox.Show("Problema nella trasmissione dei dati. Ti preghiamo di riprovare. Dettagli errore: "+ e.Message, AppName);
                }
            }
            catch (System.Exception e)
            {
                Debug.WriteLine("Errore esterno:");
                Debug.WriteLine(e);
                Debug.WriteLine(e.StackTrace);
                MessageBox.Show("Problema con l'esportazione dei dati. Dettagli errore:" +e.Message, AppName);
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
                    // Subsequent ones are pairs to be joined together. Example:
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
            Attachment[] atts = mail.Attachments.Cast<Attachment>().ToArray();
            foreach (Attachment att in atts) //mail.Attachments)
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

        private static string[] GetExistingEmails ()
        {
            string[] email_names;
            try
            {
                email_names = Directory.EnumerateFiles(Environment.GetEnvironmentVariable("OUTPUT_FOLDER"))
                    .Select(Path.GetFileNameWithoutExtension).ToArray();
            }
            catch (System.Exception ex)
            {
                email_names = new string[] { };
                Debug.WriteLine(ex);
            }
            return email_names;
        }

        private static void SaveMail(MailData mail, string outputFolder = null)
        {
            SaveMails(new MailData[1] { mail }, outputFolder);
        }


        private static void SaveMails(MailData[] mails, string outputFolder = null)
        {
            if (outputFolder == null)
            {
                outputFolder = Environment.GetEnvironmentVariable("OUTPUT_FOLDER");
            }
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }
            var options = new JsonSerializerOptions
            {
                IncludeFields = true
            };
            foreach (MailData mail in mails)
            {
                using (StreamWriter writer = new StreamWriter(Path.Combine(outputFolder, mail.GetID()+".json")))  //saves in folder/id.json
                {
                    try
                    {
                        string json = JsonSerializer.Serialize(mail, options);
                        writer.WriteLine(json);
                    }
                    catch (ArgumentException err)
                    {
                        Debug.WriteLine(err);
                    }
                    writer.Close();
                }
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
