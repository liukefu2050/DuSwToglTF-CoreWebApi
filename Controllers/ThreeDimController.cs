using CoreWebApi.Mapper;
using CoreWebApi.Models;
using CoreWebApi.Req;
using DuSwToglTF.ExportContext;
using Google.Protobuf.Compiler;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mysqlx.Expr;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto;
using SharpCompress.Common;
using SharpCompress.Readers;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections;
using System.Drawing;
using System.IO.Compression;
using System.Net;
using System.Text;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;


namespace CoreWebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ThreeDimController : ControllerBase
    {
        private readonly MyDbContext _dbContext;
        private readonly ILogger<ThreeDimController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IMysqlService _IMysqlService;
        private static Mutex mutex = new Mutex(false, "Global\\MyMutex");
        private static Mutex mutexTran = new Mutex(false, "Global\\MyTran");
        private static bool _tranning = false;//是否有正在转换中的数据

        public ThreeDimController(ILogger<ThreeDimController> logger, MyDbContext dbContext, IConfiguration configuration, IMysqlService IMysqlService)
        {
            _logger = logger;
            _dbContext = dbContext;
            _configuration = configuration;
            _IMysqlService = IMysqlService;
        }

        [HttpPost("Three3d")]
        public IActionResult Three3d([FromBody] ReqThreeDimInsert product)
        {
            _logger.LogInformation($"收到任务{product.id}");
            R r = CreateNewTask(product);
            if(r.status == SYSTEM_CONST.SYSTEM_CONST_FAIL_STATUS)
            {
                //失败或者重复
                _logger.LogInformation($"创建任务失败或者重复{product.id}");
                return Ok(r);
            }
            _logger.LogInformation($"开始转换{product.id}");
            _=DoMainTran(product);

            return Ok(r);

        }

        [HttpGet("AuthorizationFresh")]
        public async Task<string> AuthorizationFresh()
        {
            string ret = await AuthorizationCheck();
            return ret;

        }

        [HttpGet("QuartzTran")]
        public IActionResult QuartzTran()
        {

            if (_tranning)
            {
                return Ok("排队中");
            }
            var tpiHost = _configuration["tpiHost"]+"%";

            List<NetSolidWorkLog> list = _dbContext.NetSolidWork.FromSql($"select * from net_solid_work_log where sw_status = {(int)SwStatus.SolidWork} and error_code = 'Waiting' and fileurl_zip like {tpiHost} ").ToList();

            if (list != null && list.Count > 0)
            {
                NetSolidWorkLog dssd = list[0];

                ReqThreeDimInsert p = new ReqThreeDimInsert();
                p.id = dssd.id;
                p.oss_zip_id = dssd.oss_zip_id;
                p.fileurl_zip = dssd.fileurl_zip;

                _=DoMainTran(p);

                return Ok(dssd);
            }
            else
            {
                return Ok("没数据");
            }

        }

        [HttpGet("Again")]
        public IActionResult Again(string id)
        {
            NetSolidWorkLog? ret = SelectById(id); ;
            if (ret != null)
            {
                ReqThreeDimInsert p = new ReqThreeDimInsert();
                p.id = id;
                p.oss_zip_id = ret.oss_zip_id;
                p.fileurl_zip = ret.fileurl_zip;

                _ = DoMainTran(p);

                return Ok(ret);
            }
            else
            {
                return Ok("没数据");
            }

        }

        private async Task<bool> DoMainTran(ReqThreeDimInsert product)
        {
            bool ret = true;

            int downRet = CheckDownLoad(product);
            if (downRet == 9)
            {
                await To365MeNG(product, "同步下载后失败");
                return false;
            }
            else if (downRet == 0)
            {
                long fileSize = await CheckFileSize(product);

                if (fileSize < 100)
                {
                    //小于0.1K,文件有问题，是空文件
                    _logger.LogInformation($"文件有问题,小于0.1K,是空文件 fileSize= {fileSize}");
                    await To365MeNG(product, "同步下载后失败");
                    return false;

                }
                if (fileSize <= SYSTEM_CONST.SYSTEM_CONST_FILE_CHUNK_SIZE)
                {
                    //小于10M,普通下载即可
                    ret = await DoDownLoad(product);
                    _logger.LogInformation($"普通下载返回值{ret}");
                }
                else
                {
                    //大于10M,分片下载即可
                    ret = await DoRangeDownLoad(product, fileSize);
                    _logger.LogInformation($"分片下载返回值{ret}");

                    //分片下载不用等待 
                }

                if (!ret)
                {
                    await To365MeNG(product, "同步下载后失败");
                    return false;
                }
                else
                {
                    await To365MeStatus(product, "同步下载后成功");
                }
            }
            else if (downRet == 1)
            {
                await To365MeStatus(product, "同步下载后成功");
            }


            //解压
            ret = UnZip(product);
            _logger.LogInformation($"UnZip解压返回值{ret}");
            if (!ret)
            {
                await To365MeNG(product, "同步解压后失败");
                return false;
            }
            else
            {
                await To365MeStatus(product, "同步解压后成功");
            }

            //转换：正式开始
            var retInt = Solidword(product);
            _logger.LogInformation($"Solidword转换返回值{retInt}");
            if (retInt == -1)
            {
                await To365MeNG(product, "同步Solidword后失败");
                return false;
            }
            else if(retInt == 0)
            {
                await To365MeStatus(product, "同步Solidword后成功");
            }
            else if (retInt == 9)
            {
                //转换：挂起
                return true;
            }

            //压缩
            ret = RunNode(product);
            _logger.LogInformation($"压缩返回值{ret}");
            if (!ret)
            {
                await To365MeNG(product, "同步压缩后失败");
                return false;
            }
            else
            {
                //await To365MeStatus(product, "压缩后成功");
            }

            //再用Zip压缩
            ret = Zip(product);
            if (!ret)
            {
                await To365MeNG(product, "Zip再压缩后失败");
                return false;
            }

            //上传
            ret = await Upload(product);
            if (!ret)
            {
                await To365MeNG(product, "上传后失败");
                return false;
            }

            //同步365Me
            ret = await To365MeOK(product);
            if (!ret)
            {
                return false;
            }

            return true;
        }

        private async Task<bool> To365MeNG(ReqThreeDimInsert product,string type)
        {
            _logger.LogError($"To365MeNG：{product.id}--{type}");
            if (product == null || product.id == null)
            {
                return false;
            }

            NetSolidWorkLog? ids = SelectById(product.id);
            if (ids == null)
            {
                return false;
            }
            if (!ids.error_code.Equals("NG"))
            {
                //必须是已经失败的
                return true;
            }
            if (ids.create_id.Equals("NG") || ids.create_id.Equals("OK"))
            {
                //必须是已经失败的
                return true;
            }

            try
            {
                var uri = _configuration["tpiHost"] + "/api/nocode/data/edit";
                EditMes postObj = new EditMes();
                postObj.id = ids.id;
                postObj.fmId = _configuration["fmId"];
                Hashtable sd = new Hashtable();
                sd.Add("id", ids.id);
                sd.Add("status_id", "threeDimStatus.ng");
                sd.Add("status_name", "建模失败");
                sd.Add("error_msg", ids.error_msg);
                sd.Add("progress", ids.progress);
                postObj.data = sd;

                using (var client = new HttpClient()) // WebClient oss.365me.me/mes/2025/11/05/3DZip.zip
                {
                    string token = await AuthorizationGet();
                    var postStr = JsonConvert.SerializeObject(postObj);
                    _logger.LogError($"To365MeNG：{postStr}");
                    var ret1 = await client.MyPostAsync(uri, postStr, token);
                    _logger.LogError($"To365MeNG：{ret1}");
                    R? retMe = JsonConvert.DeserializeObject<R>(ret1);
                    if (retMe == null || retMe.status != 0)
                    {
                        ids.create_id = "NG";
                    }
                    else
                    {
                        ids.create_id = "OK";
                    }

                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"To365MeNG：{ex.Message}");
                ids.create_id = "NG";
            }

            updateStatus(ids, ids.sw_status, ids.error_code, ids.error_msg, ids.progress);

            return true;


        }


        private async Task<bool> To365MeStatus(ReqThreeDimInsert product,string type)
        {
            _logger.LogInformation($"To365MeStatus：{product.id}--{type}");
            if (product == null || product.id == null)
            {
                return false;
            }

            NetSolidWorkLog? ids = SelectById(product.id);
            if (ids == null)
            {
                return false;
            }

            try
            {
                var uri = _configuration["tpiHost"] + "/api/nocode/data/edit";
                EditMes postObj = new EditMes();
                postObj.id = ids.id;
                postObj.fmId = _configuration["fmId"];
                Hashtable sd = new Hashtable();
                sd.Add("id", ids.id);

                sd.Add("progress", ids.progress);
                postObj.data = sd;
                
                using (var client = new HttpClient()) // WebClient oss.365me.me/mes/2025/11/05/3DZip.zip
                {
                    string? token = await AuthorizationGet();
                    var postStr = JsonConvert.SerializeObject(postObj);
                    _logger.LogWarning($"To365MeStatus：{postStr}");
                    var ret1 = await client.MyPostAsync(uri, postStr, token);
                    _logger.LogWarning($"To365MeStatus：{ret1}");

                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"To365MeStatus：{ex.Message}");
            }

            return true;


        }

        private async Task<bool> To365MeOK(ReqThreeDimInsert product)
        {
            _logger.LogInformation($"To365MeOK开始：{product.id}--最后成功");

            int swStatusStep = (int)SwStatus.Complete;
            if (product == null || product.id == null)
            {
                return false;
            }

            NetSolidWorkLog? ids = SelectById(product.id);
            if (ids == null)
            {
                return false;
            }
            if (!(ids.sw_status == (int)SwStatus.Upload && ids.error_code.Equals("OK")))
            {
                //必须是已经上传成功的
                return true;
            }

            var errorMsg = "同步出错：";
      
            try
            {
                var uri = _configuration["tpiHost"] + "/api/nocode/data/edit";
                EditMes postObj = new EditMes();
                postObj.id = ids.id;
                postObj.fmId = _configuration["fmId"]; 
                Hashtable sd = new Hashtable();
                sd.Add("oss_glb_id", ids.oss_glb_id);
                sd.Add("fileurl_glb", ids.fileurl_glb);
                sd.Add("id", ids.id);
                sd.Add("status_id", "threeDimStatus.ok");
                sd.Add("status_name", "建模完成");
                sd.Add("error_msg", "完成");
                sd.Add("progress", "100");
                postObj.data = sd;

                using (var client = new HttpClient()) // WebClient oss.365me.me/mes/2025/11/05/3DZip.zip
                {
                    string token = await AuthorizationGet();
                    var postStr = JsonConvert.SerializeObject(postObj);
                    _logger.LogWarning($"To365MeOK 请求：{postStr}");
                    var ret1 = await client.MyPostAsync(uri, postStr, token);
                    _logger.LogWarning($"To365MeOK 响应：{ret1}");
                    R? retMe = JsonConvert.DeserializeObject<R>(ret1);
                    if (retMe == null || retMe.status != 0)
                    {
                        updateStatus(ids, swStatusStep, "NG", ret1, "100");
                        return false;
                    }
                    else
                    {
                        ids.create_id = "OK";
                        updateStatus(ids, swStatusStep, "OK", ret1, "100");
                    }

                }

            }
            catch (Exception ex)
            {
                errorMsg = errorMsg + ex.Message;
                updateStatus(ids, swStatusStep, "NG", errorMsg, "100");
                _logger.LogError($"To365MeOK 异常：{ex.Message}");
                return false;
            }

            return true;


        }


        private async Task<bool> Upload(ReqThreeDimInsert product)
        {
            _logger.LogInformation($"Upload 开始：{product.id}");

            int swStatusStep = (int)SwStatus.Upload;
            if (product == null || product.id == null)
            {
                return false;
            }

            NetSolidWorkLog? ids = SelectById(product.id);
            if (ids == null)
            {
                return false;
            }
            if (ids.sw_status > swStatusStep || (ids.sw_status == swStatusStep && ids.error_code.Equals("OK")))
            {
                //已经成功的就不需要，直接下一步
                return true;
            }

            var errorMsg = "上传出错："; 
            var fileName = ""; 
            var orgFilePath = ids.draco_zip_url;

            var ifexst = System.IO.File.Exists(orgFilePath);
            if (!ifexst)
            {
                errorMsg = "上传出错：找不到压缩后的glb.zip文件";
                updateStatus(ids, swStatusStep, "NG", errorMsg, "90");
                return false;
            }

            fileName = System.IO.Path.GetFileName(orgFilePath);

            try
            {
  
                var uri = _configuration["tpiHost"]+ "/api/tpi/oss/upload";


                using (var client = new HttpClient()) // WebClient oss.365me.me/mes/2025/11/05/3DZip.zip
                {
                    string token = await AuthorizationGet();

                    var jsonStr = await client.UploadFileTaskAsync(uri, fileName, orgFilePath, token);

                    R? ret = JsonConvert.DeserializeObject<R>(jsonStr);
                    if (ret == null || ret.status != 0 )
                    {
                        updateStatus(ids, swStatusStep, "NG", jsonStr, "100");
                        return false;
                    }

                    string dataStr = JsonConvert.SerializeObject(ret.data);
                    Oss? oss = JsonConvert.DeserializeObject<Oss>(dataStr);
                    if (oss != null && !oss.ossId.Equals(""))
                    {
                        ids.oss_glb_id = oss.ossId;
                        ids.fileurl_glb = oss.url;
                    }
                    updateStatus(ids, swStatusStep, "OK", jsonStr, "100");

                }

            }
            catch (Exception ex)
            {
                errorMsg = errorMsg + ex.Message;
                updateStatus(ids, swStatusStep, "NG", errorMsg, "90");

                _logger.LogError($"Upload 异常：{ex.Message}");
                return false;
            } 

            return true;


        }

        private bool RunNode(ReqThreeDimInsert product)
        {

            _logger.LogInformation($"压缩 开始：{product.id}");

            int swStatusStep = (int)SwStatus.Draco;
            if (product == null || product.id == null)
            {
                return false;
            }

            NetSolidWorkLog? ids = SelectById(product.id);
            if (ids == null)
            {
                return false;
            }

            if (ids.sw_status > swStatusStep || (ids.sw_status == swStatusStep && ids.error_code.Equals("OK")))
            {
                return true;
            }
            var errorMsg = "";
            var outFileName = "";

            var orgFilePath = ids.glb_url;

            try
            {

                var ifexst = System.IO.File.Exists(orgFilePath);
                if (!ifexst)
                {
                    errorMsg = "压缩出错：找不到文件glb文件";
                    updateStatus(ids, swStatusStep, "NG", errorMsg, "90");
                    return false;
                }

                var outPath = _configuration["glbUrl"] + ids.id;
                if (Directory.Exists(outPath))
                {
                    Directory.Delete(outPath, true);
                }
                Directory.CreateDirectory(outPath);

                outFileName = outPath + "\\" + Path.GetFileNameWithoutExtension(orgFilePath) + "_draco.glb";

                var gltfCmd = _configuration["gltf-pipeline"] + " ";

                string startPath = @"-i " + orgFilePath + " -o " + outFileName + " -d"; // ZIP文件路径

                if (System.IO.File.Exists(outFileName))
                {
                    System.IO.File.Delete(outFileName);
                }

                var ret = RunNodeCommand.RunNode(gltfCmd, startPath);

                if (System.IO.File.Exists(outFileName))
                {
                    ids.draco_url = outFileName;
                    updateStatus(ids, swStatusStep, "OK", "", "90");
                    return true;
                }
                else
                {
                    updateStatus(ids, swStatusStep, "NG", errorMsg, "80");
                    return false;
                }

            }
            catch (Exception ex)
            {
                errorMsg = "压缩出错：" + ex.Message;
                updateStatus(ids, swStatusStep, "NG", errorMsg, "80");
                _logger.LogError($"RunNode 异常：{ex.StackTrace}");
                return false;
            }

        }

        private bool Zip(ReqThreeDimInsert product)
        {

            _logger.LogInformation($"再Zip压缩 开始：{product.id}");

            int swStatusStep = (int)SwStatus.Zip;
            if (product == null || product.id == null)
            {
                return false;
            }

            NetSolidWorkLog? ids = SelectById(product.id);
            if (ids == null)
            {
                return false;
            }

            if (ids.sw_status > swStatusStep || (ids.sw_status == swStatusStep && ids.error_code.Equals("OK")))
            {
                return true;
            }
            var errorMsg = "";
            var outFileName = "";

            var orgFilePath = ids.draco_url;

            try
            {

                var ifexst = System.IO.File.Exists(orgFilePath);
                if (!ifexst)
                {
                    errorMsg = "Zip再压缩出错：找不到文件draco_glb文件";
                    updateStatus(ids, swStatusStep, "NG", errorMsg, "90");
                    return false;
                }

                outFileName = _configuration["glbUrl"] + System.IO.Path.GetFileNameWithoutExtension(orgFilePath) + ".zip";

                var outPathDirectory = _configuration["glbUrl"] + ids.id;

                ZipFile.CreateFromDirectory(outPathDirectory, outFileName, CompressionLevel.Optimal, false);
 

                if (System.IO.File.Exists(outFileName))
                {
                    ids.draco_zip_url = outFileName;
                    updateStatus(ids, swStatusStep, "OK", "", "90");
                    return true;
                }
                else
                {
                    updateStatus(ids, swStatusStep, "NG", errorMsg, "80");
                    return false;
                }

            }
            catch (Exception ex)
            {
                errorMsg = "压缩出错：" + ex.Message;
                updateStatus(ids, swStatusStep, "NG", errorMsg, "80");
                _logger.LogError($"RunNode 异常：{ex.StackTrace}");
                return false;
            }

        }
        //判断是否有其他正在转换的任务
        private bool SolidwordBefore()
        {
            try
            {
                mutexTran.WaitOne(); // 请求锁

                if (_tranning)
                {
                    return false;
                }
                _tranning = true;
                 
                return true;
            }
            finally
            {
                if (mutexTran != null)
                {
                    mutexTran.ReleaseMutex(); // 释放锁
                }
            }
        }

        private int Solidword(ReqThreeDimInsert product)
        {

            _logger.LogInformation($"Solidword 开始：{product.id}");

            int swStatusStep = (int)SwStatus.SolidWork;
            if (product == null || product.id == null)
            {
                return -1;
            }

            NetSolidWorkLog? ids = SelectById(product.id);
            if (ids == null)
            {
                return -1;
            }

            if (ids.sw_status > swStatusStep || (ids.sw_status == swStatusStep && ids.error_code.Equals("OK")))
            {
                return 0;
            }
            var errorMsg = "";
            var fileName = "";
            var outFileName = "";

            var orgFilePath = ids.solid_file_url;

            var ifexst = System.IO.File.Exists(orgFilePath);
            if (!ifexst)
            {
                errorMsg = "转换出错：找不到文件Solidword文件[没有SLDASM或者SLDPRT文件]";
                updateStatus(ids, swStatusStep, "NG", errorMsg, "40");
                return -1;
            }
            //正式转换前需要判断一下是否有正在执行中的转换，因为Solidword只能开一个
            var checkRet = SolidwordBefore();
            if (!checkRet)
            {
                //有其他Solidword转换正在进行中,本任务挂起。等定时任务再次开启
                errorMsg = "有其他Solidword转换正在进行中，等待中";
                updateStatus(ids, swStatusStep, "Waiting", errorMsg, "20");
                return 9;
            }

            SldWorks swApp = null;
            try
            {
                swApp = new SldWorks();
                swApp.CommandInProgress = true;
                swApp.Visible = true;

                _logger.LogInformation($"Solidword：打开文档开始 {orgFilePath}");
                var doc = OpenSWDoc(orgFilePath, true, swApp);
                if (doc == null)
                {
                    _logger.LogInformation($"Solidword：打开文档结束,错误 文档为null {orgFilePath}");
                    errorMsg = "转换出错：打开文档错误";

                    updateStatus(ids, swStatusStep, "NG", errorMsg, "40");
                    return -1;
                }

                //fileName = System.IO.Path.GetFileNameWithoutExtension(orgFilePath);
                fileName = ids.id;
                var diretory = _configuration["glbUrl"];

                outFileName = Path.Combine(diretory, fileName);

                if (System.IO.File.Exists(outFileName + ".glb"))
                {
                    System.IO.File.Delete(outFileName + ".glb");
                }
                _logger.LogInformation($"Solidword：转换开始 {outFileName}");
                ExporterUtility.ExportData(doc, new PartDocExportContext(outFileName));
                _logger.LogInformation($"Solidword：转换结束 {outFileName}");
                swApp.ExitApp();

                if (System.IO.File.Exists(outFileName+".glb"))
                {
                    ids.glb_url = outFileName + ".glb";
                    updateStatus(ids, swStatusStep, "OK", "", "90");
                    return 0;
                }
                else
                {
                    updateStatus(ids, swStatusStep, "NG", errorMsg, "40");
                    return -1;
                }


            }
            catch (Exception ex)
            {
                errorMsg = "转换出错：" + ex.Message;
                updateStatus(ids, swStatusStep, "NG", errorMsg, "40");
                _logger.LogError($"Solidword 异常：{ex.StackTrace}");
                return -1;

            }
            finally
            {
                if(swApp != null)
                {
                    swApp.ExitApp();
                }

                //释放
                _tranning = false;


            }
        }

        private NetSolidWorkLog? SelectById(string id)
        {

            DbContextOptionsBuilder<MyDbContext> optionsBuilder = new DbContextOptionsBuilder<MyDbContext>();

            optionsBuilder.UseMySql(_configuration.GetConnectionString("MySQL"), MySqlServerVersion.LatestSupportedServerVersion);

            using (var _db = new MyDbContext(optionsBuilder.Options))
            {
                NetSolidWorkLog? d = _db.NetSolidWork.Find(id);
                //var dds = JsonConvert.SerializeObject(d);
                //_logger.LogInformation($"查了一下数据库: {dds}");
                return d;
            }

        }

        //查询等待中的挂起的任务
 

        private void updateStatus(NetSolidWorkLog ids ,int sw_status,string error_code,string error_msg,string progress)
        {

            DbContextOptionsBuilder<MyDbContext> optionsBuilder = new DbContextOptionsBuilder<MyDbContext>();

            optionsBuilder.UseMySql(_configuration.GetConnectionString("MySQL"), MySqlServerVersion.LatestSupportedServerVersion);

            using (var _db = new MyDbContext(optionsBuilder.Options))
            {
                ids.sw_status = sw_status;
                ids.error_code = error_code;
                ids.error_msg = error_msg;
                ids.progress = progress;//进度10%

                _db.NetSolidWork.UpdateRange(ids);
                _db.SaveChanges();
                var dds = JsonConvert.SerializeObject(ids);
                _logger.LogInformation($"更新数据库状态: {sw_status}：{error_code}：{progress}%");
            }

        }

        //filePath文件路径SldWorks.AssemblyDoc
        //isVisible是否可见
        private ModelDoc2 OpenSWDoc(string filePath, bool isVisible, ISldWorks app)
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

        /**
        * 分段下载大文件
        */
        private async Task<long> CheckFileSize(ReqThreeDimInsert product)
        {
            _logger.LogInformation($"CheckFileSize 开始：{product.oss_zip_id}");

            var errorMsg = "";
            try
            {

                var uriGetSize = _configuration["tpiHost"] + "/api/tpi/oss/listByIds/" + product.oss_zip_id;

                using (var client = new HttpClient()) // WebClient oss.365me.me/mes/2025/11/05/3DZip.zip
                {
                    string token = await AuthorizationGet();

                    var ret1 = await client.MyGetAsync(uriGetSize, token);

                    R? retMe = JsonConvert.DeserializeObject<R>(ret1);
                    if (retMe == null || retMe.status != 0)
                    {
                        _logger.LogError($"CheckFileSize：{ret1}");
                        errorMsg = "取不到文件：" + uriGetSize;
                        return -1;
                    }
                    else
                    {
                        string dataStr = JsonConvert.SerializeObject(retMe.data);
                        List<OssMes>? ossMes = JsonConvert.DeserializeObject<List<OssMes>>(dataStr);

                        if (ossMes == null || ossMes.Count == 0)
                        {
                            errorMsg = "文件大小是0KB，不处理。";
                            return 0;
                        }
                        else
                        {
                            long s = ossMes[0].fileSize;
                            if (s < 100)
                            {
                                errorMsg = "文件小于100KB，不处理。";
                            }

                            return s;
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;

                _logger.LogError($"CheckFileSize异常: {errorMsg}");
                return -1;
            }
            finally
            {
                if (!errorMsg.Equals(""))
                {
                    NetSolidWorkLog? ids = SelectById(product.id);
                    updateStatus(ids, (int)SwStatus.Download, "NG", errorMsg, "1");
                }
                
            }

        }


        /**
         * 分段下载大文件
         */
        private async Task<bool> DoRangeDownLoad(ReqThreeDimInsert product,long fileSize)
        {
            _logger.LogInformation($"DoRangeDownLoad 开始：{product.id} -- {fileSize}");

            int swStatus = (int)SwStatus.Org;

            NetSolidWorkLog? ids = SelectById(product.id);
            if (ids == null)
            {
                return false;
            }

            //long SizePer = 1024 * 1024 * 10;//每片10M 1991415637966487552
            double ttt = (double)fileSize / SYSTEM_CONST.SYSTEM_CONST_FILE_CHUNK_SIZE; // 3.16
            double loop = Math.Ceiling(ttt);

            var errorMsg = "";
            var extension = "";
            var fileName = "";
            var downUrl = "";
            var tempDir = "";
            try
            {
                extension = Path.GetExtension(ids.fileurl_zip);//后缀

                fileName = _configuration["downloadUrl"] + ids.id + extension;//最后的文件名

                tempDir = Path.Combine(_configuration["downloadUrl"], ids.id);

                if (System.IO.File.Exists(fileName))
                {
                    System.IO.File.Delete(fileName);
                }

                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                var ossId = ids.oss_zip_id;
                downUrl = ids.fileurl_zip;

                string uri = _configuration["tpiHost"] + "/api/tpi/ossBig/downloadRange/" + ossId;
                 
                using (var client = new HttpClient()) // WebClient oss.365me.me/mes/2025/11/05/3DZip.zip
                {

                    string token = await AuthorizationGet();
                    client.DefaultRequestHeaders.Add("Authorization", token);

                    for (int i = 0; i < loop; i++)
                    {
                        long start = i * SYSTEM_CONST.SYSTEM_CONST_FILE_CHUNK_SIZE;
                        long end = (i + 1) * SYSTEM_CONST.SYSTEM_CONST_FILE_CHUNK_SIZE - 1;
                        if (end > (fileSize - 1))
                        {
                            end = fileSize - 1;

                        }
                        _logger.LogInformation($"DoRangeDownLoad下标: {start}-{end}");
                        var uriTemp = uri + "?offset="+ start + "&size="+ SYSTEM_CONST.SYSTEM_CONST_FILE_CHUNK_SIZE;
                        string pdLeft = i.ToString().PadLeft(5, '0'); 
                        var fileTempName = tempDir + "\\chunk_" + pdLeft + ".dat";
                        if (System.IO.File.Exists(fileTempName))
                        {
                            continue;
                        }

                        errorMsg = await client.DownloadFileTaskAsync(new Uri(uriTemp), fileTempName);
                    }
                }

                MergeFilesUtil.MergeFiles(tempDir,fileName);

                if (System.IO.File.Exists(fileName))
                {
                    //Directory.Delete(tempDir,true);
                }
                else
                {
                    return false;
                }

            }
            catch (Exception ex)
            {
                errorMsg = errorMsg + ex.Message;
                 
                return false;
            }

            return updateAfTerDownLoadSuccess(ids, fileName, errorMsg);

        }

        //0需要下载 1已经下完了 9异常
        private int CheckDownLoad(ReqThreeDimInsert product)
        {
            _logger.LogInformation($"CheckDownLoad 开始：{product.id}");
            int swStatus = (int)SwStatus.Download;
            if (product == null || product.id == null)
            {
                return 9;
            }

            NetSolidWorkLog? ids = SelectById(product.id);
            if (ids == null)
            {
                return 9;
            }
            if (ids.sw_status > swStatus || (ids.sw_status == swStatus && ids.error_code.Equals("OK")))
            {
                //已经下载成功的就不需要下载，直接下一步
                return 1;
            }

            return 0;
        }
        private async Task<bool> DoDownLoad(ReqThreeDimInsert product)
        {
            _logger.LogInformation($"DoDownLoad 开始：{product.id}");

            NetSolidWorkLog? ids = SelectById(product.id);
            if (ids == null)
            {
                return false;
            }

            var errorMsg="";
            var extension = "";
            var fileName = "";
            var downUrl = "";

            try
            {
                extension = Path.GetExtension(ids.fileurl_zip);

                fileName = _configuration["downloadUrl"] + ids.id + extension;

                if (System.IO.File.Exists(fileName))
                {
                    System.IO.File.Delete(fileName);
                }

                downUrl =  ids.fileurl_zip;
                var uri = new Uri(downUrl);
 

                using (var client = new HttpClient()) // WebClient oss.365me.me/mes/2025/11/05/3DZip.zip
                {
                    string token = await AuthorizationGet();
                    client.DefaultRequestHeaders.Add("Authorization", token);

                    errorMsg = await client.DownloadFileTaskAsync(uri, fileName);
                }

            }
            catch (Exception ex)
            {
                errorMsg = errorMsg + ex.Message;

                updateStatus(ids, (int)SwStatus.Org, "NG", errorMsg, "10");

                return false;
            }

            return updateAfTerDownLoadSuccess(ids, fileName, errorMsg);

        }
        private bool updateAfTerDownLoadSuccess(NetSolidWorkLog? ids,string fileName, string errorMsg)
        {
            DbContextOptionsBuilder<MyDbContext> optionsBuilder = new DbContextOptionsBuilder<MyDbContext>();

            optionsBuilder.UseMySql(_configuration.GetConnectionString("MySQL"), MySqlServerVersion.LatestSupportedServerVersion);

            using (var _db = new MyDbContext(optionsBuilder.Options))
            {
                if (System.IO.File.Exists(fileName))
                {

                    _logger.LogInformation($"DoDownLoad下载成功：{fileName}");
                    ids.sw_status = (int)SwStatus.Download;
                    ids.error_code = "OK";
                    ids.error_msg = errorMsg;
                    ids.zip_url = fileName;
                    ids.progress = "10";//进度10%

                }
                else
                {
                    _logger.LogInformation($"DoDownLoad下载失败：{fileName}");
                    ids.sw_status = (int)SwStatus.Download;
                    ids.error_code = "NG";
                    ids.zip_url = "";
                    ids.error_msg = errorMsg;
                    ids.progress = "5";//进度5%
                }

                _db.NetSolidWork.UpdateRange(ids);
                var i = _db.SaveChanges();
            }
            return true;
        }


        private bool UnZip(ReqThreeDimInsert product)
        {
            _logger.LogInformation($"UnZip 开始：{product.id}");

            int swStatusStep = (int)SwStatus.Unzip;
            if (product == null || product.id == null)
            {
                return false;
            }

            NetSolidWorkLog? ids = SelectById(product.id);
            if (ids == null)
            {
                return false;
            }

            if (ids.sw_status > swStatusStep || (ids.sw_status == swStatusStep && ids.error_code.Equals("OK")))
            {
                return true;
            }
            var errorMsg = "";
            var fileName = "";

            try
            {
                string startPath = ids.zip_url; // ZIP文件路径
                string extractPath = _configuration["unzipUrl"] + ids.id; // 解压目标路径
                bool exist = System.IO.Path.Exists(extractPath);
                if (exist)
                {
                    Directory.Delete(extractPath, true);
                }

                Directory.CreateDirectory(extractPath);
                string extensionZip = System.IO.Path.GetExtension(startPath).ToUpper();
                if (".ZIP".Equals(extensionZip))
                {
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    Encoding encoding = Encoding.GetEncoding("GBK");
                    ZipFile.ExtractToDirectory(startPath, extractPath, encoding);
                }
                else if (".RAR".Equals(extensionZip))
                {
                    using (Stream stream = System.IO.File.OpenRead(startPath))
                    {
                        var reader = ReaderFactory.Open(stream);
                        while (reader.MoveToNextEntry())
                        {
                            if (!reader.Entry.IsDirectory)
                            {
                                Console.WriteLine(reader.Entry.Key);
                                reader.WriteEntryToDirectory(extractPath, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                            }
                        }
                    }
                }
                else
                {
                    errorMsg = "这不是zip或者rar的压缩包";
                    return false;
                }


                string[] files = Directory.GetFiles(extractPath, "*.*", SearchOption.AllDirectories);
                long fileSize = 0;
                bool existSolidWorkFile = false;
                foreach (string file in files)
                {
                    string extension = System.IO.Path.GetExtension(file);

                    if (extension.ToUpper().Equals(".SLDPRT") || extension.ToUpper().Equals(".SLDASM"))
                    {

                        FileInfo fileInfo = new FileInfo(file);
                        long fleng = fileInfo.Length;

                        if (fileSize < fleng)
                        {
                            fileSize = fleng;
                            fileName = System.IO.Path.GetFullPath(file);
                        }
                        existSolidWorkFile = true;
                    }
                }

                if (!existSolidWorkFile)
                {
                    errorMsg = "压缩包里没有SLDASM或者SLDPRT文件";
                    return false;
                }

            }
            catch (Exception ex)
            {
                errorMsg += ex.Message;
                _logger.LogError($"UnZip 异常：{ex.StackTrace}");
                return false;
            }
            finally
            {

                DbContextOptionsBuilder<MyDbContext> optionsBuilder = new DbContextOptionsBuilder<MyDbContext>();

                optionsBuilder.UseMySql(_configuration.GetConnectionString("MySQL"), MySqlServerVersion.LatestSupportedServerVersion);

                using (var _db = new MyDbContext(optionsBuilder.Options))
                {
                    if (System.IO.File.Exists(fileName))
                    {
                        _logger.LogInformation($"UnZip解压成功：{fileName}");
                        ids.sw_status = swStatusStep;
                        ids.error_code = "OK";
                        ids.error_msg = errorMsg;
                        ids.solid_file_url = fileName;
                        ids.progress = "20";//进度10%
                    }
                    else
                    {
                        _logger.LogInformation($"UnZip解压失败：压缩包里没有SLDASM或者SLDPRT文件");
                        ids.sw_status = swStatusStep;
                        ids.error_code = "NG";
                        ids.solid_file_url = "";
                        ids.error_msg = errorMsg;
                    }

                    _db.NetSolidWork.UpdateRange(ids);
                    var i = _db.SaveChanges();
                }

            }

            return true;


        }

        private R CreateNewTask(ReqThreeDimInsert product)
        {

            try
            {
                mutex.WaitOne(); // 请求锁

                var r = new R();

                NetSolidWorkLog? list = SelectById(product.id);
                if (list != null)
                {
                    r.status = SYSTEM_CONST.SYSTEM_CONST_FAIL_STATUS;
                    r.code = SYSTEM_CONST.SYSTEM_CONST_FAIL;
                    r.msg = "主键重复";
                    return r;
                }

                var person = new NetSolidWorkLog
                {
                    id = product.id,
                    progress = "0",
                    oss_zip_id = product.oss_zip_id,
                    fileurl_zip = _configuration["tpiHost"] + product.fileurl_zip,
                    oss_glb_id = "",
                    fileurl_glb = "",
                    sw_status = (int)SwStatus.Org,
                    error_code = "",
                    error_msg = "",
                    zip_url = "",
                    draco_url = "",
                    solid_file_url = "",
                    glb_url = "",
                    create_id = "",
                    draco_zip_url = "",
                };

                bool c = _IMysqlService.CreateSolidWorkLog(person);

                if (c)
                {
                    r.status = SYSTEM_CONST.SYSTEM_CONST_SUCCESS_STATUS;
                    r.code = SYSTEM_CONST.SYSTEM_CONST_SUCCESS;
                    r.msg = SYSTEM_CONST.SYSTEM_CONST_SUCCESS_MSG;
                    r.data = person; //JsonConvert.SerializeObject(person);
                }
                else
                {
                    r.status = SYSTEM_CONST.SYSTEM_CONST_FAIL_STATUS;
                    r.code = SYSTEM_CONST.SYSTEM_CONST_FAIL;
                    r.msg = SYSTEM_CONST.SYSTEM_CONST_FAIL_MSG;
                    r.data = person;//JsonConvert.SerializeObject(person);
                }

                return r;
            }
            finally
            {
                if (mutex != null)
                {
                    mutex.ReleaseMutex(); // 释放锁
                }
            }
        }

        private async Task<string> AuthorizationGet()
        {

            NetSolidWorkLog? token = SelectById(_configuration["AuthorizationEnv"]);
            if (token == null)
            {
                return await GetToken();
            }
            else
            {
                return token.error_msg;
            }

        }

        private async Task<string> AuthorizationCheck()
        {

            try
            {
                var uri = _configuration["tpiHost"] + "/api/system/tt/test1";

                using (var client = new HttpClient()) // WebClient oss.365me.me/mes/2025/11/05/3DZip.zip
                {

                    NetSolidWorkLog? token = SelectById(_configuration["AuthorizationEnv"]);
                    if (token == null)
                    {
                        return await GetToken();
                    }
                    else
                    {
                        string exp = token.error_code;
                        string n = DateTime.Now.ToString("yyyy-MM-dd HH:00:00");
                        if (String.IsNullOrWhiteSpace(exp) || n.CompareTo(exp)>0)
                        {
                            return await GetToken();
                        }
                    }

                    var ret1 = await client.MyGetAsync(uri, token.error_msg);

                    R? retMe = JsonConvert.DeserializeObject<R>(ret1);
                    if (retMe == null)
                    {
                        return "";
                    }
                    else
                    {
                        if (retMe.status != 0)
                        {
                            if ("未认证".Equals(retMe.msg))
                            {
                                return await GetToken();
                            }
                            else
                            {
                                return "";
                            }
                        }
                        else
                        {
                            return token.error_msg;
                        }
                            
                    } 

                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"AuthorizationCheck：{ex.Message}");
            }

            return "";


        }

        private async Task<string> GetToken()
        {

            try
            {
                var uri = _configuration["tpiHost"] + "/api/system/user/login/appCrmLogin";

                var AuthorizationEnv = _configuration["AuthorizationEnv"];
 
                Hashtable sd = new Hashtable();
                sd.Add("mobile", "admin");
                sd.Add("password", "GvDSWt5YWrAU/u1T5V/aGf341gI7FsbSZ087sXfJ1JSKpqwX+oEm/aMdk7OeGezqKGmcY70Ty0woibhwtkbweTbgXgo79MiLxC51nQn963SLw/ITP1sLlHgkKu2VbDHn8UsAJRZu8IwdednXoKEY0GXxXw6W2BzInwSI4cW8Pi0=");
                sd.Add("appCode", "97418612");

                string newToken = "";
                using (var client = new HttpClient()) // WebClient oss.365me.me/mes/2025/11/05/3DZip.zip
                { 
                    var postStr = JsonConvert.SerializeObject(sd);

                    var ret1 = await client.MyPostAsync(uri, postStr, "");
                    _logger.LogInformation($"GetToken：{ret1}");
                    R? retMe = JsonConvert.DeserializeObject<R>(ret1);
                    if (retMe == null || retMe.status != 0)
                    {
                        _logger.LogError($"GetToken：{ret1}");
                        newToken = "Token取得失败" + ret1;
                    }
                    else
                    {
                        string dataStr = JsonConvert.SerializeObject(retMe.data);
                        TokenMes? tokenMes = JsonConvert.DeserializeObject<TokenMes>(dataStr);
                        if (tokenMes == null)
                        {
                            newToken = "Token取得失败" + ret1;
                        }
                        else
                        {
                            newToken = "Bearer " + tokenMes.token;
                        }
                    }

                    var exp = DateTime.Now.AddHours(20).ToString("yyyy-MM-dd HH:00:00");

                    DbContextOptionsBuilder<MyDbContext> optionsBuilder = new DbContextOptionsBuilder<MyDbContext>();

                    optionsBuilder.UseMySql(_configuration.GetConnectionString("MySQL"), MySqlServerVersion.LatestSupportedServerVersion);

                    using (var _db = new MyDbContext(optionsBuilder.Options))
                    {
                        NetSolidWorkLog token = _db.NetSolidWork.Find(_configuration["AuthorizationEnv"]);
                        if (token == null)
                        {
                            var person = new NetSolidWorkLog
                            {
                                id = AuthorizationEnv,
                                progress = "0",
                                oss_zip_id = "",
                                fileurl_zip = "",
                                oss_glb_id = "",
                                fileurl_glb = "",
                                sw_status = (int)SwStatus.Org,
                                error_code = exp,
                                error_msg = newToken,
                                zip_url = "",
                                draco_url = "",
                                solid_file_url = "",
                                glb_url = "",
                                create_id = "",
                                draco_zip_url = ""
                            };
                            _db.NetSolidWork.Add(person);
                            _db.SaveChanges();

                        }
                        else
                        {
                            token.error_code = exp;
                            token.error_msg = newToken;
                            _db.NetSolidWork.UpdateRange(token);
                            _db.SaveChanges();
                        }

                    }

                }

                return newToken;

            }
            catch (Exception ex)
            {
                _logger.LogError($"GetToken：{ex.StackTrace}");
            }

            return "";


        }

    }

    }
