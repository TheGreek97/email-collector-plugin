using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PhishingDataCollector
{
    internal class URLData
    {
        private string _URL;
        private string _TLD;
        private IPAddress _IP;

        public VirusTotalScan VTScan { get; set; }
        public string HostName { get; }
        public string DomainName { get; }
        public string ProtocolDomainName { get; }
        private string Protocol { get; }
        private string Port { get; }

        //URL features
        public bool has_https;
        public bool protocol_port_match_binary;

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
        public bool https_not_trusted;
        public URLData(string uRL)
        {
            uRL = Regex.Replace(uRL, @"[\\""']+", "");
            _URL = uRL;
            Match urlMatch = Regex.Match(uRL, @"^(?:(\w+):\/\/)?(?:[^@\/\n]+@)?((?:www\.)?[^:\/?\n]+)\:?(\d)*", RegexOptions.IgnoreCase);
            Protocol = urlMatch.Groups[1].Value;  // the protocol can be http, https, ftp, etc.
            HostName = urlMatch.Groups[2].Value; // Host name (e.g., "www.studenti.uniba.it")
            Port = urlMatch.Groups[3].Value;  // Port (e.g., "8080")
            
            if (!string.IsNullOrEmpty(HostName))
            {
                Match domainMatch = Regex.Match(HostName, @"\w+(\.\w+)$");  // Domain name (e.g., "uniba.it")
                DomainName = domainMatch.Groups[0].Value;  // the full match
                _TLD = domainMatch.Groups[1].Value;
                if (string.IsNullOrEmpty(DomainName)) {
                    DomainName = HostName;
                }
                ProtocolDomainName = string.IsNullOrEmpty(Protocol) ? DomainName : Protocol + "://" + DomainName;
            }
        }

        public void ComputeURLFeatures ()
        {
            ComputeProtocolPortMatchFeature();
            has_https = Regex.IsMatch(Protocol, "https", RegexOptions.IgnoreCase); 
        }

        public void ComputeDomainFeatures()
        {
            // DNS Lookup
            DNSInfo dnsInfo = new DNSInfo(DomainName);  // try to see if domain.com is needed instead of sub.domain.com
            DNSInfo_API.PerformAPICall(dnsInfo);
            DNS_TTL = dnsInfo.GetFeatureTTL();
            DNS_info_exists_binary = dnsInfo.GetFeatureDNSInfoExists();

            // Page Rank
            PageRank pr = new PageRank(ProtocolDomainName);
            PageRank_API.PerformAPICall(pr);
            page_rank = pr.GetFeaturePageRank();
            website_traffic = pr.GetFeatureWebsiteTraffic();
            
            // WhoIS Data
            WhoIS whois = new WhoIS(DomainName);
            WhoIS_API.PerformAPICall(whois);
            domain_creation_date = whois.GetFeatureCreationDate();
            domain_expiration_date = whois.GetFeatureExpirationDate();
            domain_reg_length = whois.GetFeatureDomainRegLength();
            abnormal_URL = whois.GetFeatureAbnormalURL();
            n_name_servers = whois.GetFeatureNumNameServers();
            // Certificate 
            https_not_trusted = whois.GetFeatureSelfSignedHTTPS();
            
            return;
        }

        private void ComputeProtocolPortMatchFeature ()
        {
            protocol_port_match_binary = true;
            if (string.IsNullOrEmpty(Protocol))
            {
                return;
            }
            if (Protocol == "http")
            {
                protocol_port_match_binary = string.IsNullOrEmpty(Port) || 
                    Port == "80" || Port == "8000" || Port == "8080" || Port == "8081";
            } else if (Protocol == "https")
            {
                protocol_port_match_binary = string.IsNullOrEmpty(Port) || Port == "443";
            } else if (Protocol == "file" || Protocol == "mailto" || Protocol == "news" || 
                Protocol == "sms" || Protocol == "callto" || Protocol == "tel")
            {
                protocol_port_match_binary = string.IsNullOrEmpty(Port);
            } else if (Protocol == "ftp")
            {
                protocol_port_match_binary = Port == "21";
            }
            else if (Protocol == "ssh")
            {
                protocol_port_match_binary = Port == "22";
            }
            else if (Protocol == "telnet")
            {
                protocol_port_match_binary = Port == "23";
            }
            else if (Protocol == "gopher")
            {
                protocol_port_match_binary = Port == "70";
            }
            else if (Protocol == "rdp")  // Remote Desktop Protocol
            {
                protocol_port_match_binary = Port == "3389";
            }
            else if (Protocol == "ldap")  // Lightweight Directory Access Protocol
            {
                protocol_port_match_binary = Port == "389";
            }
            else if (Protocol == "nntp")  // Network News Transfer Protocol
            {
                protocol_port_match_binary = Port == "119";
            }
        }
    }

}
