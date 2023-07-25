using Dasync.Collections;
using PhishingDataCollector;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

public static class FileUploader
{
    private static HttpClient _httpClient = ThisAddIn.HTTPCLIENT;
    private static string _secretKey = Environment.GetEnvironmentVariable("SECRETKEY_MAIL_COLLECTOR");
    private static readonly int TIMEOUT = 2000;

    public static async Task<bool> UploadFiles(string url, string[] fileNames, CancellationTokenSource cts, string folderName = ".\\", string fileExt = "")
    {
        //_httpClient = _httpClient ?? new HttpClient();
        _httpClient.CancelPendingRequests();

        Guid g = Guid.NewGuid();  // Generate a GUID for the boundary of the multipart/form-data request

        // Split the email list in multiple requests of N mails (e.g., 20)
        List<string[]> chunks = new List<string[]>();
        int chunkSize = 20;  // 20 is the default value for max_file_uploads in Apache (editable in php.ini)
        int testSize = fileNames.Length;  // TEST ONLY: sends only N mails to the endpoint
        for (int i = 0; i < testSize; i += chunkSize)
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
            _httpClient.Timeout = TimeSpan.FromSeconds(10);

            var bag = new ConcurrentBag<object>();
            await chunks.ParallelForEachAsync(async mailChunk =>
            {
                CancellationTokenSource timeoutSource = new CancellationTokenSource(TIMEOUT);

                // Build the request body
                using (var formData = new MultipartFormDataContent("----=NextPart_" + g))
                {
                    foreach (string fileName in mailChunk)
                    {
                        string filePath = Path.Combine(folderName, fileName + fileExt);
                        var fileContent = new StreamContent(File.OpenRead(filePath));
                        formData.Add(fileContent, fileName, Path.GetFileName(filePath));
                    }
                    try
                    {
                        var response = await _httpClient.PostAsync(url, formData, timeoutSource.Token);
                        bag.Add(response);
                        Debug.WriteLine(response.StatusCode);
                        if (!response.IsSuccessStatusCode)
                        {
                            errors = true;
                        }
                    }
                    catch
                    {
                        errors = true;
                        cts.Cancel();
                        return;
                    }
                }
            }, maxDegreeOfParallelism: 10);
            //var count = bag.Count;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error while uploading the files ");
            Debug.WriteLine(ex);
            _httpClient.Dispose();
            _httpClient = new HttpClient();
            cts.Cancel();
            errors = true;
        }
        return !errors;
    }

    public static async Task<bool> TestConnection(string url)
    {
        // Test the connection to the server
        bool result;
        try
        {
            var response = await _httpClient.GetAsync(url);
            result = response.IsSuccessStatusCode;
        }
        catch
        {
            result = false;
        }
        finally
        {
            // TODO: this is bad practice, but avoids "System.InvalidOperationException: This instance has already started one or more requests. Properties can only be modified before sending the first request."
            _httpClient.Dispose();
            _httpClient = new HttpClient();
        }
        return result;
    }
}
