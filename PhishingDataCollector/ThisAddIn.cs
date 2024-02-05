/***
 *  This file is part of Dataset-Collector.

    Dataset-Collector is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Dataset-Collector is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Dataset-Collector.  If not, see <http://www.gnu.org/licenses/>. 
 * 
 * ***/

using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Appender;
using Microsoft.Office.Interop.Outlook;
using PhishingDataCollector.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dasync.Collections;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Deployment.Application;
using System.Reflection;
using System.Text.Json.Serialization;

namespace PhishingDataCollector
{
    public partial class ThisAddIn
    {
        //public static HttpClientHandler httpHandler = new HttpClientHandler();
        public static HttpClient HTTPCLIENT = new HttpClient(); // (httpHandler);
        public static int EMAIL_LIMIT = 10000;
        public static DateTime DATE_LIMIT = new DateTime(2013, 1, 1);  // Year limit for collection is 2013

        public static string POS_PATH;

        public static bool InExecution = false;
        public static bool UploadingFiles = false;
        private static int MailProgress;
        private static int N_Mails_To_Process;
        private static int N_Mails_To_Upload;
        private static Stopwatch RuntimeWatch;
        private static readonly List<MailData> MailList = new List<MailData>(); // Initialize empty array to store the features of each email
        private static readonly string AppName = "Dataset Collector";
        private static readonly bool LIMIT_FILENAME_SPACE = true;  // If true, the filenames of the email features will be shortened and lack the extension
        private static LaunchRibbon TaskPaneControl;
        private static bool ExecuteInMultithread = true;  // Tells the plugin to use more HW resources (CPU and RAM) to speed up the process

        // Variables initialized in the ThisAddIn_Startup function:
        public static string ENDPOINT_BASE_URL;
        public static ILog Logger;
        public static Guid ClientID;
        public static NameSpace MapiNs;  // the namespace of the mapi
        private static string ENDPOINT_TEST_URL;
        private static string ENDPOINT_UPLOAD_URL;
        private static string ENDPOINT_SENDLOGS_URL;
        private static string RootDir;
        private static string LogFilePath;
        private static DialogResult sendLogs;

        public static Dictionary<string, string> HashesFromMailID = new Dictionary<string, string>();

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
            ClientID = GetClientID();

            Logger.Info("Plugin started - Client ID : " + ClientID.ToString());
            MapiNs = Globals.ThisAddIn.Application.GetNamespace("MAPI");

            TaskPaneControl = Globals.Ribbons.LaunchRibbon;
            TaskPaneControl.RibbonType = "Microsoft.Outlook.Explorer";
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            // Extract Python POS tagger for Italian
            POS_PATH = Path.Combine(Environment.GetEnvironmentVariable("RESOURCE_FOLDER"), "POS");
            if (!File.Exists(Path.Combine(POS_PATH, "flag"))) {  // in the zip file there is a "flag" file (if it is already extracted, avoid extracting the zip again)
                string python_zip_path = Path.Combine(POS_PATH, "posTagger_it.zip");
                ZipUtils.Extract(python_zip_path, null, POS_PATH);
                //if (File.Exists(python_zip_path)) File.Delete(python_zip_path);  // delete the archive to free up space
            }

            ENDPOINT_BASE_URL = Environment.GetEnvironmentVariable("ENDPOINT_BASE_URL");
            ENDPOINT_TEST_URL = ENDPOINT_BASE_URL + "/api/test";
            ENDPOINT_UPLOAD_URL = ENDPOINT_BASE_URL + "/api/mail";
            ENDPOINT_SENDLOGS_URL = ENDPOINT_BASE_URL + "/api/logs";
            //var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            //ServicePointManager.ServerCertificateValidationCallback += (s, cert, chain, sslPolicyErrors) => true;
            //ExecuteAddIn();
        }
        public static void ShowClientID()
        {
            Clipboard.SetText(ClientID.ToString());
            MessageBox.Show($"Il tuo ID client è:\n\n" +
                ClientID + "\n\n" +
                "Il client ID è stato salvato nei tuoi appunti, premi CTRL+V (o click destro > \"Incolla\") per incollarlo in un qualsiasi editor di testo.\n" +
                $"Comunicacelo nel caso ti sia richiesto per svolgere alcune operazioni di amministrazione dei tuoi dati.", AppName);
            return;
        }

        public static void ShowStatus()
        {
            if (InExecution)  // Prevent multiple instances running at the same time 
            {
                if (RuntimeWatch == null)
                {
                    MessageBox.Show("Il processo è in esecuzione, attendi di ricevere una notifica. Stiamo organizzando le email della tua casella per l'elaborazione.", AppName);
                    return;
                }
                if (UploadingFiles)
                {
                    MessageBox.Show($"Il processo è in esecuzione! I dati sono in fase di caricamento verso i nostri server ({FileUploader.NSentEmail}/{N_Mails_To_Upload} file caricati).\nRiceverai una notifica al termine." +
                    "\nNon chiudere il client di posta durante l'operazione.", AppName);
                    return;
                }
                long seconds_elapsed = RuntimeWatch.ElapsedMilliseconds / 1000;  // int value
                MessageBox.Show("Il processo è in esecuzione! Riceverai una notifica al termine." +
                    "\n" + (MailProgress - 1) + "/" + N_Mails_To_Process + " mail processate - In esecuzione da " + seconds_elapsed + " secondi." +
                    "\nNon chiudere il client di posta durante l'operazione.", AppName);
                return;
            } else
            {
                var mails_to_upload = GetEmailsToUpload();
                if (mails_to_upload.Length > 0)
                {
                    MessageBox.Show($"Il processo non è ancora in esecuzione.\n" +
                        $"Sono presenti dati processati che non sono stati ancora trasmessi ai nostri server. " +
                        $"Clicca sul tasto \"{TaskPaneControl.LaunchPluginBtn.Label}\" per riprendere il processo.", AppName);
                }
                else
                {
                    MessageBox.Show($"Il processo non è ancora in esecuzione.\n" +
                        $"Per lanciare il plugin clicca sul tasto \"{TaskPaneControl.LaunchPluginBtn.Label}\".", AppName);
                    return;
                }
            }
        }

        public static async void ExecuteAddIn()
        {
            if (InExecution)  // Prevent multiple instances running at the same time 
            {
                ShowStatus();
                return;
            }
            // Change the ribbon buttons + setup vars
            //TaskPaneControl.LaunchPluginBtn.Enabled = false;
            //TaskPaneControl.StateBtn.Visible = true;
            //System.Windows.Forms.Application.DoEvents();
            InExecution = true;
            Logger.Info("Add-in executed");
            var dispatcher = Dispatcher.CurrentDispatcher;
            //FileUploader.SendLogs(LogFilePath, ENDPOINT_SENDLOGS_URL);

            sendLogs = DialogResult.None;

            // Get the list of already processed emails (if the plugin was previously executed)
            string[] ExistingEmails = GetExistingEmails();

            // Prompt to user
            var dialogResult = MessageBox.Show("Grazie per la tua partecipazione alla raccolta dati.\n" +
                "Stiamo sistemando le email della tua casella di posta per estrarre e collezionare i dati anonimi.\n" +
                "Ti ricordiamo che la tua partecipazione è volontaria ed i tuoi dati saranno raccolti in conformità all'informativa " +
                "sul trattamento dei dati già accettata al momento del download del plugin. Desideri visionarla sul nostro sito web?", 
                AppName, 
                MessageBoxButtons.YesNoCancel, 
                MessageBoxIcon.Information);
            if (dialogResult == DialogResult.Cancel)
            {
                StopAddIn();
                return;
            } else if (dialogResult == DialogResult.Yes)
            {
                dispatcher.Invoke(() => { Process.Start(ENDPOINT_BASE_URL + "/review"); });  // the /review route lets the user only review the informed consent
            }
            MessageBox.Show("Attendi che le email nella tua casella siano raccolte.\nIl programma potrebbe non rispondere per diversi secondi, ti chiediamo di pazientare.\n" +
                "Riceverai una notifica a breve.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
           
            var mailIDsToProcess = new List<(string, string, string)>();
            await dispatcher.InvokeAsync(() =>
                {
                Logger.Info("Collecting email folders...");
                var mailFolders = GetMailFolders();
                Logger.Info("Collecting emails from " + mailFolders.Count() + " folders...");
                mailIDsToProcess = GetEmailsToProcess(mailFolders, ExistingEmails);
                N_Mails_To_Process = mailIDsToProcess.Count();
                Logger.Info("Counted " + N_Mails_To_Process + " emails to process");
                }, DispatcherPriority.SystemIdle
            );

            if (N_Mails_To_Process > 0)
            {
                var showMessage = "Sono presenti " + N_Mails_To_Process + " mail da elaborare.\n" +
                "Il client di posta elettronica potrebbe subire rallentamenti per tutta la durata dell'elaborazione.\n" +
                "Il processo potrebbe durare diversi minuti, in base al numero di email e alla potenza di questo sistema.\n" +
                $"Si prega di non chiudere il client di posta durante l'operazione. Clicca su \"{TaskPaneControl.StateBtn.Label}\" per verificare l'avanzamento.\n" +
                "Iniziare il processo di esportazione?";
                dialogResult = MessageBox.Show(showMessage, AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (dialogResult == DialogResult.No)
                {
                    StopAddIn();
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
                MailProgress = 1;
                if (ExecuteInMultithread)
                {
                    int batchSize = Environment.ProcessorCount / 2;  // optimize parallel threads
                    batchSize = batchSize > 0 ? batchSize : 1;  // ensure the batch size is at least 1
                    for (int i = 0; i < mailIDsToProcess.Count; i += batchSize)
                    {
                        var mailBatch = new List<RawMail>(); // create the batch
                        for (int j = 0; j < batchSize; j++)
                        {
                            if (i + j >= mailIDsToProcess.Count)
                            {
                                break;
                            }
                            var mail = mailIDsToProcess[i+j];
                            string mailID = mail.Item1;
                            string folderID = mail.Item2;
                            string folderName = mail.Item3;

                            MailItem mailItem = MapiNs.GetItemFromID(mailID, folderID);
                            RawMail rm = ExtractRawDataFromMailItem(mailItem, folderName);
                            if (rm != null)
                            {
                                mailBatch.Add(rm);
                            }
                        }
                        // compute parallely the emails within the batch
                        await mailBatch.ParallelForEachAsync(async mail =>
                        {
                            cts.Token.ThrowIfCancellationRequested();
                            MailData data = new MailData(mail);

                            // Extract features
                            try
                            {
                                int completed = await Task.Run(() => data.ComputeFeatures()).
                                ContinueWith((prevTask) =>
                                {
                                    MailList.Add(data);
                                    SaveMail(data);
                                    SaveHashInDictionary(data.EmailHash, data.GetID());
                                    MailProgress++;
                                    Logger.Info($"Processed mail with ID: {data.GetID()}; {N_Mails_To_Process - MailProgress} remaining.");
                                    return MailProgress;
                                });
                            }
                            catch (System.Exception e)
                            {
                                Logger.Error("Problem processing mail with ID: " + data.GetID() + "\nError details: " + e.Message);
                            }
                            //}
                        }, maxDegreeOfParallelism: Environment.ProcessorCount / 2);
                    }
                }
                else  // Non-parallel computation
                {
                    foreach (var mail in mailIDsToProcess)
                    {
                        string mailID = mail.Item1;
                        string folderID = mail.Item2;
                        string folderName = mail.Item3;

                        MailItem mailItem = MapiNs.GetItemFromID(mailID, folderID);
                        RawMail m = ExtractRawDataFromMailItem(mailItem, folderName);
                        if (m  == null) { continue; }
                        dispatcher.Invoke(() =>
                        {
                            MailData data = new MailData(m);
                            try
                            {
                                //int completed = await Task.Run(() => data.ComputeFeatures()).ContinueWith((task) => { return 1; } );
                                data.ComputeFeatures();
                                MailList.Add(data);
                                SaveMail(data);
                                SaveHashInDictionary(data.EmailHash, data.GetID());
                                MailProgress++;
                                Logger.Info($"Processed mail with ID: {data.GetID()}; {N_Mails_To_Process - MailProgress} remaining.");
                            }
                            catch (System.Exception e)
                            {
                                Logger.Error("Problem processing mail with ID: " + data.GetID() + "\nError details: " + e.Message);
                            }

                        }, DispatcherPriority.SystemIdle);
                    }
                }
                // feature extraction process ended
                ExistingEmails = GetExistingEmails();  // retrieve all the computed emails 
                string[] EmailsToUpload = GetEmailsToUpload(ExistingEmails);  // check which of them must be uploaded
                N_Mails_To_Upload = EmailsToUpload.Length;
                RuntimeWatch.Stop();
                string msg = "";
                if (MailProgress > 1)  // 1 or more mails have been processed 
                {
                    msg = "Esportazione dei dati estratti dalle email completata!\n" +
                        $"Processate {MailProgress - 1} email in {Math.Round(RuntimeWatch.ElapsedMilliseconds / 1000f, 2)} secondi.\n";
                }
                if (N_Mails_To_Upload > 0)  // 1 or more mails must be trasmitted to the endpoint 
                {
                    msg += "I dati (ricavati da " + N_Mails_To_Upload + " email) saranno ora spediti ai nostri server per scopi di ricerca e trattati ai sensi della GDPR.\n" +
                    "I dati raccolti risultano da un processo di elaborazione delle email della casella di posta e sono completamente anonimi, " +
                    "in quanto non è possibile risalire al contenuto originale delle email o ai soggetti coinvolti.";
                    MessageBox.Show(msg, AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                } 
                else  // No mails to transmit -> the program can be closed
                {
                    MessageBox.Show("Tutti i dati sono già stati estratti e caricati sui nostri server. Grazie!\n" +
                        "È comunque possibile rilanciare la procedura dopo aver ricevuto nuove email.", AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    StopAddIn();
                    return;
                }

                /* Data trasmission to the end-point over HTTP(S) */
                Logger.Info("Starting the upload of "+ N_Mails_To_Upload + " files");
                try
                {
                    string[] successfullyUploadedMails;
                    UploadingFiles = true;
                    bool connectionOK = await FileUploader.TestConnection(ENDPOINT_TEST_URL);
                    if (connectionOK)
                    {
                        Logger.Info("Connection to remote server OK");

                        await dispatcher.InvokeAsync(async () =>
                        {
                            RuntimeWatch.Restart();
                            // MessageBox.Show("Upload dei dati iniziato.", AppName);
                            string file_ext = LIMIT_FILENAME_SPACE ? "" : ".json";
                            bool result = false;
                            (result, successfullyUploadedMails) = await FileUploader.UploadFiles(ENDPOINT_UPLOAD_URL, EmailsToUpload, cts, Environment.GetEnvironmentVariable("OUTPUT_FOLDER"), file_ext);
                            if (result)
                            {
                                sendLogs = MessageBox.Show($"Tutti i dati sono stati trasmessi con successo ({successfullyUploadedMails.Length} file caricati in {Math.Round(RuntimeWatch.ElapsedMilliseconds/1000f, 2)} s). Grazie!\nVuoi mandarci dati anonimi di diagnostica per migliorare il programma?",
                                    AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                            }
                            else if (successfullyUploadedMails.Length != 0 && successfullyUploadedMails.Length < N_Mails_To_Upload)  // some mails have been trasmitted
                            {
                                sendLogs = MessageBox.Show($"Problema nella trasmissione di {N_Mails_To_Upload - successfullyUploadedMails.Length } file su {N_Mails_To_Upload} totali ({successfullyUploadedMails.Length} file sono stati invece trasmessi correttamente)." +
                                    $"\nTi preghiamo di riprovare più tardi cliccando nuovamente su {TaskPaneControl.button1.Label}.\nVuoi mandarci dati di diagnostica anonimi per investigare le potenziali cause del problema?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                            }
                            else  // if no mail has been trasmitted successfully
                            {
                                sendLogs = MessageBox.Show($"Problema nella trasmissione dei dati. Ti preghiamo di riprovare più tardi cliccando nuovamente su {TaskPaneControl.button1.Label}.\nVuoi mandarci dati di diagnostica anonimi per investigare le potenziali cause del problema?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Error);

                            }
                            SaveUploadedEmails(successfullyUploadedMails);
                            StopAddIn();
                        }, DispatcherPriority.SystemIdle);
                    }
                    else
                    {
                        Logger.Error("Error connecting to remote server");
                        MessageBox.Show($"Server temporaneamente non raggiungibile. Ti preghiamo di riprovare più tardi cliccando nuovamente su {TaskPaneControl.button1.Label}.", AppName,
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        StopAddIn();
                        return;
                    }
                }
                catch (System.Exception e)
                {
                    Logger.Error("Error trasmitting data: " + e);
                    sendLogs = MessageBox.Show("Problema nella trasmissione dei dati delle email. Ti preghiamo di riprovare. Dettagli errore: " + e.Message + "\nTi chiediamo di mandarci dati di diagnostica anonimi per investigare l'errore. Accetti?", 
                        AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                    StopAddIn();
                }
            }
            catch (System.Exception e)
            {
                Logger.Error("Error in main: " + e);
                sendLogs = MessageBox.Show("Problema con l'esportazione dei dati. Dettagli errore: " + e.Message + "\nTi chiediamo di mandarci dati di diagnostica anonimi per investigare l'errore. Accetti?", 
                    AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                StopAddIn();
            }
            finally
            {
                cts.Dispose();

            }
            return;
        }

        private static MAPIFolder[] GetMailFolders()
        {
            // Get the mail folders list
            MAPIFolder defaultInbox = Globals.ThisAddIn.Application.Session.GetDefaultFolder(OlDefaultFolders.olFolderInbox);
            MAPIFolder defaultDeleted = Globals.ThisAddIn.Application.Session.GetDefaultFolder(OlDefaultFolders.olFolderDeletedItems);
            MAPIFolder defaultSpam = Globals.ThisAddIn.Application.Session.GetDefaultFolder(OlDefaultFolders.olFolderJunk);

            List<MAPIFolder> mailFolders = new List<MAPIFolder>
            {
                //defaultInbox,  
                //defaultDeleted,
                //defaultSpam 
            };  // add the "inbox", "deleted" and "spam" folders by default


            // Define recursive function
            void GetFolders(MAPIFolder folder)
            {
                var folder_name = folder.FullFolderPath.Split('\\').LastOrDefault();
                if (folder.Folders.Count <= 0)
                {
                    if (folder_name != "PersonMetadata" &&  // PernsoMetadata folder must be ignored (belongs to a deprecated functionality)
                       folder.EntryID != defaultInbox.EntryID &&  // ensure that we do not add the inbox, deleted, and spam folders twice
                       folder.EntryID != defaultDeleted.EntryID &&
                       folder.EntryID != defaultSpam.EntryID && 
                       !mailFolders.Contains(folder))
                    {
                        mailFolders.Add(folder);
                    }
                }
                else
                {
                    foreach (MAPIFolder subFolder in folder.Folders)
                    {
                        GetFolders(subFolder);
                    }
                }
            }

            foreach (MAPIFolder folder in Globals.ThisAddIn.Application.GetNamespace("MAPI").Folders)
            {
                if (folder.FullFolderPath == "\\\\francesco.greco1697@gmail.com")
                GetFolders(folder);
            }
            return mailFolders.ToArray();
        }


        /** 
         * Returns a list of tuples containing, for each email to process: the mail EntryID, the folder StoreID, and the folder name.
        **/
        private static List<(string, string, string)> GetEmailsToProcess(MAPIFolder[] mailFolders, string[] existingMails)
        {
            int k = 0;
            var mailIDsToProcess = new List<(string, string, string)>();

            try
            {
                string inbox_folder = Globals.ThisAddIn.Application.Session.GetDefaultFolder(OlDefaultFolders.olFolderInbox)?.EntryID;
                string deleted_folder = Globals.ThisAddIn.Application.Session.GetDefaultFolder(OlDefaultFolders.olFolderDeletedItems)?.EntryID;
                string junk_folder = Globals.ThisAddIn.Application.Session.GetDefaultFolder(OlDefaultFolders.olFolderJunk)?.EntryID;
                string sent_folder = Globals.ThisAddIn.Application.Session.GetDefaultFolder(OlDefaultFolders.olFolderSentMail)?.EntryID;

                foreach (MAPIFolder folder in mailFolders)
                {
                    var mails = folder.Items.OfType<MailItem>();
                    string folderCategory;
                    if (folder.EntryID == inbox_folder)
                    {
                        folderCategory = "inbox";
                    } else if (folder.EntryID == deleted_folder)
                    {
                        folderCategory = "deleted";
                    } else if (folder.EntryID == junk_folder)
                    {
                        folderCategory = "junk";
                    } else if (folder.EntryID == sent_folder)
                    {
                        folderCategory = "sent";
                    } else
                    {
                        folderCategory = "other";
                    }
                    int mails_in_folder_count = 0;
                    foreach (MailItem mail in mails)
                    {
                        try
                        {
                            string mail_ID = mail.EntryID;
                            if (LIMIT_FILENAME_SPACE)
                            {
                                mail_ID = mail_ID.TrimStart('0');
                            }
                            if (k >= EMAIL_LIMIT)  // Check that we have computed less emails than the limit
                            {
                                break;
                            }

                            if (mail.ReceivedTime >= DATE_LIMIT &&  // Check that the email is recent enough (e.g., of the last 10 years)
                               !existingMails.Contains(mail_ID))  // Checks that the mail has not already been computed previously
                            {
                                mailIDsToProcess.Add(
                                    (mail.EntryID,
                                    folder.StoreID,
                                    folderCategory)
                                );
                                k++;
                                mails_in_folder_count++;
                            }
                        } catch (System.Exception e)
                        {
                            Logger.Error("Error with an email item - " + e);
                            break;
                        }
                        
                    }
                    Logger.Info($"Found {mails_in_folder_count} mails in folder: {folderCategory} - Total mails: {k}" ); // folder.FullFolderPath retains sensitive information, like the email address
                    //mailItems.AddRange(from MailItem mail in folder.Items select (mail is MailItem ? mail : null, folder.Name));
                }
            } catch (System.Runtime.InteropServices.COMException e)
            {
                Logger.Error("Error gathering the emails to process: " + e);
            }
            
            return mailIDsToProcess;
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
            try
            {

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
                    folderName: folder_name,
                    datetime: mail.ReceivedTime
                    );
                return rawMail;
            } catch (System.Exception err)
            {
                Logger.Error($"Add-in GenericException: {err.Message}");
                return null;
            }
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
                if (LIMIT_FILENAME_SPACE)
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

        private static string[] GetEmailsToUpload(string[] existingEmails = null)
        {
            string[] uploadedEmails;
            if (existingEmails == null)
            {
                existingEmails = GetExistingEmails();
            }
            try
            {
                string uploadedFolder = Path.Combine(Environment.GetEnvironmentVariable("OUTPUT_FOLDER"), "up");
                if (!Directory.Exists(uploadedFolder))
                {
                    Directory.CreateDirectory(uploadedFolder);
                    return existingEmails;
                }
                if (LIMIT_FILENAME_SPACE)
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
                    if (!LIMIT_FILENAME_SPACE)  // if the files have the extension, add it
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
                //Debug.WriteLine($"SaveUploadedEmails: {ex.Message}");
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
                IncludeFields = true,
                NumberHandling = JsonNumberHandling.WriteAsString
            };
            foreach (MailData mail in mails)
            {
                string file_name;
                if (LIMIT_FILENAME_SPACE)
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
                    Logger.Error($"SaveMails() - PathTooLongException: trying to write on a path with {file_name.Length} chars ({file_name}).");
                }
            }
        }

        private static void SaveHashInDictionary (string hash, string mailID)
        {
            if (LIMIT_FILENAME_SPACE)
            {
                mailID = mailID.TrimStart('0');
            }
            HashesFromMailID[mailID] = hash;
        }

        public static void SetMultiThreadExecution(bool limit_execution)
        {
            ExecuteInMultithread = ! limit_execution;
        }

        public static Guid GetClientID()
        {
            string rootDir = Environment.GetEnvironmentVariable("ROOT_FOLDER");
            string clientIDFile = Path.Combine(rootDir, "CLIENT_ID");
            Guid clientID;
            try
            {
                if (File.Exists(clientIDFile)) // Retrieve the Client ID of the user
                {
                    string guid_txt = File.ReadAllText(clientIDFile);
                    clientID = new Guid(guid_txt);
                }
                else // This should happen only in the first run ever
                {
                    clientID = Guid.NewGuid();
                    File.WriteAllText(clientIDFile, clientID.ToString());
                }
            }
            catch (System.Exception e)
            {
                Logger.Error(e);
                clientID = Guid.NewGuid();
            }
            return clientID;
        }


        public static string GetAddinVersion()
        {
            if (ApplicationDeployment.IsNetworkDeployed)
            {
                ApplicationDeployment applicationDeployment = ApplicationDeployment.CurrentDeployment;

                Version version = applicationDeployment.CurrentVersion;

                return String.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
            }
            else return Assembly.GetEntryAssembly()?.GetName().Version.ToString() ?? "unknown";
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
                    string logFileLocation = Path.Combine(log_base_path, "logs", AppName+".log");

                    // Uncomment the lines below if you want to retain the base file name
                    // and change the folder name...
                    //FileInfo fileInfo = new FileInfo(fa.File);
                    //logFileLocation = string.Format(@"C:\MySpecialFolder\{0}", fileInfo.Name);
                    LogFilePath = logFileLocation;
                    fa.File = logFileLocation;
                    fa.ActivateOptions();
                    break;
                }
            }
            Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        }

        private static void StopAddIn()
        {
            if (sendLogs == DialogResult.Yes)
            {
                SendLogs();
            }
            InExecution = false;  
            UploadingFiles = false;
            //TaskPaneControl.LaunchPluginBtn.Enabled = true;
            System.Windows.Forms.Application.DoEvents();
        }

        public static async void SendLogs()
        {
            UploadingFiles = true;
            //string path = Path.Combine(RootDir, "logs", "test.log");
            bool uplodaed = await FileUploader.SendLogs(LogFilePath, ENDPOINT_SENDLOGS_URL);
            if (uplodaed)
            {
                MessageBox.Show("I file di log sono stati inviati correttamente al server.");
            } else
            {
                MessageBox.Show($"Errore nella trasmissione dei file di log {LogFilePath}. Ti preghiamo di riprovare più tardi");
            }
            UploadingFiles = false;
        }


        public static async void SendResidualEmails()
        {
            MessageBox.Show("Recupero di eventuali email già processate in precedenza...", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);

            var ExistingEmails = GetExistingEmails();  // retrieve all the computed emails 
            string[] EmailsToUpload = GetEmailsToUpload(ExistingEmails);  // check which of them must be uploaded
            N_Mails_To_Upload = EmailsToUpload.Length;
            if (N_Mails_To_Upload == 0)
            {
                MessageBox.Show($"Non ci sono email già processate. Lanciare il processo cliccando il tasto {TaskPaneControl.LaunchPluginBtn.Label}.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var promptResult = MessageBox.Show($"Ci sono {N_Mails_To_Upload} email già processate che possono essere inviate al server. Inviarle ora?", AppName, MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (promptResult == DialogResult.Cancel)  // The user can press Cancel and not start the transmission
                return;

            // Otherwise, begin the transmission process
            InExecution = true;
            Logger.Info("Starting the upload of residual " + N_Mails_To_Upload + " files");

            var dispatcher = Dispatcher.CurrentDispatcher;
            var cts = new CancellationTokenSource();
            // This code is redundant, it should be put in a method and reused
            try
            {
                string[] successfullyUploadedMails;
                UploadingFiles = true;
                bool connectionOK = await FileUploader.TestConnection(ENDPOINT_TEST_URL);
                if (connectionOK)
                {
                    Logger.Info("Connection to remote server OK");

                    await dispatcher.InvokeAsync(async () =>
                    {
                        // MessageBox.Show("Upload dei dati iniziato.", AppName);
                        string file_ext = LIMIT_FILENAME_SPACE ? "" : ".json";
                        bool result = false;
                        (result, successfullyUploadedMails) = await FileUploader.UploadFiles(ENDPOINT_UPLOAD_URL, EmailsToUpload, cts, Environment.GetEnvironmentVariable("OUTPUT_FOLDER"), file_ext);
                        if (result)
                        {
                            sendLogs = MessageBox.Show($"Tutti i dati sono stati trasmessi con successo ({successfullyUploadedMails.Length} file). Grazie!\nVuoi mandarci dati anonimi di diagnostica per migliorare il programma?",
                                AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        }
                        else if (successfullyUploadedMails.Length != 0 && successfullyUploadedMails.Length < N_Mails_To_Upload)  // some mails have been trasmitted
                        {
                            sendLogs = MessageBox.Show($"Problema nella trasmissione di {N_Mails_To_Upload - successfullyUploadedMails.Length} file su {N_Mails_To_Upload} totali ({successfullyUploadedMails.Length} file sono stati trasmessi correttamente)." +
                                $"\nTi preghiamo di riprovare più tardi cliccando nuovamente su {TaskPaneControl.button1.Label}.\nVuoi mandarci dati di diagnostica anonimi per investigare le potenziali cause del problema?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        }
                        else  // if no mail has been trasmitted successfully
                        {
                            sendLogs = MessageBox.Show($"Problema nella trasmissione dei dati. Ti preghiamo di riprovare più tardi cliccando nuovamente su {TaskPaneControl.button1.Label}. \nVuoi mandarci dati di diagnostica anonimi per investigare le potenziali cause del problema?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Error);

                        }
                        SaveUploadedEmails(successfullyUploadedMails);
                        StopAddIn();
                    }, DispatcherPriority.SystemIdle);
                }
                else
                {
                    Logger.Error("Error connecting to remote server");
                    MessageBox.Show($"Server temporaneamente non raggiungibile. Ti preghiamo di riprovare più tardi cliccando nuovamente su {TaskPaneControl.button1.Label}.", AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    StopAddIn();
                    return;
                }
            }
            catch (System.Exception e)
            {
                Logger.Error("Error trasmitting data: " + e);
                sendLogs = MessageBox.Show("Problema nella trasmissione dei dati delle email. Ti preghiamo di riprovare. Dettagli errore: " + e.Message + "\nTi chiediamo di mandarci dati di diagnostica anonimi per investigare l'errore. Accetti?",
                    AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                StopAddIn();
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
