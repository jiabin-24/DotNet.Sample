namespace ConsoleApp;

public static class CodeStructureAndReadabilityCase
{
    public static void Run()
    {
        Console.WriteLine("\n[1] 示例一");

        var badResult = Calc(12, 3, true, false, true);

        Console.WriteLine($"输出结果: {badResult:F2}");
    }

    private static decimal Calc(decimal a, decimal b, bool c, bool d, bool e)
    {
        var x = a;
        if (c) x -= b;
        if (d) x -= 2;
        if (e) x *= 0.9m;
        return x < 0 ? 0 : x;
    }

}
