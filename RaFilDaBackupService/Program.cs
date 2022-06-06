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
        public static IScheduler _sheduler = null;
        public static string API_URL = "https://localhost:5001/";
        public static int ID { get; set; }
        public static string TOKEN = "";

        public static void Main(string[] args)
        {
            while(TOKEN == "")
            {
                var sr = new StreamReader(@"..\token.txt");
                TOKEN = sr.ReadToEnd();
                sr.Close();
                if (TOKEN == "")
                {
                    System.Console.WriteLine("Please insert a token into the token.txt file. You can generate it on the Sessions tab. If you are still having any trouble with adding a new token, please refer to our support at support@rafilda.com");
                    Thread.Sleep(10000);
                    System.Console.Clear();
                }
            }

            System.Console.WriteLine("Token successfuly added.");

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

                        var jobKey = new JobKey("Sheduler");
                        q.AddJob<ScheduleJob>(opts => opts.WithIdentity(jobKey));
                        q.AddTrigger(opts => opts
                            .ForJob(jobKey)
                            .WithIdentity("t_Sheduler")
                            .WithSimpleSchedule(x => x
                                .WithIntervalInSeconds(3600)
                                .RepeatForever()));
                    });
                    services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
                });
    }
}
