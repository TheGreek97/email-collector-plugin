using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace PhishingDataCollector
{
    public class PageRank : URLObject
    {
        public byte RankDecimal { set; get; }
        public int? RankAbsolute { set; get; }

        public PageRank (string server) : base(server)
        {
            SetToUnknown();
        }
        public PageRank(string server, byte rank_decimal, int webiste_traffic) : base(server)
        { 
            RankDecimal = rank_decimal;
            RankAbsolute = webiste_traffic;
        }
        public byte GetFeaturePageRank()
        {
            return RankDecimal;
        }
        public int? GetFeatureWebsiteTraffic()
        {
            return RankAbsolute;
        }
        public void SetToUnknown()
        { 
            RankDecimal = RankDecimal != 0 ? RankDecimal : (byte) 0;
            RankAbsolute = RankAbsolute != null ? RankAbsolute : null;
        }
    }

    public class PageRankCollection : URLsCollection
    {
        public void CopyTo(PageRank[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException("The array cannot be null.");
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException("The starting array index cannot be negative.");
            if (Count > array.Length - arrayIndex)
                throw new ArgumentException("The destination array has fewer elements than the collection.");

            for (int i = 0; i < innerCol.Count; i++)
            {
                array[i + arrayIndex] = (PageRank)innerCol[i];
            }
        }
    }

    public static class PageRank_API
    {

        private static readonly string _api_key = Environment.GetEnvironmentVariable("APIKEY__OPEN_PAGE_RANK");
        private const string _api_request_url = "https://openpagerank.com/api/v1.0/getPageRank";
        public static void PerformAPICall(PageRank page)
        {
            var queryParameters = new Dictionary<string, string>()
            {
                ["domains[0]"] = page.Address,
                ["API-OPR"] = _api_key
            };
            var api_url = QueryHelpers.AddQueryString(_api_request_url, queryParameters);
            
            try
            {
                //if (ThisAddIn.HTTPCLIENT == null) ThisAddIn.HTTPCLIENT = new System.Net.Http.HttpClient();
                ThisAddIn.HTTPCLIENT.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = ThisAddIn.HTTPCLIENT.GetAsync(api_url).Result;
                {
                    if (response.IsSuccessStatusCode)
                    {
                        string resultString = response.Content.ReadAsStringAsync().Result;

                        JObject jsonObject = (JObject)((JObject)JsonConvert.DeserializeObject(resultString)).GetValue("response")[0];  // contains the array of results (in our case it should hold only 1 element

                        if (jsonObject != null)
                        {
                            page.RankDecimal = (byte)jsonObject.GetValue("page_rank_decimal");
                            page.RankAbsolute = (int)jsonObject.GetValue("rank");
                        }
                        else { page.SetToUnknown(); }
                    }
                    else { page.SetToUnknown(); }
                    response.Dispose();
                }
            }
            catch (Exception ex) when (ex is DnsClient.DnsResponseException || ex is JsonException)
            {
                page.SetToUnknown();
                Debug.WriteLine(ex);
            }
            return;
        }
    }
}

