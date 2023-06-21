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
        public string DomainName { get; set; }
        private string Protocol { get; }

        //URL features
        public bool has_https;

        //Domain-based features
        public bool DNS_info_exists_binary;
        public int DNS_TTL;
        public byte page_rank;
        public int? website_traffic;
        public double domain_creation_date;
        public double domain_expiration_date;
        public double domain_reg_length;
        public bool abnormal_URL;
        public byte n_name_servers;

        public URLData(string uRL)
        {
            uRL = Regex.Replace(uRL, @"[\\""']+", "");
            _URL = uRL;
            HostName = Regex.Match(uRL, @"^(?:\w+:\/\/)?(?:[^@\/\n]+@)?(?:www\.)?[^:\/?\n]+", RegexOptions.IgnoreCase).Groups[0].Value; // Host name (e.g., "www.studenti.uniba.it")
            Protocol = Regex.Match(HostName, @"^\w+:", RegexOptions.IgnoreCase).Value;  // the protocol can be http, https, ftp, etc.
            if (! string.IsNullOrEmpty(Protocol) ) {
                Protocol = Protocol.Substring(0, Protocol.Length - 1); // strip the trailing ":"
            }
            if (!string.IsNullOrEmpty(HostName))
            {
                HostName = Regex.Replace(HostName, @"^\w+:\/\/", "", RegexOptions.IgnoreCase);  // strip the protocol
                DomainName = Regex.Match(HostName, @"\w+\.\w+$").Value;  //The Domain name (e.g., "uniba.it")
                if (!string.IsNullOrEmpty(DomainName)) { 
                    _TLD = Regex.Match(DomainName, @"\.\w+$").Value;  // Top-Level Domain  (e.g., ".it) 
                } else
                {
                    DomainName = HostName;
                }
            }
        }

        public void ComputeURLFeatures ()
        {
            has_https = Regex.IsMatch(Protocol, "https", RegexOptions.IgnoreCase); 
        }

        public async void ComputeDomainFeatures()
        {
            // DNS Lookup
            DNSInfo dnsInfo = new DNSInfo(DomainName);  // try to see if domain.com is needed instead of sub.domain.com
            await DNSInfo_API.PerformAPICall(dnsInfo);
            DNS_TTL = dnsInfo.GetFeatureTTL();
            DNS_info_exists_binary = dnsInfo.GetFeatureDNSInfoExists();

            // Page Rank
            PageRank pr = new PageRank(DomainName);
            await PageRank_API.PerformAPICall(pr);
            page_rank = pr.GetFeaturePageRank();
            website_traffic = pr.GetFeatureWebsiteTraffic();

            // WhoIS Data
            WhoIS whois = new WhoIS(DomainName);
            await WhoIS_API.PerformAPICall(whois);
            domain_creation_date = whois.GetFeatureCreationDate();
            domain_expiration_date = whois.GetFeatureExpirationDate();
            domain_reg_length = whois.GetFeatureDomainRegLength();
            abnormal_URL = whois.GetFeatureAbnormalURL();
            n_name_servers = whois.GetFeatureNumNameServers();
        }
    }

}
