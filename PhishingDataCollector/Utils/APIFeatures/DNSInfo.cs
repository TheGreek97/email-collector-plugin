using DnsClient;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;

namespace PhishingDataCollector
{
    public class DNSInfo : URLObject
    {
        //public IPAddress IP { set; get; }
        public int? TimeToLive { set; get; }
        public string DomainName { set; get; }
        public string RecordType { set; get; }

        public DNSInfo(string server) : base(server)
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
        static readonly LookupClient Client = new LookupClient(new IPAddress(134744072));  // 134744072 is the representation in long of the Google DNS "8.8.8.8"
        public static void PerformAPICall(DNSInfo domain)
        {
            try
            {
                string address = domain.Address.ToLower();
                var result = Client.Query(address, QueryType.ANY);
                var record = result?.Answers?.ToArray()?.FirstOrDefault();
                //domain.IP = result?.Answers?.ARecords()?.FirstOrDefault()?.Address;
                domain.DomainName = record?.DomainName.ToString().ToLower();
                domain.TimeToLive = record?.TimeToLive;
                domain.RecordType = record?.RecordType.ToString();
                return;
            }
            catch (Exception ex) when (ex is DnsResponseException || ex is ArgumentNullException)
            {
                Debug.WriteLine("DNS Info exception:");
                Debug.WriteLine(ex);
                domain.SetToUnknown();
                return;
            }
        }
    }
}

