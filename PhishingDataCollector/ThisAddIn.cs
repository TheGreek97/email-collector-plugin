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
using Newtonsoft.Json.Serialization;
using System.Security.Cryptography;

namespace PhishingDataCollector
{
    public partial class ThisAddIn
    {
        //public static HttpClientHandler httpHandler = new HttpClientHandler();
        public static HttpClient HTTPCLIENT = new HttpClient(); // (httpHandler);

        private static readonly List<MailData> MailList = new List<MailData>(); // Initialize empty array to store the features of each email
        private static readonly bool _executeInParallel = true;
        private static readonly string AppName = "Auriga Mail Collector";
        private static readonly string ENDPOINT_TEST_URL = "http://127.0.0.1:8000/api/test";
        private static readonly string ENDPOINT_UPLOAD_URL = "http://127.0.0.1:8000/api/mail";
        //private static string outputFile = @"output\test.txt";

        private LaunchRibbon taskPaneControl;

        private void ThisAddIn_Startup(object sender, System.EventArgs e)
        {
            //Get the assembly information
            System.Reflection.Assembly assemblyInfo = System.Reflection.Assembly.GetExecutingAssembly();

            //Location is where the assembly is run from 
            //string assemblyLocation = assemblyInfo.Location;

            //CodeBase is the location of the ClickOnce deployment files
            Uri uriCodeBase = new Uri(assemblyInfo.CodeBase);
            string ClickOnceLocation = Path.GetDirectoryName(uriCodeBase.LocalPath.ToString());
            var uri = Path.Combine(ClickOnceLocation, "Resources", "dict", "it.dic");

            var fileStream = File.ReadAllText(uri);

            //MessageBox.Show($"assemblyLocation : {assemblyLocation}, clickOnceLocation : {ClickOnceLocation}");
            MessageBox.Show($" Array: {fileStream.Substring(0, 20)}"); // System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames())}");

            string workingDir = ClickOnceLocation;
            //string rootDir = Directory.GetParent(workingDir).Parent.FullName;  // Debug
            string rootDir = workingDir;  // Release
            var dotenv = Path.Combine(rootDir, "Outlook", ".env");
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
            List<MAPIFolder> mailFolders = new List<MAPIFolder> { 
                Globals.ThisAddIn.Application.Session.GetDefaultFolder(OlDefaultFolders.olFolderInbox),  // "inbox" folder
                Globals.ThisAddIn.Application.Session.GetDefaultFolder(OlDefaultFolders.olFolderDeletedItems),  // "deleted" folder
                Globals.ThisAddIn.Application.Session.GetDefaultFolder(OlDefaultFolders.olFolderJunk) // "junk" folder
            };
            try
            {
                mailFolders.Add(Globals.ThisAddIn.Application.Session.GetDefaultFolder(OlDefaultFolders.olPublicFoldersAllPublicFolders));
            }
            catch {
                Debug.WriteLine("Not an exchange account");
            }

            List<MailItem> mailItems = new List<MailItem>();
            foreach (var folder in mailFolders)
            {
                mailItems.AddRange(from MailItem mail in folder.Items select mail);
            }

            // Prompt to user
            var tot_n_mails_to_process = mailItems.Count();
            var showMessage = "Sono presenti " + tot_n_mails_to_process + " mail da elaborare.\n" +
            "Il client di posta elettronica potrebbe non essere disponibile per tutta la durata dell'esportazione.\n" +
            "Il processo potrebbe durare più di un'ora, in base al numero di email e alla potenza di questo sistema.\n" +
            "Sei sicuro di voler iniziare il processo di esportazione?";
            var dialogResult = MessageBox.Show(showMessage, AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dialogResult == DialogResult.No)
            {
                return;
            }
           
            List<RawMail> rawMailList = new List<RawMail>();
            int k = 0;
            int test_limiter = tot_n_mails_to_process;  // TEST ONLY: Limit the feature computation to N mails
            foreach (MailItem m in mailItems)  // See both inbox and junk emails
            {
                // Checks that the mail has not already been computed previously
                if (! ExistingEmails.Contains(m.EntryID))
                {
                    if (k < test_limiter)  
                    {
                        dispatcher.Invoke(() =>
                        {
                            RawMail raw = ExtractRawDataFromMailItem(m);
                            rawMailList.Add(raw);
                        }, DispatcherPriority.ApplicationIdle);
                        k++;
                    }
                }
            }
            MessageBox.Show("L'esportazione dei dati inizierà a breve. Sono presenti " + rawMailList.Count() + " mail da elaborare.\n" +
            "È preferibile non interagire con la casella di posta elettronica per tutta la durata dell'esportazione.\n" +
            "Al termine di quest'ultima, sarà mostrata una notifica. Il processo potrebbe durare più di un'ora, in base al numero di email e alla potenza di questo sistema.", "Phishing Data Collector");/**/

            var watch = Stopwatch.StartNew();

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
                int batchSize = 10;
                int numBatches = (int)Math.Ceiling((double)tot_n_mails / batchSize);
                //if (_executeInParallel){
                await dispatcher.InvokeAsync(() =>
                {
                    for (int i = 0; i < numBatches; i++)
                    {
                        Debug.WriteLine("Batch {0}/{1}", i + 1, numBatches);
                        Parallel.ForEach(rawMailList.Skip(i * batchSize).Take(batchSize), po,
                            async m =>
                            {
                                cts.Token.ThrowIfCancellationRequested();
                                Debug.WriteLine("Processing mail with ID: " + m.EntryID, progress);

                                    MailData data = new MailData(m);
                                    // Extract features
                                    int completed = await Task.Run(() => data.ComputeFeatures()).
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
                                        return progress;
                                    });
                                }
                            );
                        }
                    });
                /*} else
                {
                    foreach (RawMail m in rawMailList) { 
                        MailData data = new MailData(m);
                        data.ComputeFeatures();
                        MailList.Add(data);
                        Debug.WriteLine("Processed mail with ID: " + data.GetID());
                        Debug.WriteLine("{0} Remaining", tot_n_mails - progress);
                        dispatcher.Invoke(() =>
                        {
                            SaveMail(data);
                        });
                        progress++;
                    }
                }*/
                watch.Stop();
                MessageBox.Show("Esportazione dei dati estratti dalle email completata!" +
                    "\nProcessate " + (progress - 1) + " email in " + watch.ElapsedMilliseconds/1000 + " s.\n" +
                    "I dati saranno ora spediti ai nostri server per scopi di ricerca e trattati ai sensi della GDPR.\n" +
                    "I dati raccolti risultano da un processo di elaborazione delle email della casella di posta e sono completamente anonimi, " +
                    "in quanto non è possibile risalire al contenuto originale delle email o ai soggetti coinvolti.",
                    AppName);

                // Data trasmission over HTTPS
                try
                {
                    bool connectionOK = await FileUploader.TestConnection(ENDPOINT_TEST_URL);

                    if (connectionOK)
                    {
                        await dispatcher.InvokeAsync(async () =>
                        {
                            watch.Restart();
                            ExistingEmails = GetExistingEmails();
                            // MessageBox.Show("Upload dei dati iniziato.", AppName);
                            bool result = await FileUploader.UploadFiles(ENDPOINT_UPLOAD_URL, ExistingEmails, cts, Environment.GetEnvironmentVariable("OUTPUT_FOLDER"));
                            if (result)
                            {
                                MessageBox.Show("I dati sono stati trasmessi con successo (in " + watch.ElapsedMilliseconds + " ms)! Grazie", AppName);
                            }
                            else
                            {
                                MessageBox.Show("Problema nella trasmissione dei dati. Ti preghiamo di riprovare più tardi.", AppName);
                            }
                        });
                    }
                    else
                    {
                        MessageBox.Show("Server temporaneamente non raggiungibile. Riprovare più tardi, grazie.", AppName);
                        return;
                    }
                }
                catch (System.Exception e)
                {
                    MessageBox.Show("Problema nella trasmissione dei dati. Ti preghiamo di riprovare. Dettagli errore: " + e.Message, AppName);
                }
            }
            catch (System.Exception e)
            {
                Debug.WriteLine("Errore esterno:");
                Debug.WriteLine(e);
                Debug.WriteLine(e.StackTrace);
                MessageBox.Show("Problema con l'esportazione dei dati. Dettagli errore: " +e.Message, AppName);
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
                string mail_headers_string = mail.PropertyAccessor.GetProperty("http://schemas.microsoft.com/mapi/proptag/0x007D001E");  // :subject, :sender 
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
                attachments: attachments
                );
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

        // Wrapper for saving 1 email through SaveMails
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
            // deve essere eseguito all'arresto di Outlook, vedere https://go.microsoft.com/fwlink/?LinkId=506785
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
