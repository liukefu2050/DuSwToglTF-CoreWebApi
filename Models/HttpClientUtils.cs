using Newtonsoft.Json;
using System.Text;

namespace CoreWebApi.Models
{
    public static class HttpClientUtils
    {
        public static async Task<string> DownloadFileTaskAsync(this HttpClient client, Uri uri, string FileName)
        {
            try
            {

                using (var s = await client.GetStreamAsync(uri))
                {
                    using (var fs = new FileStream(FileName, FileMode.CreateNew))
                    {

                        await s.CopyToAsync(fs);
                    }
                }

            }
            catch (Exception ex)
            {
                return ex.Message;

            }
            return "下载成功";
        }


        public static async Task<string> UploadFileTaskAsync(this HttpClient client, string url, string fileName, string filePath, string Authorization)
        {
            using (var content = new MultipartFormDataContent())
            {
                // 目标URL
                //string url = "http://192.168.15.56:8080/tpi/oss/upload";

                // 读取文件内容
                var fileStream = File.OpenRead(filePath);
                // 添加文件到MultipartFormDataContent中
                var fileContent = new StreamContent(fileStream);
                client.DefaultRequestHeaders.Add("Authorization", Authorization);
                fileContent.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("form-data")
                {
                    Name = "\"file\"", // 注意：这里的引号是为了防止某些服务器对参数名的特殊处理（如PHP）
                    FileName = "\"" + fileName + "\"" // 注意：这里的引号是为了防止某些服务器对文件名的特殊处理（如PHP）
                };
                content.Add(fileContent);

                // 发送POST请求
                HttpResponseMessage response = await client.PostAsync(url, content);
                
                var haha = response.EnsureSuccessStatusCode(); // 确保响应状态码表示成功
                string responseBody = await response.Content.ReadAsStringAsync();
                //JsonConvert.DeserializeObject
                //Console.WriteLine("Response: " + responseBody);

                return responseBody;
            }

        }


        public static async Task<string> MyPostAsync(this HttpClient client, string url, string content, string Authorization)
        {
            // 目标URL
            if (!"".Equals(Authorization))
            {
                //Console.WriteLine($"Httpclient Authorization = : {Authorization}");
                client.DefaultRequestHeaders.Add("Authorization", Authorization);
            }
            var contentContent = new StringContent(content, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, contentContent);
            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                //Console.WriteLine(responseBody);
                return responseBody;
            }
            else
            {
                Console.WriteLine($"Httpclient MyPostAsync: {response.StatusCode}");

                R ret = new R();
                ret.status = 1;
                return JsonConvert.SerializeObject(ret);
            }

        }

        public static async Task<string> MyGetAsync(this HttpClient client, string url, string Authorization)
        {
            // 目标URL
            client.DefaultRequestHeaders.Add("Authorization", Authorization);
            //Console.WriteLine($"Httpclient Authorization = : {Authorization}");
            //var contentContent = new StringContent(content, Encoding.UTF8, "application/json");
            var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseBody);
                return responseBody;
            }
            else
            {
                Console.WriteLine($"Httpclient MyPostAsync: {response.StatusCode}");

                R ret = new R();
                ret.status = 1;
                return JsonConvert.SerializeObject(ret);
            }

        }
    }
}
