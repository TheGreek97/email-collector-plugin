using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhisingDataCollector
{
    class LinkData
    {
        public int score;
        private string link;

        public LinkData(string pLink)
        {
            score = 0;
            link = pLink;
        }
    }
}
