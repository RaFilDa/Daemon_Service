using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaFilDaBackupService
{
    abstract class BackupTool
    {
        public int RETENTION { get; set; }
        public int PACKAGES { get; set; }
        public List<string> Dirs { get; set; }
        public List<string> Files { get; set; }
        public List<string> NewFiles { get; set; }
        public List<string> NewDirs { get; set; }
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

        public virtual void LogFiles(string path) { throw new NotImplementedException(); }
        public virtual bool CheckForFile(string path) { throw new NotImplementedException(); }
        public virtual void UpdateFile(string path, string snapshot, int retention, int? packages, string number) { throw new NotImplementedException(); }
        public virtual string[] GetInfo(string path) { throw new NotImplementedException(); }
        public virtual void Pack(string path, string typeBackup) { throw new NotImplementedException(); }
        public virtual void DeleteOldest(string path) { throw new NotImplementedException(); }
        public virtual void Zip(string pathSource, string pathDestination, DateTime snapshot) { throw new NotImplementedException(); }
        public virtual void CreateDirectory(string path) => throw new NotImplementedException();
        public virtual void Copy(string source, string dest) => throw new NotImplementedException();
    }
}
