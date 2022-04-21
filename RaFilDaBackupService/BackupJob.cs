using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace RaFilDaBackupService
{
    public class BackupJob : IJob
    {
        private readonly ILogger<BackupJob> _logger;
        public BackupJob(ILogger<BackupJob> logger)
        {
            _logger = logger;
        }

        public Task Execute(IJobExecutionContext context)
        {
            JobDataMap dataMap = context.JobDetail.JobDataMap;

            _logger.LogInformation("Backup: " + dataMap.GetString("jobName") + " STARTED");

            List<string> oldLogs = new List<string>();

            if (Backup(dataMap.GetInt("jobType"),
                      dataMap.GetString("jobSource"),
                      dataMap.GetString("jobDestination"),
                      dataMap.GetInt("jobRetention"),
                      dataMap.GetInt("jobPackages"),
                      dataMap.GetString("jobName")))
            {
                oldLogs.Add(DateTime.Now + " SUCCESSFULLY BACKUPED UP: " + dataMap.GetString("jobName"));
                _logger.LogInformation("Backup: " + dataMap.GetString("jobName") + " COMPLETED");
            }   
            else
            {
                oldLogs.Add(DateTime.Now + " ERROR IN BACKUP: " + dataMap.GetString("jobName"));
                _logger.LogInformation("Backup: " + dataMap.GetString("jobName") + " FAILED");   
            }

            StreamReader sr = new StreamReader(@"..\log.txt");
            while (!sr.EndOfStream)
            {
                oldLogs.Add(sr.ReadLine());
            }
            sr.Close();

            //TODO: ADD SENDING REPORTS TO SERVER
            if (false) //TODO: ADD CONNECTING SERVER
            {
                //TODO: SEND ALL LOGS
            }
            else
            {
                StreamWriter sw = new StreamWriter(@"..\log.txt");
                foreach (string log in oldLogs)
                {
                    sw.WriteLine(log);
                }
                sw.Close();
            }

            return Task.CompletedTask;
        }

        public static bool Backup(int type, string source, string destination, int retention, int packages, string name)
        {
            string typeBackup = "";
            switch (type)
            {
                case 0:
                    typeBackup = "FULL";
                    break;
                case 1:
                    typeBackup = "DIFF";
                    break;
                case 2:
                    typeBackup = "INC";
                    break;
            }

            try
            {
                StartBackup(typeBackup, source, destination, retention, packages, name);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }

        }

        private static void StartBackup(string typeBackup, string pathSource, string pathDestination, int retention, int packages, string name)
        {
            BackupTools bt = new BackupTools(retention, packages);

            pathDestination = pathDestination + @"\" + name + @"\";

            bt.NewLists();

            typeBackup += "_BACKUP";

            string infoPath = pathDestination + @$"\{typeBackup}\";

            Directory.CreateDirectory(infoPath);

            if (!bt.CheckForFile(infoPath))
                bt.UpdateFile(infoPath, DateTime.MinValue.ToString(), bt.RETENTION, typeBackup == "FULL_BACKUP" ? 1 : bt.PACKAGES, "1");

            pathDestination = pathDestination + @$"\{typeBackup}\" + "BACKUP_" + bt.GetInfo(infoPath)[3] + pathSource.Remove(0, pathSource.LastIndexOf("\\"));

            Directory.CreateDirectory(pathDestination);

            if (typeBackup != "FULL_BACKUP")
            {
                if (Convert.ToInt32(bt.GetInfo(infoPath)[2]) < bt.PACKAGES)
                    pathDestination += @"\PACKAGE_" + ((Convert.ToInt32(bt.GetInfo(infoPath)[2]) - 6) * -1) + "\\";
                else
                    pathDestination += @"\FULL\";
                Directory.CreateDirectory(pathDestination);
            }

            DateTime snapshot = DateTime.Parse(bt.GetInfo(infoPath)[0]);

            foreach (string dir in Directory.GetDirectories(pathSource, "*", SearchOption.AllDirectories))
            {
                if (new DirectoryInfo(dir).LastWriteTime <= snapshot)
                    continue;
                bt.Dirs.Add(dir);
                Directory.CreateDirectory(dir.Replace(pathSource, pathDestination));
            }

            foreach (string file in Directory.GetFiles(pathSource, "*.*", SearchOption.AllDirectories))
            {
                if (new FileInfo(file).LastWriteTime <= snapshot)
                    continue;
                bt.Files.Add(file);
                File.Copy(file, file.Replace(pathSource, pathDestination), true);
            }

            if (int.Parse(bt.GetInfo(infoPath)[2]) == 5 || typeBackup != "DIFF_BACKUP")
                bt.UpdateFile(infoPath, DateTime.Now.ToString(), Convert.ToInt32(bt.GetInfo(infoPath)[1]), Convert.ToInt32(bt.GetInfo(infoPath)[2]) - 1, bt.GetInfo(infoPath)[3]);
            else if (typeBackup == "DIFF_BACKUP")
                bt.UpdateFile(infoPath, bt.GetInfo(infoPath)[0], Convert.ToInt32(bt.GetInfo(infoPath)[1]), Convert.ToInt32(bt.GetInfo(infoPath)[2]) - 1, bt.GetInfo(infoPath)[3]);

            if (bt.GetInfo(infoPath)[2] == "0")
            {
                bt.Pack(infoPath, typeBackup);
            }

            bt.LogFiles(pathDestination);
        }
    }
}
