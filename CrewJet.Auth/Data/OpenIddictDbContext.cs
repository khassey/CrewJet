using Microsoft.EntityFrameworkCore;

namespace CrewJet.Auth.Data;

public class OpenIddictDbContext(DbContextOptions<OpenIddictDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.UseOpenIddict();
    }
}
