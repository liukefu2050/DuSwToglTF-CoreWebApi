namespace CoreWebApi
{
    public static class SYSTEM_CONST
    {
        public static string SYSTEM_CONST_SUCCESS = "00000";
        public static string SYSTEM_CONST_SUCCESS_MSG = "成功";

        public static string SYSTEM_CONST_FAIL = "99999";
        public static string SYSTEM_CONST_FAIL_MSG = "失败";

        public static int SYSTEM_CONST_SUCCESS_STATUS = 0;
        public static int SYSTEM_CONST_FAIL_STATUS = 1;

        //分片大小SIZE
        public static long SYSTEM_CONST_FILE_CHUNK_SIZE = 1024 * 1024 * 10;
    }
}
