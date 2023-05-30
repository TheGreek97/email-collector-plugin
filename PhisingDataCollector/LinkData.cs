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

        public LinkData(string pLink)
        {
            score = 0;
            link = pLink;

            Valorize_n_dashes();
            Valorize_n_underscores();
            Valorize_n_dots();
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
    }
}
