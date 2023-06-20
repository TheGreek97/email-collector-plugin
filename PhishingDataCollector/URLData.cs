using DnsClient;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace PhishingDataCollector
{
    internal class URLData
    {
        private string _URL;
        private string _TLD;
        private IPAddress _IP;

        public VirusTotalScan VTScan { get; set; }
        public string HostName { get; }
        private string Protocol { get; }

        //URL features
        public bool has_https;

        //Domain-based features
        public bool DNS_info_exists_binary;
        public int DNS_TTL;

        public URLData(string uRL)
        {
            uRL = Regex.Replace(uRL, @"[\\""']+", "");
            _URL = uRL;
            HostName = Regex.Match(uRL, @"^(?:\w+:\/\/)?(?:[^@\/\n]+@)?(?:www\.)?[^:\/?\n]+", RegexOptions.IgnoreCase).Groups[0].Value; // get host name (e.g., www.uniba.it)
            Protocol = Regex.Match(HostName, @"^\w+:", RegexOptions.IgnoreCase).Value;  // the protocol can be http, https, ftp, etc.
            Protocol = Protocol.Substring(0, Protocol.Length - 1); // strip the trailing ":"
            HostName = Regex.Replace(HostName, @"^\w+:\/\/", "", RegexOptions.IgnoreCase);  // strip the protocol
            _TLD = Regex.Match(HostName, @"\.\w+$").Value;  // Top-Level Domain  (e.g., ".com")
        }

        public void ComputeURLFeatures ()
        {
            has_https = Regex.IsMatch(Protocol, "https", RegexOptions.IgnoreCase); 
        }

        public async void ComputeDomainFeatures()
        {
            // DNS Lookup
            //var lookup = new LookupClient(IPAddress.Parse("8.8.8.8"));
            var lookup = new LookupClient(new IPAddress(134744072));  // 134744072 is the representation in long of the Google DNS "8.8.8.8"
            var result = await lookup.QueryAsync(HostName, QueryType.A);
            var record = result?.Answers?.ARecords()?.FirstOrDefault();
            _IP = record?.Address;
            DNS_info_exists_binary = _IP != null;
            DNS_TTL = record?.TimeToLive ?? 0;
        }
    }
}
