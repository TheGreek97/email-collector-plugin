﻿using com.sun.tools.@internal.xjc.generator.util;
using log4net;
using log4net.Appender;
using Microsoft.Office.Interop.Outlook;
using Microsoft.Scripting.Generation;
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
        private static bool UploadingFiles = false;
        private static int MailProgress;
        private static int N_Mails_To_Process;
        private static Stopwatch RuntimeWatch;
        private static readonly List<MailData> MailList = new List<MailData>(); // Initialize empty array to store the features of each email
        private static readonly bool _executeInParallel = false;
        private static readonly string AppName = "Auriga Mail Collector";
        private static readonly string ENDPOINT_BASE_URL = "http://212.189.202.20/email-collector-endpoint/public"; // "http://127.0.0.1:8000";
        private static readonly string ENDPOINT_TEST_URL = ENDPOINT_BASE_URL + "/api/test";
        private static readonly string ENDPOINT_UPLOAD_URL = ENDPOINT_BASE_URL + "/api/mail";
        private static readonly bool SAVE_FILENAME_SPACE = true;  // If true, the filenames of the email features will be shortened and lack the extension
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
            Environment.SetEnvironmentVariable("OUTPUT_FOLDER", Path.Combine(RootDir, "out"));
            Environment.SetEnvironmentVariable("TEMP_FOLDER", Path.Combine(RootDir, "out", ".tmp"));

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
                if (UploadingFiles)
                {
                    MessageBox.Show("Il processo è già in esecuzione! I dati sono in fase di caricamento verso i nostri server. Riceverai una notifica al termine." +
                    "\nNon chiudere il client di posta durante l'operazione.", AppName);
                    return;
                }
                long seconds_elapsed = RuntimeWatch.ElapsedMilliseconds / 1000;  // int value
                MessageBox.Show("Il processo è già in esecuzione! Riceverai una notifica al termine." +
                    "\n" + (MailProgress - 1) + "/" + N_Mails_To_Process + " mail processate - In esecuzione da " + seconds_elapsed + " secondi." +
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
            catch (System.Runtime.InteropServices.COMException)
            {
                Logger.Warn("Not an exchange account");
            }

            var mailItems = new List<(MailItem, string)>();
            foreach (var folder in mailFolders)
            {
                mailItems.AddRange(from MailItem mail in folder.Items select (mail, folder.Name));
            }

            // Prompt to user
            var tot_n_mails_to_process = mailItems.Count();
            List<RawMail> rawMailList = new List<RawMail>();
            int k = 0;
            int test_limiter = tot_n_mails_to_process;  // useful for TESTING purposes: limits the feature computation to N mails
            foreach ((MailItem m, string folder_name) in mailItems)
            {
                string mail_ID = m.EntryID;
                if (SAVE_FILENAME_SPACE)
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
                            RawMail raw = ExtractRawDataFromMailItem(m, folder_name);
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


            /* Feature Extraction from Mails */
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
                MailProgress = 1;
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
                                            Logger.Info($"Processed mail with ID: {data.GetID()}; {N_Mails_To_Process - MailProgress} remaining.");
                                            MailProgress++;
                                            return MailProgress;
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
                                MailProgress++;
                                Logger.Info($"Processed mail with ID: {data.GetID()}; {N_Mails_To_Process - MailProgress} remaining.");
                            }
                            catch (System.Exception e)
                            {
                                Logger.Error("Problem processing mail with ID: " + data.GetID() + "\nError details: " + e.Message);
                            }

                        }, DispatcherPriority.ApplicationIdle);
                    }
                }
                // feature extraction process ended
                ExistingEmails = GetExistingEmails();  // retrieve all the computed emails 
                string[] EmailsToUpload = GetEmailsToUpload(ExistingEmails);  // check which of them must be uploaded
                RuntimeWatch.Stop();
                string msg = "";
                if (MailProgress > 1)  // 1 or more mails have been processed 
                {
                    msg = "Esportazione dei dati estratti dalle email completata!\n" +
                        $"Processate {MailProgress - 1} email in {Math.Round(RuntimeWatch.ElapsedMilliseconds / 1000f, 2)} secondi.\n";
                }
                if (EmailsToUpload.Length > 0)  // 1 or more mails must be trasmitted to the endpoint 
                {
                    msg += "I dati (ricavati da " + EmailsToUpload.Length + " email) saranno ora spediti ai nostri server per scopi di ricerca e trattati ai sensi della GDPR.\n" +
                    "I dati raccolti risultano da un processo di elaborazione delle email della casella di posta e sono completamente anonimi, " +
                    "in quanto non è possibile risalire al contenuto originale delle email o ai soggetti coinvolti.";
                    MessageBox.Show(msg, AppName);
                } 
                else  // No mails to transmit -> the program can be closed
                {
                    InExecution = false;
                    MessageBox.Show("Tutti i dati sono già stati estratti e caricati sui nostri server. Grazie!\n" +
                        "È comunque possibile ri-lanciare la procedura dopo aver ricevuto nuove email.", AppName);
                    return;
                }
                

                /* Data trasmission to the end-point over HTTP(S) */
                try
                {
                    string[] successfullyUploadedMails;
                    UploadingFiles = true;
                    bool connectionOK = await FileUploader.TestConnection(ENDPOINT_TEST_URL);

                    if (connectionOK)
                    {
                        await dispatcher.InvokeAsync(async () =>
                        {
                            RuntimeWatch.Restart();
                            // MessageBox.Show("Upload dei dati iniziato.", AppName);
                            string file_ext = SAVE_FILENAME_SPACE ? "" : ".json";
                            bool result;
                            (result, successfullyUploadedMails) = await FileUploader.UploadFiles(ENDPOINT_UPLOAD_URL, EmailsToUpload, cts, Environment.GetEnvironmentVariable("OUTPUT_FOLDER"), file_ext);
                            if (result)
                            {
                                MessageBox.Show($"Tutti i dati sono stati trasmessi con successo ({successfullyUploadedMails.Length} file caricati in {Math.Round(RuntimeWatch.ElapsedMilliseconds/1000f, 2)} s). Grazie!", AppName);
                            }
                            else if (successfullyUploadedMails.Length != 0 && successfullyUploadedMails.Length < EmailsToUpload.Length)  // some mail has been trasmitted
                            {
                                MessageBox.Show($"Problema nella trasmissione di {successfullyUploadedMails.Length - EmailsToUpload.Length} file su {EmailsToUpload.Length} totali ({successfullyUploadedMails.Length} file trasmessi correttamente)." +
                                    $"\nTi preghiamo di riprovare più tardi.", AppName);
                            }
                            else  // if no mail has been trasmitted successfully
                            {
                                MessageBox.Show("Problema nella trasmissione dei dati. Ti preghiamo di riprovare più tardi.", AppName);
                            }
                            SaveUploadedEmails(successfullyUploadedMails);
                            InExecution = false;
                            UploadingFiles = false;
                        }, DispatcherPriority.ApplicationIdle);
                    }
                    else
                    {
                        MessageBox.Show("Server temporaneamente non raggiungibile. Riprovare più tardi, grazie.", AppName);
                        InExecution = false;
                        UploadingFiles = false;
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
                UploadingFiles = false;
            }
            finally
            {
                cts.Dispose();
            }
            return;
        }

        private static RawMail ExtractRawDataFromMailItem(MailItem mail, string folder_name)
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
                attachments: attachments,
                read: !mail.UnRead,
                folderName: folder_name
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
                if (SAVE_FILENAME_SPACE)
                {
                    email_names = Directory.EnumerateFiles(outputFolder)
                    .Select(Path.GetFileName)
                    .ToArray();
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

        private static string[] GetEmailsToUpload(string[] existingEmails)
        {
            string[] uploadedEmails;
            try
            {
                string uploadedFolder = Path.Combine(Environment.GetEnvironmentVariable("OUTPUT_FOLDER"), "up");
                if (!Directory.Exists(uploadedFolder))
                {
                    Directory.CreateDirectory(uploadedFolder);
                    return existingEmails;
                }
                if (SAVE_FILENAME_SPACE)
                {
                    uploadedEmails = Directory.EnumerateFiles(uploadedFolder)
                    .Select(Path.GetFileName)  // the files don't have the extension ".json"
                    .ToArray();  
                }
                else
                {
                    uploadedEmails = Directory.EnumerateFiles(uploadedFolder)
                    .Select(Path.GetFileNameWithoutExtension)  // In this case the .json extension needs to be removed
                    .ToArray();
                }

            }
            catch (System.Exception ex)
            {
                uploadedEmails = existingEmails;
                Logger.Error($"GetEmailsToUpload: {ex.Message}");
            }
            return existingEmails.Except(uploadedEmails).ToArray();  // subtraction set between Existing emails and Uploaded emails
        }

        private static void SaveUploadedEmails(string[] uploadedEmails, string ext = ".json")
        {
            try
            {
                string outputFolder = Environment.GetEnvironmentVariable("OUTPUT_FOLDER");
                string uploadedFolder = Path.Combine(outputFolder, "up");
                if (!Directory.Exists(uploadedFolder))
                {
                    Directory.CreateDirectory(uploadedFolder);
                }
                foreach (var file in uploadedEmails)
                {
                    string file_name = file;
                    if (!SAVE_FILENAME_SPACE)  // if the files have the extension, add it
                    {
                        file_name += ".json";
                    }
                    /* To save space on disk we can avoid copying the files' content
                    string fileToMove = Path.Combine(outputFolder, file_name);
                    string moveTo = Path.Combine(uploadedFolder, file_name);
                    File.Copy(fileToMove, moveTo);
                    */
                    File.Create(Path.Combine(uploadedFolder, file_name)).Dispose();  // creates an empty file and closes its stream
                }

            }
            catch (System.Exception ex)
            {
                Logger.Error($"SaveUploadedEmails: {ex.Message}");
                Debug.WriteLine($"SaveUploadedEmails: {ex.Message}");
            }
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
                if (SAVE_FILENAME_SPACE)
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
