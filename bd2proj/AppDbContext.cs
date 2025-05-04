using bd2proj.Models;
using Microsoft.EntityFrameworkCore;

namespace bd2proj
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options): base(options) {}
        
        public DbSet<XmlDocumentModel> XmlDocuments { get; set; }
    }
}