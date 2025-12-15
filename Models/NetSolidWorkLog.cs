using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoreWebApi.Models
{
    [Table("net_solid_work_log")]
    public class NetSolidWorkLog
    {
        [Key]
        public string id { get; set; }
        public string progress { get; set; }
        public string oss_zip_id { get; set; }
        public string fileurl_zip { get; set; }
        public string oss_glb_id { get; set; }
        public string fileurl_glb { get; set; }
        public int sw_status { get; set; }
        public string error_code { get; set; }
        public string error_msg { get; set; }
        public string zip_url { get; set; }
        public string draco_url { get; set; }
        public string solid_file_url { get; set; }
        public string glb_url { get; set; }
        public string create_id { get; set; }
        public string draco_zip_url { get; set; }
    }
}
