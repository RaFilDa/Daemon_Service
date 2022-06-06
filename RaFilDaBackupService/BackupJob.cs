using Quartz;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using RaFilDaBackupService.Entities;
using System.Net.Http;
using System.Net;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Net.Http.Headers;
using System.Xml;
using FluentFTP;
using Ionic.Zip;
using System.IO.Compression;

namespace RaFilDaBackupService
{
    [DisallowConcurrentExecution]
    public class BackupJob : IJob
    {
        private HttpClient _httpClient = new HttpClient(new HttpClientHandler() { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator });
        public BackupJob()
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Program.TOKEN);
        }

        public Task Execute(IJobExecutionContext context)
        {
            JobDataMap dataMap = context.JobDetail.JobDataMap;

            List<Log> oldLogs = new List<Log>();

            var bt = new LocRemBackupTool(0,0);
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
                        dataMap.GetBoolean("jobFileType"),
                        dataMap.GetString("jobSource"),
                        dataMap.GetString("jobDestinationType"),
                        dataMap.GetString("jobDestinationPath"),
                        dataMap.GetString("jobDestinationIP"),
                        dataMap.GetString("jobDestinationUsername"),
                        dataMap.GetString("jobDestinationPassword"),
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
            if (dataMap.GetString("jobDestinationType") == "FTP")
                log.Message = "BACKUP " + state + ": " + dataMap.GetString("jobName") + " | SOURCE: " + dataMap.GetString("jobSource") + " | DESTINATION: " + dataMap.GetString("jobDestinationIP") + @"\" + dataMap.GetString("jobDestinationPath");
            else
                log.Message = "BACKUP " + state + ": " + dataMap.GetString("jobName") + " | SOURCE: " + dataMap.GetString("jobSource") + " | DESTINATION: " + dataMap.GetString("jobDestinationPath");

            oldLogs.Add(log);

            try
            {
                HttpResponseMessage response = _httpClient.GetAsync(Program.API_URL + "Computers/GetComputersByID/1").Result;

                foreach (Log l in oldLogs)
                {
                    var newLog = new StringContent(JsonSerializer.Serialize(l), Encoding.UTF8, "application/json");
                    _httpClient.PostAsync(Program.API_URL + "Reports", newLog);
                }

                List<Log> emptyLogs = new List<Log>();
                t.UpdateFile(emptyLogs, @"..\log.json");

                _httpClient.PutAsync(Program.API_URL + "Computers/UpdateLastSeen?id=" + Program.ID, null);
            }
            catch
            {
                bool repeat = true;
                while (repeat)
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

        public static bool Backup(int typeBackup, bool typeFile, string source, string destinationType, string destinationPath, string destinationIP, string destinationUsername, string destinationPassword, int retention, int packages, string name)
        {
            BackupTool bt = new LocRemBackupTool(retention, packages);
            try
            {
                if (destinationType == "FTP")
                    bt = new FTPBackupTool(destinationIP, destinationUsername, destinationPassword, retention, packages);
            }
            catch(Exception e)
            {
                return false;
            }
            
            string type = bt.GetType(typeBackup);

            try
            {
                if ((!Directory.Exists(destinationPath) && destinationType == "Loc") || (!Directory.Exists(source)))
                {
                    bt.Dispose();
                    return false;
                }
                StartBackup(type, typeFile, source, destinationPath, retention, packages, name, bt);
                bt.Dispose();
                return true;
            }
            catch (Exception e)
            {
                bt.Dispose();
                return false;
            }
            
        }

        private static void StartBackup(string typeBackup, bool typeFile, string pathSource, string pathDestination, int retention, int packages, string name, BackupTool bt)
        {
            pathDestination = pathDestination + @"\" + name + @"\";

            bt.NewLists();

            typeBackup += "_BACKUP";

            string infoPath = pathDestination + @$"\{typeBackup}\";

            bt.CreateDirectory(infoPath);

            if (!bt.CheckForFile(infoPath))
                bt.UpdateFile(infoPath, DateTime.MinValue.ToString(), bt.RETENTION, typeBackup == "FULL_BACKUP" ? 1 : bt.PACKAGES, "1");

            pathDestination = pathDestination + @$"\{typeBackup}\" + "BACKUP_" + bt.GetInfo(infoPath)[3] + pathSource.Remove(0, pathSource.LastIndexOf("\\"));

            bt.CreateDirectory(pathDestination);

            if (Convert.ToInt32(bt.GetInfo(infoPath)[2]) < bt.PACKAGES && typeBackup != "FULL_BACKUP")
                pathDestination += @"\PACKAGE_" + ((Convert.ToInt32(bt.GetInfo(infoPath)[2]) - bt.PACKAGES) * -1);
            else
                pathDestination += @"\FULL";
            if (typeFile)
            {
                bt.CreateDirectory(pathDestination);
                pathDestination += "\\";
            }

            DateTime snapshot = DateTime.Parse(bt.GetInfo(infoPath)[0]);

            if (typeFile)
            {
                foreach (string dir in Directory.GetDirectories(pathSource, "*", SearchOption.AllDirectories))
                {
                    if (new DirectoryInfo(dir).LastWriteTime <= snapshot)
                        continue;
                    bt.Dirs.Add(dir);
                    bt.CreateDirectory(dir.Replace(pathSource, pathDestination));
                }

                foreach (string file in Directory.GetFiles(pathSource, "*.*", SearchOption.AllDirectories))
                {
                    if (new FileInfo(file).LastWriteTime <= snapshot)
                        continue;
                    bt.Files.Add(file);
                    bt.Copy(file, file.Replace(pathSource, pathDestination));
                }

                bt.LogFiles(pathDestination);
            }
            else
            {
                bt.Zip(pathSource, pathDestination, snapshot);
            }

            if ((int.Parse(bt.GetInfo(infoPath)[2]) == packages || typeBackup != "DIFF_BACKUP") && typeBackup != "FULL_BACKUP")
                bt.UpdateFile(infoPath, DateTime.Now.ToString(), Convert.ToInt32(bt.GetInfo(infoPath)[1]), Convert.ToInt32(bt.GetInfo(infoPath)[2]) - 1, bt.GetInfo(infoPath)[3]);
            else
                bt.UpdateFile(infoPath, bt.GetInfo(infoPath)[0], Convert.ToInt32(bt.GetInfo(infoPath)[1]), Convert.ToInt32(bt.GetInfo(infoPath)[2]) - 1, bt.GetInfo(infoPath)[3]);

            if (bt.GetInfo(infoPath)[2] == "0")
            {
                bt.Pack(infoPath, typeBackup);
            }
        }
    }
}
