using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Data;

public class UygulamaDbContext : DbContext
{
    public UygulamaDbContext(DbContextOptions<UygulamaDbContext> options) : base(options)
    {
    }
}
