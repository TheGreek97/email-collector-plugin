using System;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;

public class VirusTotalScan : URLObject
{
    public short NMalicious { set; get; }
    public short NHarmless { set; get; }
    public bool IsUnkown { set;  get; }
    public bool IsAttachment { set; get; }
    public bool IsIPAddress { get; }
    public string Base64Address { get; set; }
    public VirusTotalScan(string server) : base(server) {
        IsIPAddress = Regex.IsMatch(server, "([\\d]{1,3}\\.){3}[\\d]{1,3}");
        GenerateBase64();
    }
    
    public VirusTotalScan(string server, short n_blacklist, bool isAttachment) : base(server)
    {
        IsIPAddress = Regex.IsMatch(server, "([\\d]{1,3}\\.){3}[\\d]{1,3}");
        IsUnkown = true;
        IsAttachment = isAttachment;
        GenerateBase64();
    }

    public void SetToUnknown()
    {
        IsUnkown = true;
        NHarmless = 0;
    }
    private void GenerateBase64 ()
    {
        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(Address);
        Base64Address = System.Convert.ToBase64String(plainTextBytes);
        Base64Address = Regex.Replace(Base64Address, @"=+$", "");  // remove trailing padding chars '='
    }
}

public class VirusTotalScansCollection : URLsCollection
{
    public void CopyTo(VirusTotalScan[] array, int arrayIndex)
    {
        if (array == null)
            throw new ArgumentNullException("The array cannot be null.");
        if (arrayIndex < 0)
            throw new ArgumentOutOfRangeException("The starting array index cannot be negative.");
        if (Count > array.Length - arrayIndex)
            throw new ArgumentException("The destination array has fewer elements than the collection.");

        for (int i = 0; i < innerCol.Count; i++)
        {
            array[i + arrayIndex] = (VirusTotalScan)innerCol[i];
        }
    }
}


public static class VirusTotal_API {

    private const string _api_key = "02c720cb6e04487edb6384c18bb663da3667ca6e8f79925682d82e700f5ddae9";
    private const string _api_request_url = "https://www.virustotal.com/api/v3/";

    public static void PerformAPICall(VirusTotalScan vt)
    {
        string requestURL;
        HttpWebRequest httpRequest;
        if (vt.IsAttachment) {
            requestURL = _api_request_url + "files/"+vt.Address;  // Address represents the MD5 checksum of the attachment 
        } else {
            if (vt.IsIPAddress) {
                requestURL = _api_request_url + "ip_addresses/"+vt.Address;
            } else {
                requestURL = _api_request_url + "urls/" + vt.Base64Address;
            }
        }
        httpRequest = (HttpWebRequest)WebRequest.Create(requestURL);
        httpRequest.Headers.Add("x-apikey", _api_key);
        try
        {
            HttpWebResponse response = (HttpWebResponse)httpRequest.GetResponse();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Stream resultStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(resultStream);
                string resultString = reader.ReadToEnd();
                // Response structure: https://developers.virustotal.com/reference/url-object
                JObject jsonObject = (JObject) ((JObject) ((JObject)JsonConvert.DeserializeObject(resultString)).GetValue("data")).GetValue("attributes");
                JObject scanners_results = (JObject) jsonObject.GetValue("last_analysis_results");
                JObject total_votes = (JObject) jsonObject.GetValue("total_votes");
                if (total_votes != null)
                {
                    vt.NHarmless = (short) total_votes.GetValue("harmless");  // Positive votes
                    vt.NMalicious = (short) total_votes.GetValue("malicious");  // Negative votes
                } 
                else { vt.SetToUnknown(); }
            }
            else { vt.SetToUnknown(); }
            response.Close();
        }
        catch (Exception ex)  // when (ex is JsonException || ex is KeyNotFoundException)
        {
            Debug.WriteLine(ex);
        }
    }
}
