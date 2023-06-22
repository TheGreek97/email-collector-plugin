using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

namespace PhishingDataCollector
{
    public class OriginIP : URLObject
    {
        public string Origin { set; get; }
        public string CountryName { get; set; }
        public string RegionName { get; set; }
        public OriginIP(string server) : base(server)
        {
            SetToUnknown();
        }
        public OriginIP(string server, string regionName, string countryName) : base(server)
        {
            CountryName = countryName;
            RegionName = regionName;
            if (RegionName == "Italy" || RegionName == "Russia") { Origin = CountryName; }
            else if (RegionName == "" && CountryName == "") { Origin = "unknown"; }
            else { Origin = RegionName; }
        }
        public string GetFeature()
        {
            return Origin;
        }
        public void SetToUnknown()
        {
            CountryName = string.Empty;
            RegionName = string.Empty;
            Origin = "unknown";
        }
    }

    public class OriginIPCollection : URLsCollection
    {
        // Searches for the IP address in the collection and returns true if found, false otherwise
        // If the checkVal flag is set to true, the function returns true only if
        // the IP is found and the origin has a value different from empty
        public bool Contains(OriginIP item, bool checkVal = false)
        {
            foreach (OriginIP ip_obj in innerCol)
            {
                if (ip_obj.Address == item.Address)
                {
                    if (checkVal)
                    {
                        if (!string.IsNullOrEmpty(item.Origin))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // Adds an item if it is not already in the collection
        // as determined by calling the Contains method.
        public void Add(OriginIP item)
        {

            if (!Contains(item))
            {
                innerCol.Add(item);
            }
            else
            {
                Console.WriteLine("The IP {0} was already added to the collection (origin = {1}).",
                    item.Address, item.Origin);
            }
        }

        public void CopyTo(OriginIP[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException("The array cannot be null.");
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException("The starting array index cannot be negative.");
            if (Count > array.Length - arrayIndex)
                throw new ArgumentException("The destination array has fewer elements than the collection.");

            for (int i = 0; i < innerCol.Count; i++)
            {
                array[i + arrayIndex] = (OriginIP)innerCol[i];
            }
        }
    }

    public static class OriginIP_API
    {

        private static readonly string _api_key = Environment.GetEnvironmentVariable("APIKEY__BIGDATACLOUD");
        private const string _api_request_url = "https://api.bigdatacloud.net/data/country-by-ip";
        public static async void PerformAPICall(OriginIP originIP)
        {
            var queryParameters = new Dictionary<string, string>()
            {
                ["ip"] = originIP.Address,
                ["key"] = _api_key
            };
            var api_url = QueryHelpers.AddQueryString(_api_request_url, queryParameters);

            ThisAddIn.HTTPCLIENT.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                using (var response = ThisAddIn.HTTPCLIENT.GetAsync(api_url).Result)
                {
                    if (response.IsSuccessStatusCode)
                    {
                        string resultString = response.Content.ReadAsStringAsync().Result;

                        JObject jsonObject = (JObject)JsonConvert.DeserializeObject(resultString);
                        JObject country = (JObject)jsonObject.GetValue("country");
                        if (country != null)
                        {
                            originIP.CountryName = (string)country.GetValue("name");
                            originIP.RegionName = (string)((JObject)country.GetValue("wbRegion")).GetValue("value");
                        }
                        else { originIP.SetToUnknown(); }
                    }
                    else { originIP.SetToUnknown(); }
                }
                    
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }
}

