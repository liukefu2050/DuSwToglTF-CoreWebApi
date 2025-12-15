using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoreWebApi.Req
{
    public class ReqThreeDimInsert
    {
        public string id { get; set; }
        public string oss_zip_id { get; set; }
        public string fileurl_zip { get; set; }
    }
}
