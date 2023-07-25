using log4net;
using log4net.Appender;
using Microsoft.Office.Interop.Outlook;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;

namespace PhishingDataCollector
{
    public partial class ThisAddIn
    {
        //public static HttpClientHandler httpHandler = new HttpClientHandler();
        public static HttpClient HTTPCLIENT = new HttpClient(); // (httpHandler);
        private static bool InExecution = false;
        private static int Progress;
        private static int N_Mails_To_Process;
        private static Stopwatch RuntimeWatch;
        private static readonly List<MailData> MailList = new List<MailData>(); // Initialize empty array to store the features of each email
        private static readonly bool _executeInParallel = false;
        private static readonly string AppName = "Auriga Mail Collector";
        private static readonly string ENDPOINT_BASE_URL = "http://212.189.202.20/email-collector-endpoint/public"; //"http://127.0.0.1:8000";
        private static readonly string ENDPOINT_TEST_URL = ENDPOINT_BASE_URL + "/api/test";
        private static readonly string ENDPOINT_UPLOAD_URL = ENDPOINT_BASE_URL + "/api/mail";
        private static readonly bool SAVE_FILE_NAME_SPACE = true;
        private LaunchRibbon taskPaneControl;

        // Variables initialized in the ThisAddIn_Startup function:
        public static ILog Logger;
        private static string RootDir;


        private void ThisAddIn_Startup(object sender, System.EventArgs e)
        {
            //Get the assembly information
            System.Reflection.Assembly assemblyInfo = System.Reflection.Assembly.GetExecutingAssembly();
            Uri uriCodeBase = new Uri(assemblyInfo.CodeBase);
            //Location is where the assembly is run from 
            //string assemblyLocation = assemblyInfo.Location;
            //CodeBase is the location of the ClickOnce deployment files
            RootDir = Path.GetDirectoryName(uriCodeBase.LocalPath.ToString()); ;  // ClickOnce folder - Release version
            DotEnv.Load(Path.Combine(RootDir, ".env"));  // Load .env file - if existing

            ConfigureLogger(RootDir);

            Environment.SetEnvironmentVariable("ROOT_FOLDER", RootDir);
            Environment.SetEnvironmentVariable("RESOURCE_FOLDER", Path.Combine(RootDir, "Resources"));
            Environment.SetEnvironmentVariable("OUTPUT_FOLDER", Path.Combine(RootDir, "output"));
            Environment.SetEnvironmentVariable("TEMP_FOLDER", Path.Combine(RootDir, "output", ".temp"));

            taskPaneControl = Globals.Ribbons.LaunchRibbon;
            taskPaneControl.RibbonType = "Microsoft.Outlook.Explorer";
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            //var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            //ServicePointManager.ServerCertificateValidationCallback += (s, cert, chain, sslPolicyErrors) => true;
            //ExecuteAddIn();
            // Python engine
            Runtime.PythonDLL = Path.Combine(Environment.GetEnvironmentVariable("RESOURCE_FOLDER"), "python", "python311.dll");
            PythonEngine.Initialize();
        }

        public static async void ExecuteAddIn()
        {
            if (InExecution)  // Prevent multiple instances running at the same time 
            {
                if (RuntimeWatch == null)
                {
                    MessageBox.Show("Il processo è già in esecuzione, attendi di ricevere una notifica.");
                    return;
                }
                long seconds_elapsed = RuntimeWatch.ElapsedMilliseconds / 1000;
                MessageBox.Show("Il processo è già in esecuzione! Riceverai una notifica al termine. " +
                    "\n" + (Progress - 1) + "/" + N_Mails_To_Process + " mail processate - In esecuzione da " + seconds_elapsed + " secondi." +
                    "\nNon chiudere il client di posta durante l'operazione.", AppName);
                return;
            }
            InExecution = true;
            Logger.Info("Add-in executed");
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
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
            catch
            {
                Logger.Warn("Not an exchange account");
            }

            List<MailItem> mailItems = new List<MailItem>();
            foreach (var folder in mailFolders)
            {
                mailItems.AddRange(from MailItem mail in folder.Items select mail);
            }

            // Prompt to user
            var tot_n_mails_to_process = mailItems.Count();
            List<RawMail> rawMailList = new List<RawMail>();
            int k = 0;
            int test_limiter = tot_n_mails_to_process;  // useful for TESTING purposes: limits the feature computation to N mails
            foreach (MailItem m in mailItems)
            {
                string mail_ID = m.EntryID;
                if (SAVE_FILE_NAME_SPACE)
                {
                    mail_ID = mail_ID.TrimStart('0');
                }
                // Checks that the mail has not already been computed previously
                if (!ExistingEmails.Contains(mail_ID))
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
            int n_emails_to_process = rawMailList.Count();
            Logger.Info("Extracted data from " + n_emails_to_process + " emails");

            if (n_emails_to_process > 0)
            {
                var showMessage = "Sono presenti " + n_emails_to_process + " mail da elaborare.\n" +
                "Il client di posta elettronica potrebbe subire rallentamenti per tutta la durata dell'elaborazione.\n" +
                "Il processo potrebbe durare diversi minuti, in base al numero di email e alla potenza di questo sistema.\n" +
                "Si prega di NON chiudere il client di posta durante l'operazione.\n" +
                "Iniziare il processo di esportazione?";
                var dialogResult = MessageBox.Show(showMessage, AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dialogResult == DialogResult.No)
                {
                    InExecution = false;
                    return;
                }
            }

            RuntimeWatch = Stopwatch.StartNew();

            var cts = new CancellationTokenSource();
            var po = new ParallelOptions
            {
                CancellationToken = cts.Token,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            try
            {
                N_Mails_To_Process = rawMailList.Count();
                Progress = 1;
                int batchSize = 10;
                int numBatches = (int)Math.Ceiling((double)N_Mails_To_Process / batchSize);
                if (_executeInParallel)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        Parallel.For(0, numBatches, i =>
                        {
                            Logger.Info("Batch " + (i + 1) + "/" + numBatches);
                            Parallel.ForEach(rawMailList.Skip(i * batchSize).Take(batchSize), po,
                                async m =>
                                {
                                    cts.Token.ThrowIfCancellationRequested();
                                    Logger.Info("Processing mail with ID: " + m.EntryID);

                                    MailData data = new MailData(m);
                                    // Extract features
                                    try
                                    {
                                        int completed = await Task.Run(() => data.ComputeFeatures()).
                                        ContinueWith((prevTask) =>
                                        {
                                            MailList.Add(data);
                                            SaveMail(data);
                                            Logger.Info($"Processed mail with ID: {data.GetID()}; {N_Mails_To_Process - Progress} remaining.");
                                            Progress++;
                                            return Progress;
                                        });
                                    }
                                    catch (System.Exception e)
                                    {
                                        Logger.Error("Problem processing mail with ID: " + data.GetID() + "\nError details: " + e.Message);
                                    }
                                }
                                );
                        });
                    }, DispatcherPriority.ApplicationIdle);
                }
                else  // Non-parallel computation
                {
                    foreach (RawMail m in rawMailList)
                    {
                        dispatcher.Invoke(() =>
                        {
                            MailData data = new MailData(m);
                            try
                            {
                                //int completed = await Task.Run(() => data.ComputeFeatures()).ContinueWith((task) => { return 1; } );
                                data.ComputeFeatures();
                                MailList.Add(data);
                                SaveMail(data);
                                Progress++;
                                Logger.Info($"Processed mail with ID: {data.GetID()}; {N_Mails_To_Process - Progress} remaining.");
                            }
                            catch (System.Exception e)
                            {
                                Logger.Error("Problem processing mail with ID: " + data.GetID() + "\nError details: " + e.Message);
                            }

                        }, DispatcherPriority.ApplicationIdle);
                    }
                }
                RuntimeWatch.Stop();
                MessageBox.Show("Esportazione dei dati estratti dalle email completata!\n" +
                    (Progress > 1 ? $"Processate {Progress - 1} email in {RuntimeWatch.ElapsedMilliseconds / 1000f} secondi.\n" : "") +
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
                            RuntimeWatch.Restart();
                            ExistingEmails = GetExistingEmails();
                            // MessageBox.Show("Upload dei dati iniziato.", AppName);
                            string file_ext = SAVE_FILE_NAME_SPACE ? "" : ".json";
                            bool result = await FileUploader.UploadFiles(ENDPOINT_UPLOAD_URL, ExistingEmails, cts, Environment.GetEnvironmentVariable("OUTPUT_FOLDER"), file_ext);
                            if (result)
                            {
                                MessageBox.Show("I dati sono stati trasmessi con successo (in " + RuntimeWatch.ElapsedMilliseconds + " ms)! Grazie", AppName);
                            }
                            else
                            {
                                MessageBox.Show("Problema nella trasmissione dei dati. Ti preghiamo di riprovare più tardi.", AppName);
                            }
                            InExecution = false;
                        }, DispatcherPriority.ApplicationIdle);
                    }
                    else
                    {
                        MessageBox.Show("Server temporaneamente non raggiungibile. Riprovare più tardi, grazie.", AppName);
                        InExecution = false;
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
                Logger.Error("(Main err): " + e);
                MessageBox.Show("Problema con l'esportazione dei dati. Dettagli errore: " + e.Message, AppName);
                InExecution = false;
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
                mail_headers = new string[0];
                Logger.Error($"Add-in COMException: {err.Message}");
            }
            catch (System.Exception err)
            {
                mail_headers = new string[0];
                Logger.Error($"Add-in GenericException: {err.Message}");
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

        private static string[] GetExistingEmails()
        {
            string[] email_names;
            try
            {
                var outputFolder = Environment.GetEnvironmentVariable("OUTPUT_FOLDER");
                if (!Directory.Exists(outputFolder))
                {
                    Directory.CreateDirectory(outputFolder);
                    return new string[] { };
                }
                if (SAVE_FILE_NAME_SPACE)
                {
                    email_names = Directory.EnumerateFiles(outputFolder)
                    .Select(Path.GetFileName)
                    .ToArray();  //FIXME
                    for (int i = 0; i < email_names.Length; i++)
                    {
                        email_names[i] = email_names[i].TrimStart('0');
                    }
                }
                else
                {
                    email_names = Directory.EnumerateFiles(outputFolder)
                    .Select(Path.GetFileNameWithoutExtension)  // In this case the .json extension needs to be removed
                    .ToArray();
                }

            }
            catch (System.Exception ex)
            {
                email_names = new string[] { };
                Logger.Error($"GetExistingEmails: {ex.Message}");
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
                string file_name;
                if (SAVE_FILE_NAME_SPACE)
                {
                    file_name = mail.GetID().TrimStart('0');  // removes trailing "0"s and doesn't add the json extension
                }
                else
                {
                    file_name = mail.GetID() + ".json";
                }
                file_name = Path.Combine(outputFolder, file_name);
                try
                {
                    using (StreamWriter writer = new StreamWriter(file_name))
                    {
                        string json = JsonSerializer.Serialize(mail, options);
                        writer.WriteLine(json);
                        writer.Close();
                    }
                }
                catch (PathTooLongException)
                {
                    Logger.Error($"SaveMails PathTooLongException: trying to write on a path with {file_name.Length} chars ({file_name}).");
                }
            }
        }

        private void ConfigureLogger(string log_base_path)
        {
            log4net.Config.XmlConfigurator.Configure();
            log4net.Repository.Hierarchy.Hierarchy h = (log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository();
            foreach (IAppender a in h.Root.Appenders)
            {
                if (a is FileAppender)
                {
                    FileAppender fa = (FileAppender)a;
                    // Programmatically set this to the desired location here
                    string logFileLocation = Path.Combine(log_base_path, "logs", "MailDataCollector.log");

                    // Uncomment the lines below if you want to retain the base file name
                    // and change the folder name...
                    //FileInfo fileInfo = new FileInfo(fa.File);
                    //logFileLocation = string.Format(@"C:\MySpecialFolder\{0}", fileInfo.Name);

                    fa.File = logFileLocation;
                    fa.ActivateOptions();
                    break;
                }
            }
            Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
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
