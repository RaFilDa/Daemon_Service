using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaFilDaBackupService
{
    class LocalBackupTool : BackupTool
    {
        public LocalBackupTool(int retention, int packages)
        {
            RETENTION = retention;
            PACKAGES = packages;
        }

        public override bool CheckForFile(string path)
        {
            return File.Exists(path + @"info.txt");
        }

        public override void DeleteOldest(string path)
        {
            List<DirectoryInfo> dirs = new List<DirectoryInfo>();
            foreach (string dir in Directory.GetDirectories(path))
            {
                dirs.Add(new DirectoryInfo(dir));
            }

            dirs.Sort((x, y) => x.CreationTime.CompareTo(y.CreationTime));
            dirs[0].Delete(true);
        }

        public override string[] GetInfo(string path)
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

        public override void LogFiles(string path)
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

        public override void UpdateFile(string path, string snapshot, int retention, int? packages, string number)
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

        public override void Zip(string pathSource, string pathDestination, DateTime snapshot)
        {
            foreach (string dir in Directory.GetDirectories(pathSource, "*", SearchOption.AllDirectories))
            {
                if (new DirectoryInfo(dir).LastWriteTime <= snapshot)
                    continue;
                Dirs.Add(dir);
            }
            foreach (string file in Directory.GetFiles(pathSource, "*.*", SearchOption.AllDirectories))
            {
                if (new FileInfo(file).LastWriteTime <= snapshot)
                    continue;
                Files.Add(file);
            }
            using (Ionic.Zip.ZipFile zip = new Ionic.Zip.ZipFile())
            {
                // foreach (string dir in Dirs)
                // {
                //     zip.AddDirectory(dir);
                //     zip.AddDirectoryByName(dir);
                // }
                zip.AddFiles(Files);
                zip.Save(pathDestination + ".zip");
            }
            File.Delete(pathSource + @"backup_file_info.txt");
        }

        public override void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public override void Copy(string source, string dest)
        {
            File.Copy(source, dest, true);
        }
    }
}
