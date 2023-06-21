using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.WebUtilities;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using PhishingDataCollector.Utils;
using System.Threading.Tasks;

namespace PhishingDataCollector
{
    public class WhoIS : URLObject
    {
        public DateTime DomainCreationDate { set; get; }
        public DateTime DomainExpirationDate { set; get; }
        public string DomainName { set; get; }
        public string Registrar { set; get; }
        public JObject NameServers { set; get; }

        public WhoIS (string server) : base(server)
        {
            SetToUnknown();
        }
        public WhoIS(string server, DateTime creation_date, DateTime expiration_date) : base(server)
        {
            DomainCreationDate = creation_date;
            DomainExpirationDate = expiration_date;
        }
        public void SetToUnknown()
        {
            DomainCreationDate = TimeStamp.Origin;
            DomainExpirationDate = TimeStamp.Origin;
        }
        public double GetFeatureCreationDate()  
        {
            return TimeStamp.ConvertToUnixTimestamp(DomainCreationDate);
        }
        public double GetFeatureExpirationDate()  
        {
            return TimeStamp.ConvertToUnixTimestamp(DomainExpirationDate);
        }
        public double GetFeatureDomainRegLength ()  // domain_reg_length
        {
            return DomainCreationDate.Subtract(DomainExpirationDate).TotalMilliseconds;
        }
        public bool GetFeatureAbnormalURL ()  // abnormal_URL
        {
            try
            {
                string claimedIdentity = Regex.Match(DomainName, @"(\w*)\.\w*$").Groups[1].Value;
                return claimedIdentity.Contains(Registrar);
            } catch (Exception)
            {
                return false;
            }
        }
        public byte GetFeatureNumNameServers()  // n_name_servers
        {
            if (NameServers == null)
            {
                return 0;
            }
            byte? n = (byte) NameServers.Count;
            if (n == null) { n = 0; }
            return (byte) n;
        }
    }

    public class WhoISCollection : URLsCollection
    {
        public void CopyTo(WhoIS[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException("The array cannot be null.");
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException("The starting array index cannot be negative.");
            if (Count > array.Length - arrayIndex)
                throw new ArgumentException("The destination array has fewer elements than the collection.");

            for (int i = 0; i < innerCol.Count; i++)
            {
                array[i + arrayIndex] = (WhoIS)innerCol[i];
            }
        }
    }

    public static class WhoIS_API
    {
        private static readonly string _api_key = Environment.GetEnvironmentVariable("APIKEY__WHOIS_API");
        private const string _api_request_url = "https://api.apilayer.com/whois/query";

        private static string datetime_format = "yyyy-MM-dd HH-mm-ss";
        private static CultureInfo provider = CultureInfo.InvariantCulture;

        public static async Task PerformAPICall(WhoIS domain)
        {
            var queryParameters = new Dictionary<string, string>()
            {
                ["domain"] = domain.Address.ToLower()
            };
            var requestURL = QueryHelpers.AddQueryString(_api_request_url, queryParameters);
            HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(requestURL);
            httpRequest.Headers.Add("apikey", _api_key);
            try
            {
                HttpWebResponse response = (HttpWebResponse) await httpRequest.GetResponseAsync();  //FIXME! vedi immagine messaggio
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream resultStream = response.GetResponseStream();
                    StreamReader reader = new StreamReader(resultStream);
                    string resultString = reader.ReadToEnd();
                    // Response structure: https://developers.virustotal.com/reference/url-object
                    JObject jsonObject = (JObject)((JObject)JsonConvert.DeserializeObject(resultString)).GetValue("result");
                    
                    if (jsonObject != null)
                    {
                        domain.DomainName = (string)jsonObject.GetValue("domain_name");
                        domain.Registrar = (string)jsonObject.GetValue("registrar");
                        domain.DomainCreationDate = DateTime.ParseExact ((string)jsonObject.GetValue("creation_date"), datetime_format, provider);
                        domain.DomainExpirationDate = DateTime.ParseExact((string)jsonObject.GetValue("expiration_date"), datetime_format, provider);
                        domain.NameServers = (JObject)jsonObject.GetValue("name_servers");
                    }
                    else { domain.SetToUnknown(); }
                }
                else { domain.SetToUnknown(); }
                response.Close();
            }
            catch (Exception ex)  // when (ex is JsonException || ex is KeyNotFoundException)
            {
                Debug.WriteLine(ex);
            }
        }
    }
}

