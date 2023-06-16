using DnsClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PhishingDataCollector
{
    internal class URLData
    {
        private string URL;
        private string host_name;
        private string TLD;
        private IPAddress IP;

        //URL features


        //Domain-based features
        public bool DNS_info_exists_binary;

        public URLData(string uRL)
        {
            URL = uRL;
            host_name = uRL; // get host name (e.g., www.uniba.it
            //...
            TLD = uRL; // get top-level domain only

            ComputeDomainFeatures();
        }

        private async void ComputeDomainFeatures()
        {
            var lookup = new LookupClient();
            var result = await lookup.QueryAsync(host_name, QueryType.A);

            var record = result.Answers.ARecords().FirstOrDefault();
            IP = record?.Address;
        }
    }
}
