using CoreWebApi.Controllers;
using CoreWebApi.Mapper;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace CoreWebApi.Models
{
    public class Worker1 : BackgroundService
    {
        private readonly ILogger<Worker1> _logger;
        private readonly IConfiguration _configuration;
        private readonly string? ASPNETCORE_URLS;

        public Worker1(ILogger<Worker1> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            ASPNETCORE_URLS = _configuration["ASPNETCORE_URLS"];
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            if (ASPNETCORE_URLS == null) { 
                return;
            }
            while (!stoppingToken.IsCancellationRequested)
            {

                using (var client = new HttpClient()) //
                {
                    //_logger.LogInformation("定时启动挂起的转换:"+ ASPNETCORE_URLS);
                    var response = await client.GetAsync(ASPNETCORE_URLS + "/ThreeDim/QuartzTran");
                    if (response.IsSuccessStatusCode)
                    {
                        var responseBody = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation("定时启动挂起的转换:" + responseBody);
                    }
                    
                }

                await Task.Delay(5*60*1000, stoppingToken); // 模拟工作负载 60秒*5

                
            }
        }
    }
}
