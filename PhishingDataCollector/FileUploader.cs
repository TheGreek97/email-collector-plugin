using PhishingDataCollector;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Linq;
using System.Collections.Generic;

public static class FileUploader
{
    private static HttpClient _httpClient = ThisAddIn.HTTPCLIENT;
    private static string _secretKey = Environment.GetEnvironmentVariable("SECRETKEY_MAIL_COLLECTOR");

    public static async Task UploadFiles(string url, string[] fileNames, string folderName=".\\", string fileExt = ".json")
    {
        if (_httpClient == null)
        {
            _httpClient = new HttpClient();
        }
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);  // Add the authentication token
        
        Guid g = Guid.NewGuid();  // Generate a GUID for the boundary of the multipart/form-data request

        // Split the email list in multiple requests of N mails (e.g., 500)
        List<string[]> chunks = new List<string[]>();
        int chunkSize = 500;
        for (int i = 0; i < fileNames.Length; i += chunkSize)
        {
            int chunkLength = Math.Min(chunkSize, fileNames.Length - i);
            string[] chunk = new string[chunkLength];
            Array.Copy(fileNames, i, chunk, 0, chunkLength);
            chunks.Add(chunk);
        }

        foreach (var chunk in chunks)
        {
            using (var formData = new MultipartFormDataContent("----=NextPart_" + g))
            {
                try
                {
                    foreach (string fileName in fileNames.Take(3))
                    {
                        string filePath = Path.Combine(folderName, fileName + fileExt);
                        var fileContent = new StreamContent(File.OpenRead(filePath));
                        formData.Add(fileContent, "files", Path.GetFileName(filePath));
                    }
                    //formData.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary="+g);

                    var response = await _httpClient.PostAsync(url, formData);
                    Debug.WriteLine(response.StatusCode);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error while uploading the file");
                    Debug.WriteLine("From mail " + chunk[0]+ " To mail " + chunk[chunk.Length-1]);
                    Debug.WriteLine(ex);
                }
            }
        }
    }
}
