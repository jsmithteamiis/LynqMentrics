using LynqMentrics.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LynqMentrics.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Link> Links => Set<Link>();
    public DbSet<Click> Clicks => Set<Click>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Link>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ShortCode).IsUnique();
            entity.Property(x => x.ShortCode).IsRequired().HasMaxLength(64);
            entity.Property(x => x.OriginalUrl).IsRequired().HasMaxLength(2048);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(x => x.User)
                .WithMany(u => u.Links)
                .HasForeignKey(x => x.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Click>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ClickedAt);
            entity.Property(x => x.ClickedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(x => x.Link)
                .WithMany(l => l.Clicks)
                .HasForeignKey(x => x.LinkId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
