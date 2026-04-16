namespace ConsoleApp;

public static class ErrorHandlingCase
{
    public static void Run()
    {
        Console.WriteLine("\n[2] 错误处理");

        try
        {
            var result = Divide(10, 0);
            Console.WriteLine($"输出结果: {result}");
        }
        catch (Exception)
        {
            Console.WriteLine("发生错误。");
        }
    }

    private static int Divide(int dividend, int divisor)
    {
        return dividend / divisor;
    }
}
