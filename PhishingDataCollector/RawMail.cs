/***
 *  This file is part of Dataset-Collector.

    Dataset-Collector is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Dataset-Collector is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Dataset-Collector.  If not, see <http://www.gnu.org/licenses/>. 
 * 
 * ***/

using System;

namespace PhishingDataCollector
{
    internal class RawMail
    {
        public string EntryID { get; }
        public string Subject { get; }
        public string Body { get; }
        public string HTMLBody { get; }
        public string Sender { get; }
        public AttachmentData[] Attachments { get; }
        public string[] Headers { get; }
        public int Size { get; }
        public int NumRecipients { get;}
        public bool IsRead { get; }
        public string Folder { get; }
        public DateTime Date { get; }

        public RawMail(string id, int size, string subject,
            string body, string htmlBody, string sender,
            int numRecipients, string[] headers, AttachmentData[] attachments, 
            bool read, string folderName, DateTime datetime)
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
            IsRead = read;
            Folder = folderName;
            Date = datetime.Date;
        }
    }
}
