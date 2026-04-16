namespace ConsoleApp;

public static class ConcurrencyRiskCase
{
    public static void Run()
    {
        Console.WriteLine("\n[4] 并发风险");

        var raceCounter = 0;
        Parallel.For(0, 10000, _ =>
        {
            raceCounter++;
        });

        Console.WriteLine($"计数结果: {raceCounter}");
    }
}
