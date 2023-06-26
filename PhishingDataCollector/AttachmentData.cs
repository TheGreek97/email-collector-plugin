using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Office.Interop.Outlook;

namespace PhishingDataCollector
{
    internal class AttachmentData
    {
        public long Size { get; set; }
        public string SHA256 { get; set; }

        private string _file_name { get; set; }
        private string _file_type { get; set; }
        private static readonly List<string> ImageExtensions = new List<string> { ".JPG", ".JPEG", ".JPE", ".BMP", ".GIF", ".PNG", ".WEBP",
            ".EPS", ".NEF", ".RAW", ".TIF", ".TIFF", ".WBMP"};
        private static readonly List<string> ApplicationExtensions = new List<string> { ".EXE", ".BAT", ".COM", ".CMD", ".INF", ".IPA", 
            ".OSX", ".PIF", ".RUN", "WSH", ".APK"};
        private static readonly List<string> MessageExtensions = new List<string> { ".EML", ".MBOX", ".MSG"};
        private static readonly List<string> TextExtensions = new List<string> { ".TXT", ".RTF", ".DOC", ".TEX"};
        private static readonly List<string> VideoExtensions = new List<string> { ".WEBM", ".MKV", ".MP4", ".FLV", ".VOB", ".OGV", ".OGG",
            ".DRC", ".AVI", ".MPEG", ".MTS", ".M2TS", ".MOV", ".QT", "WMV", ".AMV", ".M4P", ".M4V", ".MPG", ".MP2", ".MPEG", ".MPE", ".M4V"};
        
        public AttachmentData(string file_name, string sha, long size) 
        { 
            _file_name = file_name;
            SHA256 = sha;
            Size = size;
        }
        public string GetAttachmentType() 
        { 
            if (string.IsNullOrEmpty(_file_type))
            {
                string fileExtension = Path.GetExtension(_file_name).ToUpperInvariant();
                string type = "";
                if (ImageExtensions.Contains(fileExtension)) { type = "img"; }
                else if (ApplicationExtensions.Contains(fileExtension)) { type = "app"; }
                else if (MessageExtensions.Contains(fileExtension)) { type = "message"; } 
                else if (TextExtensions.Contains(fileExtension)) { type = "text"; } 
                else if (VideoExtensions.Contains(fileExtension)) { type = "video"; } 
                else { type = "other"; }
                _file_type = type;
            }
            return _file_type;
        }
        public static AttachmentData ExtractFeatures (Attachment att) 
        {
            string attachment_file_name = att.GetTemporaryFilePath();
            string file_sha;
            long file_size;
            try
            {
                using (SHA256 SHA256 = SHA256Managed.Create())
                {
                    using (FileStream fileStream = File.OpenRead(attachment_file_name))
                    {
                        file_sha = Convert.ToBase64String(SHA256.ComputeHash(fileStream));
                        file_size = fileStream.Length;
                    }
                }
            }
            catch (System.Exception)
            {
                return null;
            }
            AttachmentData wrap = new AttachmentData(attachment_file_name, file_sha, file_size);
            return wrap;
        }
    }
}
