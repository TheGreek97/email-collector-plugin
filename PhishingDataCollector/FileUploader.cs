using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

public class FileUploader
{
    private readonly HttpClient httpClient;

    public FileUploader()
    {
        httpClient = new HttpClient();
    }

    public async Task UploadFiles(string url, string[] fileNames, string folderName=".\\", string fileExt = ".json")
    {
        using (var formData = new MultipartFormDataContent())
        {
            foreach (string fileName in fileNames)
            {
                string filePath = Path.Combine(folderName, fileName + fileExt);
                var fileContent = new StreamContent(File.OpenRead(filePath));
                formData.Add(fileContent, "files", Path.GetFileName(filePath));
            }
            var response = await httpClient.PostAsync(url, formData);
            formData.Headers.Add("Authorization", "API_SECRET_KEY");
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Files uploaded successfully.");
            }
            else
            {
                Console.WriteLine("Failed to upload files.");
            }
        }
    }
}
