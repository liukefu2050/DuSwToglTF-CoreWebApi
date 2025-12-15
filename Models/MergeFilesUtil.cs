using System.Diagnostics;

namespace CoreWebApi.Models
{
    public static class MergeFilesUtil
    {

        public static void MergeFiles(string inputDirectory, string outputFile)
        {
            using (FileStream outputFileStream = new FileStream(outputFile, FileMode.Create))
            {
                string[] files = Directory.GetFiles(inputDirectory, "chunk_*.dat", SearchOption.TopDirectoryOnly);
                Array.Sort(files); // 按照文件名排序，确保正确合并

                foreach (string file in files)
                {
                    using (FileStream inputFileStream = new FileStream(file, FileMode.Open))
                    {
                        byte[] buffer = new byte[checked((uint)inputFileStream.Length)];
                        inputFileStream.Read(buffer, 0, buffer.Length);
                        outputFileStream.Write(buffer, 0, buffer.Length);
                    }
                }
            }
        }

    }
}
