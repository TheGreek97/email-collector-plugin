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

using PhishingDataCollector;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

public static class FileUploader
{
    private static HttpClient _httpClient = ThisAddIn.HTTPCLIENT;
    private static string _secretKey = Environment.GetEnvironmentVariable("SECRETKEY_MAIL_COLLECTOR");
    private static readonly int TIMEOUT = 100;  // timeout in seconds per request
    private static readonly int EMAIL_BATCH_SIZE_UPLOAD = 100;  // 20 is the default value for max_file_uploads in Apache (editable in php.ini)

    private static int _numSentEmail = 0;
    public static int NSentEmail
    {
        get => _numSentEmail;
    }
    
    public static async Task<(bool, string[])> UploadFiles(string url, string[] fileNames, CancellationTokenSource cts, string folderName = ".\\", string fileExt = "")
    {
        //_httpClient = _httpClient ?? new HttpClient();
        _httpClient.CancelPendingRequests();
        
        Guid boundary = Guid.NewGuid();  // Generate a GUID for the boundary of the multipart/form-data request
        
        // Here we store the emails that have been successfully uploaded
        List<string> uploaded_mails = new List<string>();

        // Split the email list in multiple requests of N mails (e.g., 20)
        List<string[]> chunks = new List<string[]>();
        for (int i = 0; i < fileNames.Length; i += EMAIL_BATCH_SIZE_UPLOAD)
        {
            int chunkLength = Math.Min(EMAIL_BATCH_SIZE_UPLOAD, fileNames.Length - i);
            string[] chunk = new string[chunkLength];
            Array.Copy(fileNames, i, chunk, 0, chunkLength);
            chunks.Add(chunk);
        }
        var po = new ParallelOptions
        {
            CancellationToken = cts.Token,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };
        bool errors = false;
        try
        {
            // Add Headers for HTTP requests
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);  // Add the authorization token
            _httpClient.DefaultRequestHeaders.Add("ClientID", ThisAddIn.GetClientID().ToString());
            _httpClient.DefaultRequestHeaders.Add("AddinVersion", ThisAddIn.GetAddinVersion().ToString());
            _httpClient.Timeout = TimeSpan.FromSeconds(TIMEOUT);

            var tasks = new List<Task<bool>>();
            int counter = 0;
            _numSentEmail = 0;
            foreach (string[] filesChunkPaths in chunks)  // each chunk contains multiple email files to be sent together
            {
                /*string filenames_log = "";
                for(int i=0; i <filesChunkPaths.Length; i++) { 
                    filenames_log += filesChunkPaths[i];
                    if (i < filesChunkPaths.Length - 1) { filenames_log += ", "; }
                }*/
                //ThisAddIn.Logger.Info("Uploading files: "+ filenames_log);
                ThisAddIn.Logger.Info("Uploading batch containing files up to " + (filesChunkPaths.Length + (counter * EMAIL_BATCH_SIZE_UPLOAD)) + "/" + fileNames.Length + " files");
                tasks.Add(SendFileAsync(_httpClient, filesChunkPaths, url, folderName, fileExt, uploaded_mails, boundary));
                counter++;
            }
            var tasksResult = await Task.WhenAll(tasks);
            for(int i=0; i < tasksResult.Length; i++) 
            {
                // If result == true -> OK, result == false -> not OK
                if (!tasksResult[i]) { 
                    errors = true;
                    ThisAddIn.Logger.Error("Error uploading file chunk #" + i);
                }
            }
            /*
            var bag = new ConcurrentBag<object>();
            CancellationTokenSource timeoutSource = new CancellationTokenSource(TIMEOUT);
            await chunks.ParallelForEachAsync(async mailChunk =>
            {
                
            }, maxDegreeOfParallelism: Environment.ProcessorCount / 2);*/
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            ThisAddIn.Logger.Error("Error while uploading the files - " + ex);
            errors = true;
        }
        finally
        {
            // refresh _httpClient
            try
            {
                _httpClient?.Dispose();
            } // catch (ObjectDisposedException)
            finally
            {
                _httpClient = new HttpClient();
            }
        }
        return (!errors, uploaded_mails.ToArray());
    }

    static async Task<bool> SendFileAsync(HttpClient client, string [] filesToSendPath, string url, string folderName, string fileExt, List<string> uploaded_mails, Guid boundary)
    {
        bool errors = false;
        // Build the request body
        using (var formData = new MultipartFormDataContent("----=NextPart_" + boundary))
        {
            foreach (string fileName in filesToSendPath)
            {
                string filePath = Path.Combine(folderName, fileName + fileExt);
                // Use Hash instead of MAILID
                string file_hash;
                try {
                    using (StreamReader r = new StreamReader(filePath))
                    {
                        string json = r.ReadToEnd();
                        file_hash = System.Text.RegularExpressions.Regex.Match(json, @"""EmailHash""\s*:\s*""(.*)""").Groups[1].Value;
                    }
                } catch (Exception ex)
                {
                    file_hash = fileName;
                    Debug.WriteLine(ex);
                }
                var fileContent = new StreamContent(File.OpenRead(filePath));
                formData.Add(fileContent, file_hash, Path.GetFileName(filePath));
            }
            try
            {
                var response = await _httpClient.PostAsync(url, formData);
                //bag.Add(response);
                ThisAddIn.Logger.Info("Response status code: " + response.StatusCode);
                ThisAddIn.Logger.Info("Response message: " + response.Content.ReadAsStringAsync());
                if (response.IsSuccessStatusCode)
                {
                    uploaded_mails.AddRange(filesToSendPath);  // add the email that have been uploaded correctly
                    _numSentEmail += filesToSendPath.Length;
                }
                else
                {
                    errors = true;
                }
            }
            catch (Exception e)
            {
                ThisAddIn.Logger.Error("Error uploading the data to remote server - " + e);
                errors = true;
            }
            return !errors;
        }
    }

        public static async Task<bool> TestConnection(string url)
    {
        // Test the connection to the remote server
        bool result;
        try
        {
            var response = await _httpClient.GetAsync(url);
            result = response.IsSuccessStatusCode;
        }
        catch (Exception e) 
        { 
            ThisAddIn.Logger.Error(e.Message);
            result = false;
        }
        finally
        {
            // FIXME: this is bad practice, but avoids "System.InvalidOperationException: This instance has already started one or more requests. Properties can only be modified before sending the first request."
            _httpClient.Dispose();
            _httpClient = new HttpClient();
        }
        return result;
    }

    public static async Task<bool> SendLogs(string logFilePath, string endPointUrl)
    {
        try
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("ClientID", ThisAddIn.GetClientID().ToString());
                client.DefaultRequestHeaders.Add("AddinVersion", ThisAddIn.GetAddinVersion().ToString());

                var formData = new MultipartFormDataContent();

                var fileStream = new FileStream(logFilePath, FileMode.Open);
                var fileContent = new StreamContent(fileStream);

                formData.Add(fileContent, "file", "file.log");

                var response = await client.PostAsync(endPointUrl, formData);

                if (!response.IsSuccessStatusCode)
                {
                    ThisAddIn.Logger.Error("Could not send this log file to remote server at " + endPointUrl + " - Code " + response.StatusCode);
                    return false;
                }
                else
                {
                    ThisAddIn.Logger.Info("Sent this log file to remote server at " + endPointUrl);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            ThisAddIn.Logger.Error("Exception thrown while sending this log file to remote server: " + ex.Message);
            return false;
        }
    }
}
