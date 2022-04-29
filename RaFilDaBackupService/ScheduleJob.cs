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
using Quartz.Impl;
using System.Net.Http.Headers;

namespace RaFilDaBackupService
{
    public class ScheduleJob : IJob
    {
        private HttpClient _httpClient = new HttpClient(new HttpClientHandler() { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator });
        public ScheduleJob()
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Program.TOKEN);
        }
        public HttpTools tools = new HttpTools();
        public Task Execute(IJobExecutionContext context)
        {
            _httpClient.PutAsync(Program.API_URL + "Computers/UpdateLastSeen?id=" + Program.ID, null);

            if (Program._sheduler != null)
                Program._sheduler.Shutdown();

            BuildScheduler();

            return Task.CompletedTask;
        }

        public void BuildScheduler()
        {
            ISchedulerFactory schedulerFactory = new StdSchedulerFactory();
            IScheduler scheduler = schedulerFactory.GetScheduler().Result;
            Program._sheduler = scheduler;
            Program._sheduler.Start();
            BuildJobs(Program._sheduler);
        }


        public void BuildJobs(IScheduler sched)
        {
            var configs = tools.GetConfigs();
            foreach (ConfigInfo configInfo in configs)
            {
                foreach (Source s in configInfo.Sources)
                {
                    foreach (Destination d in configInfo.Destinations)
                    {
                        string name = configInfo.Config.Name + "_" + s.Path + "_" + d.Path;
                        var JobKey = new JobKey(name);
                        IJobDetail job = JobBuilder.Create<BackupJob>()
                            .WithIdentity(JobKey)                       
                            .UsingJobData("jobId", configInfo.Config.Id)
                            .UsingJobData("jobName", configInfo.Config.Name)
                            .UsingJobData("jobType", configInfo.Config.BackupType)
                            .UsingJobData("jobSource", s.Path)
                            .UsingJobData("jobDestination", d.Path)
                            .UsingJobData("jobRetention", configInfo.Config.RetentionSize)
                            .UsingJobData("jobPackages", configInfo.Config.PackageSize)
                            .Build();

                        ITrigger trigger = TriggerBuilder.Create()
                            .ForJob(JobKey)
                            .WithIdentity("t_" + name)
                            .WithCronSchedule(configInfo.Config.Cron)
                            .Build();

                        sched.ScheduleJob(job, trigger);
                    }
                }
            }
        }
    }
}
