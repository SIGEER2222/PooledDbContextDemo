using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PooledDbContextDemo;

await Main();

static async Task Main() {
  var serviceProvider = (await SetService()).BuildServiceProvider();

  await QueryDb(serviceProvider);

  // var taskFail = GetFailTasks(serviceProvider);
  // await RunTasksWithinTransactionAsync(serviceProvider, taskFail);
  var taskSuccess = GetSuccessTasks(serviceProvider);
  await ExecuteConcurrentUserCreationAsync(serviceProvider, taskSuccess);
  await QueryDb(serviceProvider);
  await ClearDb(serviceProvider);
}

static async Task ExecuteConcurrentUserCreationAsync(IServiceProvider serviceProvider, List<Func<BlogContext, Task>> taskGenerators) {
  using var scope = serviceProvider.CreateScope();
  var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BlogContext>>();
  using var masterContext = factory.CreateDbContext();
  var connection = masterContext.Database.GetDbConnection();
  await connection.OpenAsync();

  using var transaction = await connection.BeginTransactionAsync();
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
    Console.WriteLine($"ExecuteConcurrentUserCreationAsync Transaction failed: {ex.Message}");
    await transaction.RollbackAsync();
  }
  finally {
    await connection.CloseAsync();
  }
}

static async Task QueryDb(IServiceProvider serviceProvider) {
  using var scope = serviceProvider.CreateScope();
  var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BlogContext>>();
  using var context = factory.CreateDbContext();
  var users = await context.Users.ToListAsync();
  foreach (var user in users) {
    Console.WriteLine($"User ID: {user.UserId}, User Name: {user.UserName}");
  }
  Console.WriteLine($"Users: {users.Count}");
}

static async Task ClearDb(IServiceProvider serviceProvider) {
  using var scope = serviceProvider.CreateScope();
  var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BlogContext>>();
  var context = factory.CreateDbContext();
  var users = await context.Users.ToListAsync();
  context.RemoveRange(users);
  await context.SaveChangesAsync();
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

static async Task<ServiceCollection> SetService() {
  var services = new ServiceCollection();

  var tempPath = Path.Combine(Path.GetTempPath(), "blog.db");

  services.AddPooledDbContextFactory<BlogContext>(options =>
      options.UseSqlite(Help.ConnectionSql));

  services.AddTransient<BlogService>();

  using (var scope = services.BuildServiceProvider().CreateScope()) {
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BlogContext>>();
    using (var context = factory.CreateDbContext()) {
      context.Database.EnsureCreated();
    }
  }
  return services;
}
