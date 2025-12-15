namespace CoreWebApi
{
    public enum SwStatus
    {
        //
        // 摘要:
        //     任务建立初始
        Org = 0,
        //
        // 摘要:
        //     下载完成
        Download = 1,
        //
        // 摘要:
        //     解压完成
        Unzip = 2,
        //
        // 摘要:
        //     转换SolidWork完成
        SolidWork = 3,
        //
        // 摘要:
        //     draco压缩
        Draco = 4,
        //
        // 摘要:
        //     Zip再压缩
        Zip = 5,
        //
        // 摘要:
        //     上传OSS
        Upload = 6,
        //
        // 摘要:
        //     彻底完成
        Complete = 7


    }
}
