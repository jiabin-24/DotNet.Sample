using ConsoleApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ConsoleApp;

public static class ResourceManagementCase
{
    public static void Run()
    {
        Console.WriteLine("\n[3] 资源管理（EF Core N+1 查询示例）");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("SqlServerExpress");
        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("<"))
        {
            Console.WriteLine("请先在 ConsoleApp/appsettings.json 中配置 ConnectionStrings:SqlServerExpress。");
            return;
        }

        var counter = new SqlCounterInterceptor();

        try
        {
            counter.Reset();
            using (var badContext = CreateContext(connectionString, counter))
            {
                var orders = badContext.Orders
                    .AsNoTracking()
                    .ToList();

                foreach (var order in orders)
                {
                    var items = badContext.OrderItems
                        .AsNoTracking()
                        .Where(i => i.OrderId == order.Id)
                        .ToList();

                    Console.WriteLine($"订单 {order.Id}，商品数：{items.Count}");
                }
            }

            Console.WriteLine($"N+1 场景 SQL 条数：{counter.CommandCount}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("示例运行失败，请确认 SQL Server Express、连接串、表结构和数据已准备好。");
            Console.WriteLine($"错误：{ex.Message}");
        }
    }

    private static DemoDbContext CreateContext(string connectionString, SqlCounterInterceptor counter)
    {
        var options = new DbContextOptionsBuilder<DemoDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(counter)
            .Options;

        return new DemoDbContext(options);
    }
}
