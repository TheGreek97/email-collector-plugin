using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.RegularExpressions;

namespace PhisingDataCollector
{
    class LinkData
    {
        public int score;
        private string link;

        public int n_dashes;
        public int n_underscores;
        public int n_dots;
        public int n_digits;
        public int n_slashes;
        public string TLD;
        public int url_length;
        public int domain_length;
        public float average_domain_token_length;
        public bool IP_address;

        public LinkData(string pLink)
        {
            score = 0;
            link = pLink;

            Valorize_n_dashes();
            Valorize_n_underscores();
            Valorize_n_dots();
            Valorize_n_digits();
            Valorize_n_slashes();
        }

        public void Valorize_n_dashes()
        {
            Regex rx = new Regex(@"-");

            n_dashes = rx.Matches(link).Count;
        }
        public void Valorize_n_underscores()
        {
            Regex rx = new Regex(@"_");

            n_dashes = rx.Matches(link).Count;
        }
        public void Valorize_n_dots()
        {
            Regex rx = new Regex(@".");

            n_dots = rx.Matches(link).Count;
        }
        public void Valorize_n_digits()
        {
            Regex rx = new Regex(@"[0-9]");

            n_digits = rx.Matches(link).Count;
        }
        public void Valorize_n_slashes()
        {
            Regex rx = new Regex(@"/");

            n_slashes = rx.Matches(link).Count;
        }
        public void Valorize_TLD()
        {
            string[] domains = link.Split('.');
            TLD = domains[domains.Length - 1];
        }
        public void Valorize_url_length()
        {
            url_length = link.Length;
        }
        public void Valorize_domain_length()
        {
            int temp = link.LastIndexOf('.') + 1;
            domain_length = link.Length - temp;
        }
        public void Valorize_average_domain_token_length()
        {
            string[] domains = link.Split('.');

            foreach(string s in domains)
            {
                average_domain_token_length += s.Length;
            }

            average_domain_token_length = average_domain_token_length / domains.Length;
        }
        public void Valorize_IP_address()
        {
            Regex rx = new Regex(@"^((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}$");

            IP_address = rx.IsMatch(link);
        }

    }
}
