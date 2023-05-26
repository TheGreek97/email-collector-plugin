using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Outlook = Microsoft.Office.Interop.Outlook;
using Office = Microsoft.Office.Core;

using System.Text.RegularExpressions;

namespace PhisingDataCollector
{

    class MailData
    {
        
        private Outlook.MailItem mail;
        private Outlook.MailItem mailOriginal;

        public bool plain_Text;
        public int number_of_html_comments_tag;

        public MailData(Outlook.MailItem pMail)
        {
            mail = pMail;
            mailOriginal = pMail;

            //Bring all the mail to uppercase to semplify the search
            mail.Body = mail.Body.ToUpper();
            mail.HTMLBody = mail.HTMLBody.ToUpper();

            Valorize_plain_Text();
            Valorize_Number_of_html_comments_tag();
        }

        private void Valorize_plain_Text()
        {
            plain_Text = mail.Body == mail.HTMLBody;
        }

        private void Valorize_Number_of_html_comments_tag()
        {
            Regex rx = new Regex(@"<!--\b");

            number_of_html_comments_tag = rx.Matches(mail.HTMLBody).Count;
        }

       
    }
}
