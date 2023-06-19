using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Headers;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace PhishingDataCollector
{

    public class OriginIP_API {

        private const string _api_key = "bdc_cd8f72a03c1843d59d402d7cdd1b0a6b";
        private const string _api_request_url = "https://api.bigdatacloud.net/data/country-by-ip";
        private string _ip_addr_request;

        private string country_name { get; set; }
        private string region_name { get; set; }


        public OriginIP_API(string origin_ip) {

            if (string.IsNullOrEmpty(origin_ip))
            {
                throw new ArgumentNullException("The IP address cannot be empty or null!");
            } 
            else
            {
                _ip_addr_request = origin_ip;
            }
        }

        public async void PerformAPICall()
        {
            //perform API call to discover the origin (https://www.bigdatacloud.com/docs/ip-geolocation)
            HttpClient httpClient = new HttpClient();

            var queryParameters = new Dictionary<string, string>()
            {
                ["ip"] = _ip_addr_request,
                ["key"] = _api_key
            };
            var api_url = QueryHelpers.AddQueryString(_api_request_url, queryParameters);

            httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            try {
                var response = httpClient.GetAsync(api_url).Result;
                if (response.IsSuccessStatusCode)
                {
                    string resultString = response.Content.ReadAsStringAsync().Result;
                
                    JObject jsonObject = (JObject) JsonConvert.DeserializeObject(resultString);
                    JObject country = (JObject)jsonObject.GetValue("country");
                    if (country != null)
                    {
                        country_name = (string)country.GetValue("name");
                        region_name = (string)((JObject)country.GetValue("wbRegion")).GetValue("value");
                    } else {  SetToUnknown();  }   
                } else {  SetToUnknown();  }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        public string GetFeature ()
        {
            if (region_name == "Italy" || region_name == "Russia") { return country_name; }
            else if (region_name == "" &&  country_name == "") { return "unknown"; } 
            else { return region_name; }
        }

        private void SetToUnknown()
        {
            country_name = string.Empty;
            region_name = string.Empty;
        }
    }

}
