using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

public class BlogContext : DbContext {
  public BlogContext(DbContextOptions<BlogContext> options) : base(options) { }

  public DbSet<User> Users { get; set; }
  public DbSet<Post> Posts { get; set; }

  protected override void OnModelCreating(ModelBuilder modelBuilder) {
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<User>()
        .HasMany(u => u.Posts)
        .WithOne(p => p.User)
        .HasForeignKey(p => p.UserId);
  }
}

public class User {
  [Key]
  public int UserId { get; set; }
  public string UserName { get; set; }

  public List<Post> Posts { get; set; }
}

public class Post {
  [Key]
  public int PostId { get; set; }
  public string Title { get; set; }
  public string Content { get; set; }

  public int UserId { get; set; }
  public User User { get; set; }
}
