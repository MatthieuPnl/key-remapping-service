using Microsoft.EntityFrameworkCore;

namespace KeyRemappingService.Data;

public class KeyMappingDbContext : DbContext
{
    public KeyMappingDbContext(DbContextOptions<KeyMappingDbContext> options) : base(options) { }

    public DbSet<KeyMappingEntity> KeyMappings => Set<KeyMappingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KeyMappingEntity>(entity =>
        {
            entity.ToTable("KeyMappings");
            //Set the primary key for the table
            entity.HasKey(m => new { m.Keyboard, m.SourceCode });
        });
    }
}