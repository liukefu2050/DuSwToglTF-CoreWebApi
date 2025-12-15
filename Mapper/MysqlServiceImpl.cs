using CoreWebApi.Controllers;
using CoreWebApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Windows.Forms;

namespace CoreWebApi.Mapper
{
    public class MysqlServiceImpl : IMysqlService
    {

        private readonly MyDbContext _dbContext;
        private readonly ILogger<WeatherForecastController> _logger;
        private readonly IConfiguration _configuration;

        public MysqlServiceImpl(ILogger<WeatherForecastController> logger, MyDbContext dbContext, IConfiguration configuration)
        {
            _logger = logger;
            _dbContext = dbContext;
            _configuration = configuration;
        }


        public bool CreateSolidWorkLog(NetSolidWorkLog product)
        {
            _dbContext.NetSolidWork.Add(product);
            if (_dbContext.SaveChanges() > 0)
            {
                return true;
            }
            return false;
        }

        public bool DeleteSolidWorkLog(string id)
        {
            var product = _dbContext.NetSolidWork.Find(id);
            if (product != null)
            {
                _dbContext.NetSolidWork.Remove(product);
                _dbContext.SaveChanges();
            }
            return  true;
        }

        public IEnumerable<NetSolidWorkLog> GetAllSolidWorkLog()
        {
            return _dbContext.NetSolidWork.ToList();
        }

        public NetSolidWorkLog? GetById(string id)
        {
            return _dbContext.NetSolidWork.Find(id);
        }

        public void UpdateSolidWorkLog(NetSolidWorkLog product)
        {
            _dbContext.Entry(product).State = EntityState.Modified;
            _dbContext.SaveChanges();
        }
    }
}
