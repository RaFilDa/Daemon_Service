using Microsoft.Extensions.Hosting;
using Quartz;
using RaFilDaBackupService.Entities;

namespace RaFilDaBackupService
{
    public class Program
    {
        public static HttpTools tools = new HttpTools();

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

                        foreach (ConfigInfo configInfo in tools.GetConfigs())
                        {
                            foreach(Source s in configInfo.Sources)
                            {
                                foreach(Destination d in configInfo.Destinations)
                                {
                                    var JobKey = new JobKey(configInfo.Config.Name + "_" + s.Path + "_" + d.Path);
                                    q.AddJob<BackupJob>(opts => opts.WithIdentity(JobKey)
                                                                    .UsingJobData("jobId", configInfo.Config.Id)
                                                                    .UsingJobData("jobName", configInfo.Config.Name)
                                                                    .UsingJobData("jobType", configInfo.Config.BackupType)
                                                                    .UsingJobData("jobSource", s.Path)
                                                                    .UsingJobData("jobDestination", d.Path)
                                                                    .UsingJobData("jobRetention", configInfo.Config.RetentionSize)
                                                                    .UsingJobData("jobPackages", configInfo.Config.PackageSize)
                                                                    );
                                    q.AddTrigger(opts => opts
                                        .ForJob(JobKey)
                                        .WithIdentity("t_" + configInfo.Config.Name + "_" + s.Path + "_" + d.Path)
                                        .WithCronSchedule(configInfo.Config.Cron));
                                }
                            }                            
                        }
                    });
                    services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
                });
    }
}
