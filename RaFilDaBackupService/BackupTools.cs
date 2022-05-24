using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using FluentFTP;

namespace RaFilDaBackupService
{
    public class BackupTools
    {
        public BackupTools(int retention = 0, int packages = 0)
        {
            RETENTION = retention;
            PACKAGES = packages;
            Directory.CreateDirectory(TMP);
        }

        public int RETENTION { get; set; }
        public int PACKAGES { get; set; }
        public List<string> Dirs = new List<string>();
        public List<string> Files = new List<string>();
        public List<string> NewFiles = new List<string>();
        public List<string> NewDirs = new List<string>();
        public const string TMP = "C:\\temp\\";
        public void NewLists()
        {
            Dirs = new List<string>();
            Files = new List<string>();
            NewDirs = new List<string>();
            NewFiles = new List<string>();
        }

        public string GetType(int num)
        {
            string typeBackup = "";
            switch (num)
            {
                case 1:
                    typeBackup = "FULL";
                    break;
                case 2:
                    typeBackup = "DIFF";
                    break;
                case 3:
                    typeBackup = "INC";
                    break;
            }
            return typeBackup;
        }

        public void LoadFiles(string path)
        {
            using StreamReader sr = new StreamReader(path + @"backup_file_info.txt");
            {
                sr.ReadLine();
                string dir = "";
                while (dir != "Files:")
                {
                    dir = sr.ReadLine();
                    if (dir != "Files:")
                        Dirs.Add(dir);
                }
                while (!sr.EndOfStream)
                {
                    Files.Add(sr.ReadLine());
                }
                sr.Close();
            }
        }

        public void LogFiles(string path)
        {
            using StreamWriter sw = new StreamWriter(path + @"backup_file_info.txt");
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
        }
        
        public void LogFilesFTP(string path, FtpClient ftp)
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

        public bool CheckForFile(string path)
        {
            if (!File.Exists(path + @"info.txt"))
                return false;
            else
                return true;
        }
        public bool CheckForFileFTP(string path, FtpClient ftp)
        {
            if (!ftp.FileExists(path + "info.txt"))
                return false;
            else
                return true;
        }

        public void UpdateFile(string path, string snapshot, int retention, int? packages, string number)
        {
            //1st line = last snapshot time
            //2nd line = RETENTION
            //3rd line = PACKAGES
            //4th line = number of backup

            using StreamWriter sw = new StreamWriter(path + @"info.txt");
            {
                sw.WriteLine(snapshot);
                sw.WriteLine(retention);
                sw.WriteLine(packages);
                sw.WriteLine(number);
                sw.Close();
            }
        }
        public void UpdateFileFTP(string path, string snapshot, int retention, int? packages, string number, FtpClient ftp)
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

        public string[] GetInfo(string path)
        {
            string[] result = new string[4];
            int indexer = 0;
            using StreamReader sr = new StreamReader(path + @"info.txt");
            {
                while (!sr.EndOfStream)
                {
                    result[indexer] = sr.ReadLine();
                    indexer++;
                }
                sr.Close();
            }
            return result;
        }
        
        public string[] GetInfoFTP(string path, FtpClient ftp)
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

        public void Pack(string path, string typeBackup)
        {
            int retention = Convert.ToInt32(GetInfo(path)[1]);
            if (retention == 1)
            {
                DeleteOldest(path);
                retention++;
            }
            UpdateFile(path, DateTime.MinValue.ToString(), retention - 1, typeBackup == "FULL_BACKUP" ? 1 : PACKAGES, (Convert.ToInt32(GetInfo(path)[3]) + 1).ToString());
        }
        
        public void PackFTP(string path, string typeBackup, FtpClient ftp)
        {
            int retention = Convert.ToInt32(GetInfoFTP(path, ftp)[1]);
            if (retention == 1)
            {
                DeleteOldestFTP(path, ftp);
                retention++;
            }
            UpdateFileFTP(path, DateTime.MinValue.ToString(), retention - 1, typeBackup == "FULL_BACKUP" ? 1 : PACKAGES, (Convert.ToInt32(GetInfoFTP(path, ftp)[3]) + 1).ToString(), ftp);
        }

        private void DeleteOldest(string path)
        {
            List<DirectoryInfo> dirs = new List<DirectoryInfo>();
            foreach (string dir in Directory.GetDirectories(path))
            {
                dirs.Add(new DirectoryInfo(dir));
            }

            dirs.Sort((x, y) => x.CreationTime.CompareTo(y.CreationTime));
            dirs[0].Delete(true);
        }
        
        private void DeleteOldestFTP(string path, FtpClient ftp)
        {
            FtpListItem[] dirs = ftp.GetListing(path).Where(x => x.Type == FtpFileSystemObjectType.Directory).ToArray();

            string oldest = dirs.OrderBy(x => x.Created).First().FullName;
                
            ftp.DeleteDirectory(oldest);
        }
    }
}
