using CoreWebApi.Controllers;
using CoreWebApi.Mapper;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Quartz.Impl;

namespace CoreWebApi.Models
{

    public class QuartzManager
    {

        private readonly IConfiguration _configuration;
        public QuartzManager(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async void Init()
        {
            StdSchedulerFactory factory = new StdSchedulerFactory();
            //创建一个Scheduler任务调度容器
            IScheduler scheduler = await factory.GetScheduler();

            //指定具体执行的任务Job
            IJobDetail sendEmailJob = JobBuilder.Create<SendMailJob>()
            .WithIdentity("sendEmailJob", "sendEmailJobGrop")
            .WithDescription("定时发送邮件").Build();

            //设置触发条件为五秒执行一次
            ITrigger sendEmailTrigger = TriggerBuilder.Create()
            .WithIdentity("sendEmailTrigger", "sendEmailJobGrop")
            .WithDescription("QuartZ")
            .WithCronSchedule("3/5 * * * * ?")
            .Build();

            //指定具体执行的任务Job
            IJobDetail freshTokenJob = JobBuilder.Create<FreshTokenJob>()
            .WithIdentity("freshTokenJob", "freshTokenJobGrop")
            .WithDescription("定时刷新MES的Token").Build();

            //设置触发条件为五秒执行一次
            ITrigger freshTokenTrigger = TriggerBuilder.Create()
            .WithIdentity("freshTokenTrigger", "freshTokenJobGrop")
            .WithDescription("QuartZ")
            .WithCronSchedule("5/10 * * * * ?")
            .Build();

            //把策略和任务放入到Scheduler
            await scheduler.ScheduleJob(sendEmailJob, sendEmailTrigger);

            await scheduler.ScheduleJob(freshTokenJob, freshTokenTrigger);

            //执行任务
            await scheduler.Start();
        }
    }



    //增加特性保证任务不会重叠执行
    [DisallowConcurrentExecution]
    public class SendMailJob : IJob
    {
        //Job类
        public async Task Execute(IJobExecutionContext context)
        {
            await Task.Run(() =>
            {
                //doSomthing

                System.Console.WriteLine($"开始发送邮件{System.DateTime.Now}");
            });
        }
    }


    //增加特性保证任务不会重叠执行
    [DisallowConcurrentExecution]
    public class FreshTokenJob : IJob
    {
        //Job类
        public async Task Execute(IJobExecutionContext context)
        {
            await Task.Run(() =>
            {
                //doSomthing
                //_ = await FreshTokenUtil.GetToken(_configuration);

                //MyUsingService ds = new MyUsingService(new MyService());
                //ds.FreshWork();
                System.Console.WriteLine($"定时刷新MES的Token{System.DateTime.Now}");
            });
        }
    }

}
