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

  await QueryDb(serviceProvider);

  var taskFail = GetFailTasks(serviceProvider);
  await RunApplicationAsync(serviceProvider, taskFail);

  var taskSuccess = GetSuccessTasks(serviceProvider);
  await RunApplicationAsync2(serviceProvider, new List<Task>());
  await RunApplicationAsync(serviceProvider, taskSuccess);
  await QueryDb(serviceProvider);
  await ClearDb(serviceProvider);
}

static async Task RunApplicationAsync2(IServiceProvider serviceProvider, List<Task> tasks) {
  using var connection = serviceProvider.GetRequiredService<IDbContextFactory<BlogContext>>().CreateDbContext().Database.GetDbConnection();
  var options = new DbContextOptionsBuilder<BlogContext>().UseSqlite(connection).Options;
  var context1 = new BlogContext(options);
  using var transaction = context1.Database.BeginTransaction();
  try {
    for (int i = 0; i < 5; i++) {
      var task = Task.Run(async () => {
        var blogService = serviceProvider.GetRequiredService<BlogService>();
        using var context2 = new BlogContext(options);
        context2.Database.UseTransaction(transaction.GetDbTransaction());
        await blogService.CreateUserAsync($"User {i}", context2);
      });
      tasks.Add(task);
    }
    await Task.WhenAll(tasks);
    await transaction.CommitAsync();
  }
  catch (System.Exception ex) {
    System.Console.WriteLine(new string('-', 50));
    System.Console.WriteLine($"RunApplicationAsync2 Transaction failed: {ex.Message}");
  }
}

static async Task RunApplicationAsync(IServiceProvider serviceProvider, List<Func<BlogContext, Task>> taskGenerators) {
  // using (var scope = serviceProvider.CreateScope())
  var factory = serviceProvider.GetRequiredService<IDbContextFactory<BlogContext>>();
  var masterContext = factory.CreateDbContext();
  var connection = masterContext.Database.GetDbConnection();
  await connection.OpenAsync();

  var transaction = await connection.BeginTransactionAsync();
  try {
    var tasks = new List<Task>();
    foreach (var taskGenerator in taskGenerators) {
      var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>().UseSqlite(connection).Options);
      context.Database.UseTransaction(transaction);
      var task = taskGenerator(context);
      tasks.Add(task);
    }

    await Task.WhenAll(tasks);
    await transaction.CommitAsync();
  }
  catch (Exception ex) {
    Console.WriteLine($"RunApplicationAsync Transaction failed: {ex.Message}");
    await transaction.RollbackAsync();
  }
  finally {
    masterContext?.Dispose();
    transaction?.Dispose();
    await connection?.CloseAsync();
  }
}

static async Task QueryDb(IServiceProvider serviceProvider) {
  using (var scope = serviceProvider.CreateScope()) {
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BlogContext>>();
    using (var context = factory.CreateDbContext()) {
      var users = await context.Users.ToListAsync();
      foreach (var user in users) {
        Console.WriteLine($"User ID: {user.UserId}, User Name: {user.UserName}");
      }
      System.Console.WriteLine($"Users:{users.Count}");
    }
  }
}

static async Task ClearDb(IServiceProvider serviceProvider) {
  using (var scope = serviceProvider.CreateScope()) {
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BlogContext>>();
    var context = factory.CreateDbContext();
    var users = await context.Users.ToListAsync();
    context.RemoveRange(users);
    await context.SaveChangesAsync();
  }
}

static List<Func<BlogContext, Task>> GetSuccessTasks(IServiceProvider serviceProvider) {
  var tasks = new List<Func<BlogContext, Task>>();
  var blogService = serviceProvider.GetRequiredService<BlogService>();
  for (int i = 0; i < 5; i++) {
    int userId = i;
    int currentUserId = userId;
    tasks.Add(async context => {
      await Task.Delay(200);
      await blogService.CreateUserAsync($"User {currentUserId}", context);
    });
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
