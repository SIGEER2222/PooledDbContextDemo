using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BlogContext> {
  public BlogContext CreateDbContext(string[] args) {
    var optionsBuilder = new DbContextOptionsBuilder<BlogContext>();
    optionsBuilder.UseSqlite(Help.ConnectionSql);

    return new BlogContext(optionsBuilder.Options);
  }
}
