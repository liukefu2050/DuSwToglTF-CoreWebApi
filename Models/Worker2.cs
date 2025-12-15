using CoreWebApi.Controllers;
using CoreWebApi.Mapper;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace CoreWebApi.Models
{
    public class Worker2 : BackgroundService
    {
        private readonly ILogger<Worker2> _logger;
        private readonly IConfiguration _configuration;
        private readonly string? ASPNETCORE_URLS;

        public Worker2(ILogger<Worker2> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            ASPNETCORE_URLS = _configuration["ASPNETCORE_URLS"];
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            //var builder = new ConfigurationBuilder()
            //.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            //IConfigurationRoot configuration2 = builder.Build();
            if (ASPNETCORE_URLS == null) { 
                return;
            }
            while (!stoppingToken.IsCancellationRequested)
            {

                using (var client = new HttpClient()) // WebClient oss.365me.me/mes/2025/11/05/3DZip.zip
                {
                    _logger.LogInformation("刷新Token:"+ ASPNETCORE_URLS);
                    await client.GetAsync(ASPNETCORE_URLS + "/ThreeDim/AuthorizationFresh");
                }

                await Task.Delay(3600*1000, stoppingToken); // 模拟工作负载

                
            }
        }
    }
}
