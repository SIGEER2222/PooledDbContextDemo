using Microsoft.EntityFrameworkCore;

namespace PooledDbContextDemo;

public class BlogService {
  private readonly IDbContextFactory<BlogContext> _contextFactory;

  public BlogService(IDbContextFactory<BlogContext> contextFactory) {
    _contextFactory = contextFactory;
  }

  public async Task CreateUserAsync(string userName, BlogContext context) {
    var user = new User { UserName = userName };
    context.Users.Add(user);
    await context.SaveChangesAsync();
  }

  public async Task CreateUserAsync(string userName) {
    using (var context = _contextFactory.CreateDbContext()) {
      var user = new User { UserName = userName };
      context.Users.Add(user);
      await context.SaveChangesAsync();
    }
  }

  public async Task<List<User>> GetAllUsersAsync() {
    using (var context = _contextFactory.CreateDbContext()) {
      return await context.Users.ToListAsync();
    }
  }
}
