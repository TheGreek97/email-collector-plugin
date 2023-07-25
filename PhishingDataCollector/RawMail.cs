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

        public RawMail(string id, int size, string subject,
            string body, string htmlBody, string sender,
            int numRecipients, string[] headers, AttachmentData[] attachments, 
            bool read, string folderName)
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
        }
    }
}
