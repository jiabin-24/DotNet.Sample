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
            Console.WriteLine("\n--- 错误写法（N+1）---");
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

            Console.WriteLine("\n--- 正确写法（Include 预加载）---");
            counter.Reset();
            using (var goodContext = CreateContext(connectionString, counter))
            {
                var orders = goodContext.Orders
                    .Include(o => o.Items)
                    .AsNoTracking()
                    .ToList();

                foreach (var order in orders)
                {
                    Console.WriteLine($"订单 {order.Id}，商品数：{order.Items.Count}");
                }
            }

            Console.WriteLine($"优化后 SQL 条数：{counter.CommandCount}");
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
