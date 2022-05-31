using FluentFTP;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RaFilDaBackupService
{
    class FTPBackupTool : BackupTool, IDisposable
    {
        public FTPBackupTool(string ip, string username, string password, int retention, int packages)
        {
            this.ip = ip;
            this.username = username;
            this.password = password;

            RETENTION = retention;
            PACKAGES = packages;
            
            ftp = new FtpClient(this.ip, this.username, this.password);
            ftp.EncryptionMode = FtpEncryptionMode.None;
            ftp.Connect();

            Directory.CreateDirectory(TMP);
        }
        private string ip;
        private string username;
        private string password;
        public FtpClient ftp { get; set; }
        public override void LogFiles(string path)
        {
            using StreamWriter sw = new StreamWriter(TMP + @"backup_file_info.txt");
            {
                sw.WriteLine("Dirs:");
                foreach (string item in Dirs)
                {
                    sw.WriteLine(item);
                }
                sw.WriteLine("Files:");
                foreach (string item in Files)
                {
                    sw.WriteLine(item);
                }
                sw.Close();
            }

            ftp.UploadFile(TMP + @"backup_file_info.txt", path + @"backup_file_info.txt");
            File.Delete(TMP + @"backup_file_info.txt");
        }

        public override bool CheckForFile(string path)
        {
            return ftp.FileExists(path + "info.txt");
        }
        public override void UpdateFile(string path, string snapshot, int retention, int? packages, string number)
        {
            //1st line = last snapshot time
            //2nd line = RETENTION
            //3rd line = PACKAGES
            //4th line = number of backup

            using StreamWriter sw = new StreamWriter(TMP + @"info.txt");
            {
                sw.WriteLine(snapshot);
                sw.WriteLine(retention);
                sw.WriteLine(packages);
                sw.WriteLine(number);
                sw.Close();
            }

            ftp.UploadFile(TMP + @"info.txt", path + @"info.txt");
            File.Delete(TMP + @"info.txt");
        }

        public override string[] GetInfo(string path)
        {
            string[] result = new string[4];
            int indexer = 0;
            ftp.DownloadFile(TMP + @"info.txt", path + @"info.txt");
            using StreamReader sr = new StreamReader(TMP + @"info.txt");
            {
                while (!sr.EndOfStream)
                {
                    result[indexer] = sr.ReadLine();
                    indexer++;
                }
                sr.Close();
            }
            File.Delete(TMP + @"info.txt");
            return result;
        }

        public override void Pack(string path, string typeBackup)
        {
            int retention = Convert.ToInt32(GetInfo(path)[1]);
            if (retention == 0)
            {
                DeleteOldest(path);
                retention++;
            }
            UpdateFile(path, DateTime.MinValue.ToString(), retention - 1, typeBackup == "FULL_BACKUP" ? 1 : PACKAGES, (Convert.ToInt32(GetInfo(path)[3]) + 1).ToString());
        }

        public override void DeleteOldest(string path)
        {
            FtpListItem[] dirs = ftp.GetListing(path).Where(x => x.Type == FtpFileSystemObjectType.Directory).ToArray();

            string oldest = dirs.OrderBy(x => x.Created).First().FullName;

            ftp.DeleteDirectory(oldest);
        }

        public override void Zip(string pathSource, string pathDestination, DateTime snapshot)
        {
            using (Stream memoryStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (string path in Directory.EnumerateFiles(pathSource, "*.*", SearchOption.AllDirectories))
                    {
                        if (new FileInfo(path).LastWriteTime <= snapshot)
                            continue;

                        ZipArchiveEntry entry = archive.CreateEntry(path);

                        using (Stream entryStream = entry.Open())
                        using (Stream fileStream = File.OpenRead(path))
                        {
                            fileStream.CopyTo(entryStream);
                        }
                    }

                }

                memoryStream.Seek(0, SeekOrigin.Begin);

                var request =
                    WebRequest.Create("ftp://" + ip + "/" + pathDestination + ".zip");
                request.Credentials = new NetworkCredential(username, password);
                request.Method = WebRequestMethods.Ftp.UploadFile;
                using (Stream ftpStream = request.GetRequestStream())
                {
                    memoryStream.CopyTo(ftpStream);
                }
                memoryStream.Dispose();
            }
        }

        public override void CreateDirectory(string path)
        {
            ftp.CreateDirectory(path);
        }

        public override void Copy(string source, string dest)
        {
            ftp.UploadFile(source, dest);
        }

        public override void Dispose()
        {
            ftp.Disconnect();
        }
    }
}
