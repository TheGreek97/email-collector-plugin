using Dasync.Collections;
using PhishingDataCollector;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;

public static class FileUploader
{
    private static HttpClient _httpClient = ThisAddIn.HTTPCLIENT;
    private static string _secretKey = Environment.GetEnvironmentVariable("SECRETKEY_MAIL_COLLECTOR");
    private static readonly int TIMEOUT = 300;  // seconds

    public static async Task<(bool, string[])> UploadFiles(string url, string[] fileNames, CancellationTokenSource cts, string folderName = ".\\", string fileExt = "")
    {
        //_httpClient = _httpClient ?? new HttpClient();
        _httpClient.CancelPendingRequests();
        
        Guid boundary = Guid.NewGuid();  // Generate a GUID for the boundary of the multipart/form-data request
        
        // Here we store the emails that have been successfully uploaded
        List<string> uploaded_mails = new List<string>();

        // Split the email list in multiple requests of N mails (e.g., 20)
        List<string[]> chunks = new List<string[]>();
        int chunkSize = 20;  // 20 is the default value for max_file_uploads in Apache (editable in php.ini)
        for (int i = 0; i < fileNames.Length; i += chunkSize)
        {
            int chunkLength = Math.Min(chunkSize, fileNames.Length - i);
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
            _httpClient.Timeout = TimeSpan.FromSeconds(TIMEOUT);

            var tasks = new List<Task<bool>>();
            foreach (string[] filesChunkPaths in chunks)  // each chunk contains multiple email files to be sent together
            {
                tasks.Add(SendFileAsync(_httpClient, filesChunkPaths, url, folderName, fileExt, uploaded_mails, boundary));
            }

            var tasksResult = await Task.WhenAll(tasks);
            foreach (bool result in tasksResult)
            {
                errors = errors && result;  // get if even one result is an error (=false)
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
                var fileContent = new StreamContent(File.OpenRead(filePath));
                formData.Add(fileContent, fileName, Path.GetFileName(filePath));
            }
            try
            {
                var response = await _httpClient.PostAsync(url, formData);
                //bag.Add(response);
                ThisAddIn.Logger.Info("Response status code: " + response.StatusCode);
                if (response.IsSuccessStatusCode)
                {
                    uploaded_mails.AddRange(filesToSendPath);  // add the email that have been uploaded correctly
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
            return errors;
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
}
