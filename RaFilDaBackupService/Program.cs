using Microsoft.Extensions.Hosting;
using Quartz;
using RaFilDaBackupService.Entities;
using System.Threading;
using System.Collections.Generic;
using Quartz.Impl;
using System.Threading.Tasks;
using System.IO;

namespace RaFilDaBackupService
{
    public class Program
    {
        public static IScheduler _scheduler = null;
        public static string API_URL = "https://localhost:5001/";
        public static int ID { get; set; }
        public static string TOKEN { get; set; }

        public static void Main(string[] args)
        {
            var sr = new StreamReader(@"..\token.txt");
            TOKEN = sr.ReadToEnd();
            sr.Close();

            var t = new HttpTools();
            ID = t.GetID();

            CreateHostBuilder(args).Build().Run();
        }
        
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddQuartz(q =>
                    {
                        q.UseMicrosoftDependencyInjectionScopedJobFactory();

                        var jobKey = new JobKey("Scheduler");
                        q.AddJob<ScheduleJob>(opts => opts.WithIdentity(jobKey));
                        q.AddTrigger(opts => opts
                            .ForJob(jobKey)
                            .WithIdentity("t_Scheduler")
                            .WithSimpleSchedule(x => x
                                .WithIntervalInSeconds(3600)
                                .RepeatForever()));
                    });
                    services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
                });
    }
}
