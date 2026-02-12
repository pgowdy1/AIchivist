using ArchiveSearch.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArchiveSearch.Data;

public class ArchiveContext(DbContextOptions<ArchiveContext> options) : DbContext(options)
{
    public DbSet<CollectionEntity> Collections => Set<CollectionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CollectionEntity>(entity =>
        {
            entity.HasIndex(e => e.CollectionUnitId).IsUnique();
            entity.Property(e => e.Subjects).HasColumnType("text[]");
            entity.Property(e => e.Persnames).HasColumnType("text[]");
            entity.Property(e => e.Geognames).HasColumnType("text[]");
            entity.Property(e => e.Genres).HasColumnType("text[]");
            entity.Property(e => e.Corpnames).HasColumnType("text[]");
            entity.Property(e => e.SeriesTitles).HasColumnType("text[]");

            // Weighted tsvector column populated via raw SQL during indexing
            entity.Property(e => e.SearchVector).HasColumnType("tsvector");
            entity.HasIndex(e => e.SearchVector).HasMethod("gin");
        });
    }
}
