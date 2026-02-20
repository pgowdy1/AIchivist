using System.Text.Json;
using ArchiveSearch.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ArchiveSearch.Data;

public class ArchiveContext(DbContextOptions<ArchiveContext> options) : DbContext(options)
{
    public DbSet<CollectionEntity> Collections => Set<CollectionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var jsonOptions = new JsonSerializerOptions();

        var stringArrayConverter = new ValueConverter<string[], string>(
            v => JsonSerializer.Serialize(v, jsonOptions),
            v => JsonSerializer.Deserialize<string[]>(v, jsonOptions) ?? Array.Empty<string>());

        var stringArrayComparer = new ValueComparer<string[]>(
            (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
            v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item == null ? 0 : item.GetHashCode())),
            v => v.ToArray());

        modelBuilder.Entity<CollectionEntity>(entity =>
        {
            entity.HasIndex(e => e.CollectionUnitId).IsUnique();

            entity.Property(e => e.Subjects).HasConversion(stringArrayConverter).Metadata.SetValueComparer(stringArrayComparer);
            entity.Property(e => e.Persnames).HasConversion(stringArrayConverter).Metadata.SetValueComparer(stringArrayComparer);
            entity.Property(e => e.Geognames).HasConversion(stringArrayConverter).Metadata.SetValueComparer(stringArrayComparer);
            entity.Property(e => e.Genres).HasConversion(stringArrayConverter).Metadata.SetValueComparer(stringArrayComparer);
            entity.Property(e => e.Corpnames).HasConversion(stringArrayConverter).Metadata.SetValueComparer(stringArrayComparer);
            entity.Property(e => e.SeriesTitles).HasConversion(stringArrayConverter).Metadata.SetValueComparer(stringArrayComparer);
        });
    }
}
