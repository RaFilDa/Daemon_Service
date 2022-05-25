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
            BackupTools bt = new BackupTools();

            string type = bt.GetType(typeBackup);

            try
            {
                if (!Directory.Exists(destinationPath) || (!Directory.Exists(source)))
                    return false;
                if (destinationType == "Loc")
                    StartLocalBackup(type, typeFile, source, destinationPath, retention, packages, name);
                else
                    StartFTPBackupAsync(type, typeFile, source, destinationPath, destinationIP, destinationUsername,
                        destinationPassword, retention, packages, name);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }

        }

        private static void StartLocalBackup(string typeBackup, bool typeFile, string pathSource, string pathDestination, int retention, int packages, string name)
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
                    pathDestination += @"\PACKAGE_" + ((Convert.ToInt32(bt.GetInfo(infoPath)[2]) - bt.PACKAGES) * -1);
                else
                    pathDestination += @"\FULL";

                if (typeFile)
                {
                    pathDestination = pathDestination + "\\";
                    Directory.CreateDirectory(pathDestination);
                }
            }

            DateTime snapshot = DateTime.Parse(bt.GetInfo(infoPath)[0]);

            if (typeFile)
            {
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

                bt.LogFiles(pathDestination);
            }
            else
            {
                foreach (string dir in Directory.GetDirectories(pathSource, "*", SearchOption.AllDirectories))
                {
                    if (new DirectoryInfo(dir).LastWriteTime <= snapshot)
                        continue;
                    bt.Dirs.Add(dir);
                }
                foreach (string file in Directory.GetFiles(pathSource, "*.*", SearchOption.AllDirectories))
                {
                    if (new FileInfo(file).LastWriteTime <= snapshot)
                        continue;
                    bt.Files.Add(file);
                }
                using (Ionic.Zip.ZipFile zip = new Ionic.Zip.ZipFile())
                {
                    foreach (string dir in bt.Dirs)
                    {
                        zip.AddDirectory(dir);
                        zip.AddDirectoryByName(dir);
                    }
                    zip.AddFiles(bt.Files);
                    zip.Save(pathDestination + ".zip");
                }
                File.Delete(pathSource + @"backup_file_info.txt");
            }

            if (int.Parse(bt.GetInfo(infoPath)[2]) == packages || typeBackup != "DIFF_BACKUP")
                bt.UpdateFile(infoPath, DateTime.Now.ToString(), Convert.ToInt32(bt.GetInfo(infoPath)[1]), Convert.ToInt32(bt.GetInfo(infoPath)[2]) - 1, bt.GetInfo(infoPath)[3]);
            else if (typeBackup == "DIFF_BACKUP")
                bt.UpdateFile(infoPath, bt.GetInfo(infoPath)[0], Convert.ToInt32(bt.GetInfo(infoPath)[1]), Convert.ToInt32(bt.GetInfo(infoPath)[2]) - 1, bt.GetInfo(infoPath)[3]);

            if (bt.GetInfo(infoPath)[2] == "0")
            {
                bt.Pack(infoPath, typeBackup);
            }
        }

        private static void StartFTPBackupAsync(string typeBackup, bool typeFile, string pathSource, string pathDestination, string ip, string username, string password, int retention, int packages, string name)
        {
            BackupTools bt = new BackupTools(retention, packages);
            FtpClient ftp = new FtpClient(ip, username, password);
            ftp.EncryptionMode = FtpEncryptionMode.None;
            ftp.Connect();

            pathDestination = pathDestination + @"\" + name + @"\";

            bt.NewLists();

            typeBackup += "_BACKUP";

            string infoPath = pathDestination + @$"\{typeBackup}\";

            ftp.CreateDirectory(infoPath);

            /* CHECK */
            if (!bt.CheckForFileFTP(infoPath, ftp))
                bt.UpdateFileFTP(infoPath, DateTime.MinValue.ToString(), bt.RETENTION, typeBackup == "FULL_BACKUP" ? 1 : bt.PACKAGES, "1", ftp);

            pathDestination = pathDestination + @$"\{typeBackup}\" + "BACKUP_" + bt.GetInfoFTP(infoPath, ftp)[3] + pathSource.Remove(0, pathSource.LastIndexOf("\\"));

            ftp.CreateDirectory(pathDestination);

            if (typeBackup != "FULL_BACKUP")
            {
                if (Convert.ToInt32(bt.GetInfoFTP(infoPath, ftp)[2]) < bt.PACKAGES)
                    pathDestination += @"\PACKAGE_" + ((Convert.ToInt32(bt.GetInfoFTP(infoPath, ftp)[2]) - bt.PACKAGES) * -1);
                else
                    pathDestination += @"\FULL";

                if (typeFile)
                {
                    pathDestination = pathDestination + "\\";
                    ftp.CreateDirectory(pathDestination);
                }
            }

            DateTime snapshot = DateTime.Parse(bt.GetInfoFTP(infoPath, ftp)[0]);

            if (typeFile)
            {
                foreach (string dir in Directory.GetDirectories(pathSource, "*", SearchOption.AllDirectories))
                {
                    if (new DirectoryInfo(dir).LastWriteTime <= snapshot)
                        continue;
                    bt.Dirs.Add(dir);
                    ftp.CreateDirectory(dir.Replace(pathSource, pathDestination));
                }

                foreach (string file in Directory.GetFiles(pathSource, "*.*", SearchOption.AllDirectories))
                {
                    if (new FileInfo(file).LastWriteTime <= snapshot)
                        continue;
                    bt.Files.Add(file);
                    ftp.UploadFile(file, file.Replace(pathSource, pathDestination));
                }

                bt.LogFilesFTP(pathDestination, ftp);
            }
            else
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

            if (int.Parse(bt.GetInfoFTP(infoPath, ftp)[2]) == packages || typeBackup != "DIFF_BACKUP")
                bt.UpdateFileFTP(infoPath, DateTime.Now.ToString(), Convert.ToInt32(bt.GetInfoFTP(infoPath, ftp)[1]), Convert.ToInt32(bt.GetInfoFTP(infoPath, ftp)[2]) - 1, bt.GetInfoFTP(infoPath, ftp)[3], ftp);
            else if (typeBackup == "DIFF_BACKUP")
                bt.UpdateFileFTP(infoPath, bt.GetInfoFTP(infoPath, ftp)[0], Convert.ToInt32(bt.GetInfoFTP(infoPath, ftp)[1]), Convert.ToInt32(bt.GetInfoFTP(infoPath, ftp)[2]) - 1, bt.GetInfoFTP(infoPath, ftp)[3], ftp);

            if (bt.GetInfoFTP(infoPath, ftp)[2] == "0")
            {
                bt.PackFTP(infoPath, typeBackup, ftp);
            }
            ftp.Disconnect();
        }
    }
}
