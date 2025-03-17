using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.ApplicationServices;
using urban_style_auto_regist.Model;

namespace urban_style_auto_regist
{
    public class AppDbContext : DbContext
    {
        public DbSet<CombineShop> CombineShops { get; set; }

        public DbSet<CombineProduct> CombineProducts { get; set; }

        public DbSet<CombineProductImg> CombineProductImgs { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            const string connectionString = "server=localhost;database=iameeo_db;user=root;password=wndnjsWkd!2;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        }

    }
}