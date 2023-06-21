using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using DnsClient;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace PhishingDataCollector
{
    public class DNSInfo : URLObject
    {
        //public IPAddress IP { set; get; }
        public int? TimeToLive { set; get; }
        public string DomainName { set; get; }
        public string RecordType { set; get; }

        public DNSInfo (string server) : base(server)
        {
            SetToUnknown();
        }
        public DNSInfo(string server, int ttl) : base(server)
        {
            TimeToLive = ttl;
        }
        public bool GetFeatureDNSInfoExists()
        {
            return DomainName != null;
        }
        public int GetFeatureTTL()
        {
            return TimeToLive ?? 0;
        }
        public void SetToUnknown()
        {
            DomainName = null;
            TimeToLive = null;
            RecordType = null;
        }
    }

    public class DNSInfoCollection : URLsCollection
    {
        public void CopyTo(DNSInfo[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException("The array cannot be null.");
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException("The starting array index cannot be negative.");
            if (Count > array.Length - arrayIndex)
                throw new ArgumentException("The destination array has fewer elements than the collection.");

            for (int i = 0; i < innerCol.Count; i++)
            {
                array[i + arrayIndex] = (DNSInfo)innerCol[i];
            }
        }
    }

    public static class DNSInfo_API
    {
        public static async Task PerformAPICall(DNSInfo domain)
        {
            //var lookup = new LookupClient(IPAddress.Parse("8.8.8.8"));
            var lookup = new LookupClient(new IPAddress(134744072));  // 134744072 is the representation in long of the Google DNS "8.8.8.8"
            try
            {
                string address = domain.Address.ToLower();
                var result = await lookup.QueryAsync(address, QueryType.ANY);
                var record = result?.Answers?.ToArray()?.FirstOrDefault();
                //domain.IP = result?.Answers?.ARecords()?.FirstOrDefault()?.Address;
                domain.DomainName = record?.DomainName.ToString().ToLower();
                domain.TimeToLive = record?.TimeToLive;
                domain.RecordType = record?.RecordType.ToString();
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                domain.SetToUnknown();
                return;
            }
        }
    }
}

