using CoreWebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CoreWebApi
{
    public class MyDbContext : DbContext
    {
        public DbSet<NetSolidWorkLog> NetSolidWork { get; set; }

        public MyDbContext(DbContextOptions<MyDbContext> options) : base(options)
        {

        }
    }
}
