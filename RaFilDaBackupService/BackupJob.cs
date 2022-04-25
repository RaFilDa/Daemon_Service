using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using RaFilDaBackupService.Entities;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Threading;

namespace RaFilDaBackupService
{
    public class BackupJob : IJob
    {
        public HttpClientHandler handler = new HttpClientHandler() { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
        public Task Execute(IJobExecutionContext context)
        {
            JobDataMap dataMap = context.JobDetail.JobDataMap;

            List<Log> oldLogs = new List<Log>();

            var bt = new BackupTools();
            var t = new GenericHttpTools<Log>();
            var t2 = new HttpTools();
            try
            {
                oldLogs = new List<Log>(t.LoadFile(@"..\log.json"));
            }
            catch
            {
                bool repeat = true;
                while (repeat)
                {
                    try
                    {
                        oldLogs = new List<Log>(t.LoadFile(@"..\log.json"));
                        repeat = false;
                    }
                    catch
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
            var log = new Log();
            string state = "";

            if (Backup(dataMap.GetInt("jobType"),
                      dataMap.GetString("jobSource"),
                      dataMap.GetString("jobDestination"),
                      dataMap.GetInt("jobRetention"),
                      dataMap.GetInt("jobPackages"),
                      dataMap.GetString("jobName")))
            {
                state = "SUCCESSFUL";
                log.IsError = false;
            }   
            else
            {
                state = "ERROR";
                log.IsError = true;
            }

            log.CompConfID = t2.GetCompConfigID(dataMap.GetInt("jobId"));
            log.Type = bt.GetType(dataMap.GetInt("jobType"));
            log.Message = "BACKUP " + state + ": " + dataMap.GetString("jobName") + " | SOURCE: " + dataMap.GetString("jobSource") + " | DESTINATION: " + dataMap.GetString("jobDestination");

            oldLogs.Add(log);

            try
            {
                var httpClient = new HttpClient(handler);

                HttpResponseMessage response = httpClient.GetAsync(Program.API_URL + "Computers/GetComputersByID/1").Result;

                foreach (Log l in oldLogs)
                {
                    var newLog = new StringContent(JsonSerializer.Serialize(l), Encoding.UTF8, "application/json");
                    httpClient.PostAsync(Program.API_URL + "Reports", newLog);
                }

                List<Log> emptyLogs = new List<Log>();
                t.UpdateFile(emptyLogs, @"..\log.json");
            }
            catch
            {
                bool repeat = true;
                while(repeat)
                {
                    try
                    {
                        t.UpdateFile(oldLogs, @"..\log.json");
                        repeat = false;
                    }
                    catch
                    {
                        Thread.Sleep(1000);
                    }
                }
            }

            return Task.CompletedTask;
        }

        public static bool Backup(int type, string source, string destination, int retention, int packages, string name)
        {
            BackupTools bt = new BackupTools();

            string typeBackup = bt.GetType(type);

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
