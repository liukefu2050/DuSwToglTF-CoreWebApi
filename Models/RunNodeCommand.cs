using System.Diagnostics;

namespace CoreWebApi.Models
{
    public static class RunNodeCommand
    {

    public static string RunNode(string node,string command)
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo
        {
            FileName = node, // 指定为node命令
            Arguments = command, // 传递你的Node.js脚本路径或代码
            UseShellExecute = false, // 不使用shell执行
            RedirectStandardOutput = true, // 重定向输出
            CreateNoWindow = true // 不创建新窗口
        };

        using (Process proces = Process.Start(processStartInfo))
        {
            using (StreamReader reader = proces.StandardOutput)
            {
                string result = reader.ReadToEnd();

                Console.WriteLine(result); // 输出结果
                return result;
                    
            }
        }
    }

}
}
