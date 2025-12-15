using CoreWebApi.Models;

namespace CoreWebApi.Mapper
{
    public interface IMysqlService
    {
        IEnumerable<NetSolidWorkLog> GetAllSolidWorkLog();
        NetSolidWorkLog? GetById(string id);
        bool CreateSolidWorkLog(NetSolidWorkLog product);
        void UpdateSolidWorkLog(NetSolidWorkLog product);
        bool DeleteSolidWorkLog(string id);
    }
}
