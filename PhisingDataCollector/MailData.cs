﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Outlook = Microsoft.Office.Interop.Outlook;
using Office = Microsoft.Office.Core;

using System.Text.RegularExpressions;
using System.IO.MemoryMappedFiles;
using System.Runtime.Serialization.Formatters.Binary;

namespace PhisingDataCollector
{

    class MailData
    {
        
        private Outlook.MailItem mail;
        private Outlook.MailItem mailOriginal;
        private List<LinkData> linkList;

        public bool plain_Text;
        public int number_of_html_comments_tag;
        public int number_of_words_body;
        public int account_count_in_body;
        public int n_images;
        public float proportion_Words_No_Vowels;
        public int count_href_tag;
        public int table_tag_count;
        public int noLinks;
        public float capRatio;

        public MailData(Outlook.MailItem pMail)
        {
            LinkData ld;

            mail = pMail;
            mailOriginal = pMail;
            linkList = new List<LinkData>();

            //Bring all the mail to uppercase to semplify the search
            mail.Body = mail.Body.ToUpper();
            mail.HTMLBody = mail.HTMLBody.ToUpper();

            Regex rx = new Regex(@"<a(.*?)>(.*?)</a>");

            foreach (Match link in rx.Matches(mail.HTMLBody))  {
                ld = new LinkData(link.Value);
                linkList.Add(ld);
            }

            Valorize_plain_Text();
            Valorize_Number_of_html_comments_tag();
            Valorize_Number_of_words_body();
            Valorize_account_count_in_body();
            Valorize_n_images();
            Valorize_Proportion_Words_No_Vowels();
            Valorize_Count_href_tag();
            Valorize_table_tag_count();
            Valorize_noLinks();
            Valorize_capRatio();
        }

        private void Valorize_plain_Text()
        {
            plain_Text = mail.Body == mail.HTMLBody;
        }
        private void Valorize_Number_of_html_comments_tag()
        {
            Regex rx = new Regex(@"<!--(.*?)-->");

            number_of_html_comments_tag = rx.Matches(mail.HTMLBody).Count;
        }
        private void Valorize_Number_of_words_body()
        {
            Regex rx = new Regex(@"[\w-]+");

            number_of_words_body = rx.Matches(mail.HTMLBody).Count;
        }
        private void Valorize_account_count_in_body() {
            Regex rx = new Regex(@"ACCOUNT");

            account_count_in_body = rx.Matches(mail.Body).Count;
        }
        private void Valorize_n_images()
        {
            Regex rx = new Regex(@"<IMG([\w\W]+?)/?>");

            n_images = rx.Matches(mail.Body).Count;
        }
        private void Valorize_Proportion_Words_No_Vowels()
        {
            Regex rx = new Regex(@"\\b([^AEIOU\\s]+)\\b");

            proportion_Words_No_Vowels = rx.Matches(mail.Body).Count/ number_of_words_body;
        }
        private void Valorize_Count_href_tag()
        {
            Regex rx = new Regex(@"HREF");

            count_href_tag = rx.Matches(mail.Body).Count;
        }
        private void Valorize_table_tag_count()
        {
            Regex rx = new Regex(@"<TABLE");

            table_tag_count = rx.Matches(mail.Body).Count;
        }
        private void Valorize_noLinks()
        {
            noLinks = linkList.Count();
        }
        public void Valorize_capRatio()
        {
            Regex rxM = new Regex(@"[A-Z]");
            Regex rxm = new Regex(@"[a-z]");

            try
            {
                capRatio = rxM.Matches(mailOriginal.Body).Count / rxm.Matches(mailOriginal.Body).Count;
            }
            catch(Exception es)
            {
                capRatio = 0;
            }
        }

    }
}
