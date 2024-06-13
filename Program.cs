using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

await Main();

static async Task Main() {
  var services = new ServiceCollection();

  var tempPath = Path.Combine(Path.GetTempPath(), "blog.db");

  services.AddPooledDbContextFactory<BlogContext>(options =>
      options.UseSqlite(Help.ConnectionSql));

  services.AddTransient<BlogService>();

  var serviceProvider = services.BuildServiceProvider();

  using (var scope = serviceProvider.CreateScope()) {
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BlogContext>>();
    using (var context = factory.CreateDbContext()) {
      context.Database.EnsureCreated();
    }
  }

  var taskFail = GetFailTasks(serviceProvider);
  await RunApplicationAsync(serviceProvider, taskFail);

  var taskSuccess = GetSuccessTasks(serviceProvider);
  await RunApplicationAsync(serviceProvider, taskSuccess);

}

static async Task RunApplicationAsync(IServiceProvider serviceProvider, List<Func<BlogContext, Task>> taskGenerators) {
  using (var scope = serviceProvider.CreateScope()) {
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BlogContext>>();
    using (var context = factory.CreateDbContext()) {

      var users = await context.Users.ToListAsync();
      context.RemoveRange(users);
      await context.SaveChangesAsync();

      using (var transaction = await context.Database.BeginTransactionAsync()) {
        try {
          var tasks = new List<Task>();
          foreach (var taskGenerator in taskGenerators) {
            var task = taskGenerator(context);
            tasks.Add(task);
          }

          await Task.WhenAll(tasks);

          await transaction.CommitAsync();
        }
        catch (Exception ex) {
          Console.WriteLine($"Transaction failed: {ex.Message}");
          await transaction.RollbackAsync();
        }
      }
      users = await context.Users.ToListAsync();
      foreach (var user in users) {
        Console.WriteLine($"User ID: {user.UserId}, User Name: {user.UserName}");
      }
    }
  }
}

static List<Func<BlogContext, Task>> GetSuccessTasks(IServiceProvider serviceProvider) {
  var tasks = new List<Func<BlogContext, Task>>();
  var blogService = serviceProvider.GetRequiredService<BlogService>();
  for (int i = 0; i < 5; i++) {
    int userId = i;
    tasks.Add(context => blogService.CreateUserAsync($"User {userId}", context));
  }
  return tasks;
}

static List<Func<BlogContext, Task>> GetFailTasks(IServiceProvider serviceProvider) {
  var tasks = GetSuccessTasks(serviceProvider);
  tasks.Add(context => Task.Run(async () => {
    throw new Exception("Task failed");
  }));
  return tasks;
}



// static async Task RunApplicationAsync(IServiceProvider serviceProvider) {
//   using (var scope = serviceProvider.CreateScope()) {
//     var blogService = scope.ServiceProvider.GetRequiredService<BlogService>();

//     var tasks = new List<Task>();

//     for (int i = 0; i < 5; i++) {
//       int userId = i;
//       tasks.Add(Task.Run(async () => {
//         using (var taskScope = serviceProvider.CreateScope()) {
//           var scopedBlogService = taskScope.ServiceProvider.GetRequiredService<BlogService>();
//           await scopedBlogService.CreateUserAsync($"User {userId}");
//           var users = await scopedBlogService.GetAllUsersAsync();

//           foreach (var user in users) {
//             Console.WriteLine($"User ID: {user.UserId}, User Name: {user.UserName}");
//           }
//         }
//       }));
//     }
//     await Task.WhenAll(tasks);
//   }
// }
