using PhishingDataCollector;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

public static class FileUploader
{
    private static HttpClient _httpClient = ThisAddIn.HTTPCLIENT;
    private static string _secretKey = Environment.GetEnvironmentVariable("SECRETKEY_MAIL_COLLECTOR");

    public static async Task<bool> UploadFiles(string url, string[] fileNames, string folderName=".\\", string fileExt = ".json")
    {
        if (_httpClient == null)
        {
            _httpClient = new HttpClient();
        }
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);  // Add the authentication token
        
        Guid g = Guid.NewGuid();  // Generate a GUID for the boundary of the multipart/form-data request

        // Split the email list in multiple requests of N mails (e.g., 20)
        List<string[]> chunks = new List<string[]>();
        int chunkSize = 20;  // 20 is the default value for max_file_uploads in Apache (editable in php.ini)
        int testSize = fileNames.Length;
        for (int i = 0; i < testSize; i += chunkSize)  
        {
            int chunkLength = Math.Min(chunkSize, fileNames.Length - i);
            string[] chunk = new string[chunkLength];
            Array.Copy(fileNames, i, chunk, 0, chunkLength);
            chunks.Add(chunk);
        }
        bool errors = false;
        var cts = new CancellationTokenSource();
        var po = new ParallelOptions
        {
            CancellationToken = cts.Token,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };
        Parallel.ForEach(chunks, po, async (chunk) =>
        {
            using (var formData = new MultipartFormDataContent("----=NextPart_" + g))
            {
                try
                {
                    foreach (string fileName in chunk)
                    {
                        string filePath = Path.Combine(folderName, fileName + fileExt);
                        var fileContent = new StreamContent(File.OpenRead(filePath));
                        formData.Add(fileContent, fileName, Path.GetFileName(filePath));
                    }
                    //formData.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary="+g);

                    var response = await _httpClient.PostAsync(url, formData);
                    Debug.WriteLine(response.StatusCode);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error while uploading the file");
                    Debug.WriteLine("From mail " + chunk[0] + " To mail " + chunk[chunk.Length - 1]);
                    Debug.WriteLine(ex);
                    errors = true;
                }
            }
        });
        Task.WaitAll();
        return !errors;  // returns true if there was no error
    }
}
