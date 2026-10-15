using Microsoft.EntityFrameworkCore;

namespace BookonnectWorkerService.Models
{
  public class DatabaseContext : DbContext
  {
    public DatabaseContext(DbContextOptions<DatabaseContext> options)
        : base(options)
    {
    }

    public DbSet<Book> Books { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Preference> Preferences { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);

      // Configure relationships
      modelBuilder.Entity<Preference>()
        .HasOne<User>()
        .WithMany()
        .HasForeignKey(p => p.UserId);

      modelBuilder.Entity<Preference>()
        .HasOne<Book>()
        .WithMany()
        .HasForeignKey(p => p.BookId);

      // Configure composite unique index on UserId + BookId
      modelBuilder.Entity<Preference>()
        .HasIndex(p => new { p.UserId, p.BookId })
        .IsUnique();
    }
  }
}