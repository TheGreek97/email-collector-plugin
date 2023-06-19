using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Headers;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Net;
using System.IO;
using System.Runtime.InteropServices.ComTypes;


public class BlacklistURL_API {

    private const string _api_key = "key_LS3loGrqCWUvKHRaCJgd9LiSR";
    private const string _api_request_url = "https://api.blacklistchecker.com/";
    private string _ip_addr_request;

    private short _n_blacklists_detected { get; set; }


    public BlacklistURL_API(string url) {
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentNullException ("The URL cannot be empty or null!");
        } 
        else
        {
            _ip_addr_request = url;
        }
    }

    public async void PerformAPICall()
    {
        //perform API call to discover the origin (https://www.bigdatacloud.com/docs/ip-geolocation)
        string requestURL = _api_request_url + "check/" + _ip_addr_request;
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
                    _n_blacklists_detected = (short)val;
                } 
                else {  SetToUnknown();  }
            }
            else {  SetToUnknown(); }
            response.Close();
        }
        catch (Exception ex) // when (ex is JsonException || ex is KeyNotFoundException)
        {
            Debug.WriteLine(ex);
        }
    }

    public short GetFeature ()
    {
        return _n_blacklists_detected;
    }

    private void SetToUnknown()
    {
        _n_blacklists_detected = 0;
    }
}