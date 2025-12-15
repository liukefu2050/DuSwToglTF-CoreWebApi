using CoreWebApi.Models;
using DuSwToglTF.ExportContext;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.IO.Compression;
using System.Text;
using System.Windows.Controls;
using System.Windows.Shapes;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;
 

namespace CoreWebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };
        private readonly MyDbContext _dbContext;
        private readonly ILogger<WeatherForecastController> _logger;
        private readonly IConfiguration _configuration;
     
        public WeatherForecastController(ILogger<WeatherForecastController> logger, MyDbContext dbContext, IConfiguration configuration)
        {
            _logger = logger;
            _dbContext = dbContext;
            _configuration = configuration;
        }

        /// <summary>
        /// 读取所有
        /// </summary>
        /// <returns></returns>
        [HttpGet("solidword")]
        public IActionResult solidword()
        {
            SldWorks swApp = new SldWorks();
            swApp.Visible = true;

            var orgFilePath = @"E:\3d\input2\DT3Z7.5-160（加动平衡）1.sldprt";

            var ifexst = System.IO.File.Exists(orgFilePath);
            if (!ifexst)
            {
                return Ok("文件找不到");
            }
            var doc = OpenSWDoc(orgFilePath, true, swApp);

            var fileName = System.IO.Path.GetFileNameWithoutExtension(orgFilePath);
           // Console.WriteLine("OpenSWDoc完成" + DateTime.Now.ToString());
            var diretory = _configuration["glbUrl"];
            var aa = System.IO.Path.Combine(diretory, fileName);

            ExporterUtility.ExportData(doc, new PartDocExportContext(aa));
            swApp.ExitApp();
            return Ok("dd");
        }

        //filePath文件路径SldWorks.AssemblyDoc
        //isVisible是否可见
        private static ModelDoc2 OpenSWDoc(string filePath, bool isVisible, ISldWorks app)
        {
            swDocumentTypes_e type = swDocumentTypes_e.swDocNONE;
            string ext = System.IO.Path.GetExtension(filePath).ToUpper().Substring(1);
            if (ext == "SLDASM")
            {
                type = swDocumentTypes_e.swDocASSEMBLY;
            }
            else if (ext == "SLDPRT")
            {
                type = swDocumentTypes_e.swDocPART;
            }
            else if (ext == "SLDDRW")
            {
                type = swDocumentTypes_e.swDocDRAWING;
                
            }
            else
            {
                return null;
            }
            int Errors = 0;
            int Warnings = 0;
            ModelDoc2 modelDoc2 = app.OpenDoc6(filePath, (int)type, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref Errors, ref Warnings);
            //ModelDoc2 modelDoc2 = app.OpenDoc6(filePath, (int)type);

            //swFileLoadWarning_e.swFileLoadWarning_AlreadyOpen;
            //var eee = swFileLoadError_e.swFileNotFoundError;

            return modelDoc2;
        }

        /// <summary>
        /// 创建
        /// </summary>
        /// <returns></returns>
        [HttpGet("Update")]
        public IActionResult Update()
        {
            var message = "";
            using (_dbContext)
            {
                var person = new NetSolidWorkLog
                {
                    id = "Rector",
                    progress = "progr4ess",
                    oss_zip_id = "oss_zpi4_id",
                    fileurl_zip = "file4url_zip",
                    oss_glb_id = "oss_4lb_id",
                    fileurl_glb = "f4ileurl_glb",
                    sw_status = 4,
                    error_code = "error4_code",
                    error_msg = "erro4r_msg",
                    zip_url = "zip4_url",
                    draco_url = "draco_4url",
                    solid_file_url = "sol4id_file_url",
                    glb_url = "glb_u4rl"
                };
                _dbContext.NetSolidWork.UpdateRange(person);
                var i = _dbContext.SaveChanges();
                message = i > 0 ? "数据更新成功" : "数据更新失败";
            }
            return Ok(message);
        }

        /// <summary>
        /// 创建
        /// </summary>
        /// <returns></returns>
        [HttpGet("Create")]
        public IActionResult Create()
        {
            var message = "";
            using (_dbContext)
            {
                var person = new NetSolidWorkLog
                {
                    id = "Rector",
                    progress = "progress",
                    oss_zip_id = "oss_zpi_id",
                    fileurl_zip = "fileurl_zip",
                    oss_glb_id = "oss_glb_id",
                    fileurl_glb = "fileurl_glb",
                    sw_status = 3,
                    error_code = "error_code",
                    error_msg = "error_msg",
                    zip_url = "zip_url",
                    draco_url = "draco_url",
                    solid_file_url = "solid_file_url",
                    glb_url = "glb_url"
                };
                _dbContext.NetSolidWork.Add(person);
                var i = _dbContext.SaveChanges();
                message = i > 0 ? "数据写入成功" : "数据写入失败";
            }
            return Ok(message);
        }

        /// <summary>
        /// 读取指定Id的数据
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetById")]
        public IActionResult GetById(int id)
        {
            using (_dbContext)
            {
                var person = new NetSolidWorkLog
                {
                    sw_status = 3
                };
                var list = _dbContext.NetSolidWork.FromSql($"select * from net_solid_work_log where error_code = 'ng' order by id desc").ToList();
                //var list = _dbContext.NetSolidWork.Find(id);
                return Ok(list);
            }
        }

        /// <summary>
        /// 读取所有
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetAll")]
        public IActionResult GetAll()
        {
            using (_dbContext)
            {
                var list = _dbContext.NetSolidWork.ToList();
                return Ok(list);
            }

        }
        [HttpGet("RunNode")]
        public IActionResult RunNode()
        {

            //IConfiguration configuration = new ConfigurationBuilder().SetBasePath(Environment.CurrentDirectory).AddJsonFile("filesettings.json").Build();

            var glbUrl = _configuration["glbUrl"];
            var gltfCmd = _configuration["gltf-pipeline"]+" ";
            
            string startPath = @"-i " + glbUrl + "SK7420A×750-23-30.glb -o " + glbUrl + "model_4draco.glb -d"; // ZIP文件路径
   
            return Ok(RunNodeCommand.RunNode(gltfCmd, startPath));
        }



        [HttpGet("Configuration")]
        public IActionResult Configuration(int id)
        {
            //IConfiguration configuration = new ConfigurationBuilder().SetBasePath(Environment.CurrentDirectory).AddJsonFile("filesettings.json").Build();

            var downloadUrl = _configuration["downloadUrl"]; //IConfiguration接口自带的索引器，只返回字符串类型。如：名字
            var unzipUrl = _configuration["unzipUrl"];
            var glbUrl = _configuration["glbUrl"];

            return Ok(downloadUrl+ unzipUrl + glbUrl);
        }

        [HttpGet("Zip")]
        public IActionResult Zip(int id)
        {
            string startPath = @"E:\net\net.zip"; // ZIP文件路径
            string extractPath = @"E:\net\extract\"; // 解压目标路径
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding encoding = Encoding.GetEncoding("GBK");
            ZipFile.ExtractToDirectory(startPath, extractPath, encoding);
          
            return Ok("Specific Data");
        }


        [HttpGet("DownLoad")]
        public async Task<string> DownLoad(int id)
        {
            using (var client = new HttpClient()) // WebClient oss.365me.me/mes/2025/11/05/3DZip.zip
            {
                Random random = new Random();
                byte[] bytes = new byte[10]; // 创建一个长度为10的字节数组
                random.NextBytes(bytes); // 用随机数填充这个数组
  
                var fileName = @"E:\net\"+ BitConverter.ToString(bytes) + ".txt";
                var uri = new Uri("https://oss.365me.me/mes/2025/11/05/a2db8178c7ba4138af3dc3957ad22f31.txt");

                var ret = await client.DownloadFileTaskAsync(uri, fileName);

                return ret;
            }

        }

        [HttpGet("UploadUtil")]
        public async Task<string> UploadUtil(int id)
        {

            using (var client = new HttpClient())
            {
                // 文件路径
                string filePath = @"E:\3d\mytestdraco.glb";
                // 文件名（可选，如果不设置，将使用路径的最后部分）
                string fileName = "mytestdraco.glb";

                string ret = await client.UploadFileTaskAsync(@"http://192.168.15.56:8080/tpi/oss/upload",fileName, filePath,"");

                return ret;
            }

        }


        [HttpGet("Upload")]
        public async Task<string> Upload(int id)
        {
            // 目标URL
            string url = "http://192.168.15.56:8080/tpi/oss/upload";
            // 文件路径
            string filePath = @"E:\3d\SK74202.glb";
            // 文件名（可选，如果不设置，将使用路径的最后部分）
            string fileName = "SK74202.glb";

            using (var client = new HttpClient())
            {
                using (var content = new MultipartFormDataContent())
                {
                    // 读取文件内容
                    var fileStream = System.IO.File.OpenRead(filePath);
                    // 添加文件到MultipartFormDataContent中
                    var fileContent = new StreamContent(fileStream);
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJsb2dpblR5cGUiOiJsb2dpbiIsImxvZ2luSWQiOiIxODg4MTUwMDk3ODcyNTEwOTc3Iiwicm5TdHIiOiJjZ3c4a3paemtLcFpBa3RtanRLTmpBcXlKbnBVWmtUTiIsInVzZXJJZCI6IjE4ODgxNTAwOTc4NzI1MTA5NzciLCJ0ZW5hbnRJZCI6MTczMDQ5MDExNzc4NzcwNTM0NH0.j5RzqKPEbVsBj8AiOJRXMKbqQoFZlanSBhoe1uirh3M");
                    fileContent.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("form-data")
                    {
                        Name = "\"file\"", // 注意：这里的引号是为了防止某些服务器对参数名的特殊处理（如PHP）
                        FileName = "\"" + fileName + "\"" // 注意：这里的引号是为了防止某些服务器对文件名的特殊处理（如PHP）
                    };
                    content.Add(fileContent);

                    // 发送POST请求
                    var response = await client.PostAsync(url, content);
                    var re = response.EnsureSuccessStatusCode(); // 确保响应状态码表示成功
                    Console.WriteLine("确保响应状态码表示成功: " + re);
                    string responseBody = await response.Content.ReadAsStringAsync();
                    return responseBody;
                }
            }

        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            //QuartzManager.Init();

            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}
