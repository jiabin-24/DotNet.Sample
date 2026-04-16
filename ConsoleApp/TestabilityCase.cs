namespace ConsoleApp;

public static class TestabilityCase
{
    public static void Run()
    {
        Console.WriteLine("\n[5] 可测试性");

        var service = new GreetingService();
        Console.WriteLine(service.GetGreeting("Copilot"));
    }

    private sealed class GreetingService
    {
        public string GetGreeting(string name)
        {
            var greeting = DateTime.Now.Hour < 12 ? "早上好" : "下午好";
            return $"{greeting}, {name}";
        }
    }
}
