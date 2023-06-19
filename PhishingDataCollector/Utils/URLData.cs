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
        //URL features


        //Domain-based features
        public bool DNS_info_exists_binary;

        public URLData(string uRL)
        {
            uRL = Regex.Replace(uRL, @"[\\""']+", "");
            _URL = uRL;
            HostName = Regex.Match(uRL, @"^(?:https?:\/\/)?(?:[^@\/\n]+@)?(?:www\.)?[^:\/?\n]+", RegexOptions.IgnoreCase).Groups[0].Value; // get host name (e.g., www.uniba.it)
            //...
            //_TLD = uRL; // get top-level domain only

            //ComputeDomainFeatures();
        }

        private async void ComputeDomainFeatures()
        {
            // DNS Lookup
            //var lookup = new LookupClient(IPAddress.Parse("8.8.8.8"));
            var lookup = new LookupClient(new IPAddress(134744072));  // 134744072 is the representation in long of the Google DNS "8.8.8.8"
            var result = await lookup.QueryAsync(HostName, QueryType.A);

            var record = result.Answers.ARecords().FirstOrDefault();
            _IP = record?.Address;
        }
    }
}
