﻿/***
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

using Microsoft.Office.Interop.Outlook;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

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
        private static readonly List<string> MessageExtensions = new List<string> { ".EML", ".MBOX", ".MSG" };
        private static readonly List<string> TextExtensions = new List<string> { ".TXT", ".RTF", ".DOC", ".TEX" };
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
        public static AttachmentData ExtractFeatures(Attachment att)
        {
            try
            {
                string attachment_file_name = SaveAttachmentTemp(att);
                string file_sha;
                long file_size;

                using (SHA256 SHA256 = SHA256Managed.Create())
                {
                    using (FileStream fileStream = File.OpenRead(attachment_file_name))
                    {
                        file_sha = Convert.ToBase64String(SHA256.ComputeHash(fileStream));
                        file_size = fileStream.Length;
                    }
                    File.Delete(attachment_file_name);
                }
                AttachmentData wrap = new AttachmentData(attachment_file_name, file_sha, file_size);
                return wrap;
            }
            catch (System.Exception e)
            {
                ThisAddIn.Logger.Error("Error processing the attachment - " + e.Message);
                return null;
            }
        }

        private static string SaveAttachmentTemp(Attachment att)
        {
            string temp_folder = Environment.GetEnvironmentVariable("TEMP_FOLDER");
            if (!Directory.Exists(temp_folder))
            {
                Directory.CreateDirectory(temp_folder);
            }
            string save_path = Path.Combine(temp_folder, att.FileName);
            try
            {
                att.SaveAsFile(save_path);
            }
            catch (System.Exception e)
            {
                ThisAddIn.Logger.Error("Error saving a copy of the attachment file - " + e.Message);
            }
            return save_path;
        }
    }
}
