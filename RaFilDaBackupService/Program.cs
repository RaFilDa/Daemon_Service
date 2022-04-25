using Microsoft.Extensions.Hosting;
using Quartz;
using RaFilDaBackupService.Entities;
using System.Threading;
using System.Collections.Generic;
using Quartz.Impl;
using System.Threading.Tasks;

namespace RaFilDaBackupService
{
    public class Program
    {
        public static IScheduler _sheduler = null;
        public static string API_URL = "https://localhost:5001/";

        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }
        
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddQuartz(q =>
                    {
                        q.UseMicrosoftDependencyInjectionScopedJobFactory();

                        var JobKey = new JobKey("StartScheduler");
                        q.AddJob<ScheduleJob>(opts => opts.WithIdentity(JobKey));
                        q.AddTrigger(opts => opts
                            .ForJob(JobKey)
                            .WithIdentity("t_StartScheduler")
                            .WithSimpleSchedule(x => x
                                .WithIntervalInSeconds(1)
                                .WithRepeatCount(1)));

                        JobKey = new JobKey("Sheduler");
                        q.AddJob<ScheduleJob>(opts => opts.WithIdentity(JobKey));
                        q.AddTrigger(opts => opts
                            .ForJob(JobKey)
                            .WithIdentity("t_Sheduler")
                            .WithCronSchedule("0 0 * * * ?"));
                    });
                    services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
                });
    }
}
