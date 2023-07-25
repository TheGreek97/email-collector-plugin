using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PhishingDataCollector.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace PhishingDataCollector
{
    public class WhoIS : URLObject
    {
        public DateTime DomainCreationDate { set; get; }
        public DateTime DomainExpirationDate { set; get; }
        public string DomainName { set; get; }
        public string Registrar { set; get; }
        public JArray NameServers { set; get; }
        public X509Certificate2 Cert { set; get; }

        public WhoIS(string server) : base(server)
        {
            SetToUnknown();
        }
        public WhoIS(string server, DateTime creation_date, DateTime expiration_date) : base(server)
        {
            SetToUnknown();
            DomainCreationDate = creation_date;
            DomainExpirationDate = expiration_date;
        }
        public void SetToUnknown()
        {
            DomainName = string.IsNullOrEmpty(DomainName) ? "" : DomainName;
            Registrar = string.IsNullOrEmpty(Registrar) ? "" : Registrar; ;
            DomainCreationDate = DomainCreationDate == null ? TimeStamp.Origin : DomainCreationDate;
            DomainExpirationDate = DomainExpirationDate == null ? TimeStamp.Origin : DomainExpirationDate; ;
            NameServers = NameServers == null ? new JArray() : NameServers;
        }
        public double GetFeatureCreationDate()
        {
            return TimeStamp.ConvertToUnixTimestamp(DomainCreationDate);
        }
        public double GetFeatureExpirationDate()
        {
            return TimeStamp.ConvertToUnixTimestamp(DomainExpirationDate);
        }
        public double GetFeatureDomainRegLength()  // domain_reg_length
        {
            return DomainCreationDate.Subtract(DomainExpirationDate).TotalMilliseconds;
        }
        public bool GetFeatureAbnormalURL()  // abnormal_URL
        {
            string claimedIdentity = Regex.Match(DomainName, @"(\w*)\.\w*$")?.Groups[1].Value;
            return claimedIdentity.Contains(Registrar);
        }
        public byte GetFeatureNumNameServers()  // n_name_servers
        {
            if (NameServers == null)
            {
                return 0;
            }
            byte? n = (byte)NameServers.Count;
            if (n == null) { n = 0; }
            return (byte)n;
        }
        public bool GetFeatureSelfSignedHTTPS()
        {
            if (Cert != null)
            {
                return Cert.SubjectName.RawData.SequenceEqual(Cert.IssuerName.RawData);
            }
            else
            {
                return false;
            }
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

        private static readonly string datetime_format = "yyyy-MM-dd HH:mm:ss";
        private static readonly CultureInfo provider = CultureInfo.InvariantCulture;

        public static void PerformAPICall(WhoIS domain)
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
                using (HttpWebResponse response = (HttpWebResponse)httpRequest.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        domain.Cert = new X509Certificate2(httpRequest.ServicePoint.Certificate);
                        Stream resultStream = response.GetResponseStream();
                        StreamReader reader = new StreamReader(resultStream);
                        string resultString = reader.ReadToEnd();
                        // Response structure: https://developers.virustotal.com/reference/url-object
                        JObject jsonObject = (JObject)((JObject)JsonConvert.DeserializeObject(resultString)).GetValue("result");

                        if (jsonObject != null)
                        {
                            domain.DomainName = (string)jsonObject.GetValue("domain_name") ?? null;
                            domain.Registrar = (string)jsonObject.GetValue("registrar") ?? null;
                            DateTime temp = new DateTime();
                            if (DateTime.TryParseExact((string)jsonObject.GetValue("creation_date"), datetime_format, provider, DateTimeStyles.None, out temp))
                            {
                                domain.DomainCreationDate = temp;
                            }
                            if (DateTime.TryParseExact((string)jsonObject.GetValue("expiration_date"), datetime_format, provider, DateTimeStyles.None, out temp))
                            {
                                domain.DomainExpirationDate = temp;
                            }
                            //domain.DomainCreationDate = DateTime.ParseExact((string)jsonObject.GetValue("creation_date"), datetime_format, provider);
                            try
                            {
                                domain.NameServers = new JArray(jsonObject.GetValue("name_servers"));
                            }
                            catch (System.InvalidCastException ex)
                            {
                                Debug.WriteLine("Invalid Cast exception!");
                                Debug.WriteLine(ex);
                                domain.NameServers = new JArray();
                            }
                        }
                        else { domain.SetToUnknown(); }
                    }
                    else { domain.SetToUnknown(); }
                    response.Close();
                }

            }
            catch (Exception ex) when (ex is JsonException || ex is KeyNotFoundException || ex is WebException)
            {
                Debug.WriteLine("WhoIS API exception:");
                Debug.WriteLine(ex);
                if (ex is WebException)
                {
                    Debug.WriteLine(requestURL);
                }
            }
        }
    }
}

