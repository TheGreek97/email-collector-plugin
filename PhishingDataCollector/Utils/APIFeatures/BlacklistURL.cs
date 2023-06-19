﻿using System;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.IO;

public class BlacklistURL : URLObject
{
    public short NBlacklists { set; get; }
    public short NBlacklistsDetected { get; set; }

    public BlacklistURL(string server) : base(server) { }
    public BlacklistURL(string server, short n_blacklist) : base(server)
    {
        NBlacklists = n_blacklist;
    }
    public short GetFeature()
    {
        return NBlacklistsDetected;
    }

    public void SetToUnknown()
    {
        NBlacklistsDetected = 0;
    }
}

public class BlacklistURLsCollection : URLsCollection
{
    public void CopyTo(BlacklistURL[] array, int arrayIndex)
    {
        if (array == null)
            throw new ArgumentNullException("The array cannot be null.");
        if (arrayIndex < 0)
            throw new ArgumentOutOfRangeException("The starting array index cannot be negative.");
        if (Count > array.Length - arrayIndex)
            throw new ArgumentException("The destination array has fewer elements than the collection.");

        for (int i = 0; i < innerCol.Count; i++)
        {
            array[i + arrayIndex] = (BlacklistURL)innerCol[i];
        }
    }
}


public static class BlacklistURL_API {

    private const string _api_key = "key_LS3loGrqCWUvKHRaCJgd9LiSR";
    private const string _api_request_url = "https://api.blacklistchecker.com/";


    public static async void PerformAPICall(BlacklistURL bl)
    {
        string requestURL = _api_request_url + "check/" + bl.NBlacklistsDetected;
        HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(requestURL);
        httpRequest.Headers.Add("Authorization", "Basic username" + _api_key);
        try {
            HttpWebResponse response = (HttpWebResponse)httpRequest.GetResponse();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Stream resultStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(resultStream);
                string resultString = reader.ReadToEnd();

                JObject jsonObject = (JObject)JsonConvert.DeserializeObject(resultString);
                //JObject blacklists = (JObject)jsonObject.GetValue("blacklists")
                JToken val = jsonObject.GetValue("detections");
                if (val != null)
                {
                    bl.NBlacklistsDetected = (short)val;
                } 
                else {  bl.SetToUnknown();  }
            }
            else {  bl.SetToUnknown(); }
            response.Close();
        }
        catch (Exception ex) // when (ex is JsonException || ex is KeyNotFoundException)
        {
            Debug.WriteLine(ex);
        }
    }

}