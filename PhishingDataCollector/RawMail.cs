using Microsoft.Office.Interop.Outlook;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhishingDataCollector
{
    internal class RawMail
    {
        public string EntryID { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public string HTMLBody { get; set; }
        public string Sender { get; set; }
        public string[] Attachments { get; set; }
        public string[] Headers { get; set; }
        public int Size { get; set; }
        public int NumRecipients { get; set; }

        public RawMail(string id, int size, string subject, 
            string body, string htmlBody, string sender, 
            int numRecipients, string[] headers, string[] attachments ) 
        { 
            EntryID = id;
            Subject = subject;
            Size = size; 
            Body = body;
            HTMLBody = htmlBody;
            Sender = sender;
            NumRecipients = numRecipients;
            Headers = headers;
            Attachments = attachments;
        }
    }
}
