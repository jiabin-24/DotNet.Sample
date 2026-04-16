namespace ConsoleApp;

public static class CodeStructureAndReadabilityCase
{
    public static void Run()
    {
        Console.WriteLine("\n[1] 示例一");

        var badResult = CalculateAdjustedPrice(12, 3, true, false, true);

        Console.WriteLine($"输出结果: {badResult:F2}");
    }

    private static decimal CalculateAdjustedPrice(
        decimal basePrice,
        decimal discountAmount,
        bool applyDiscount,
        bool applyFee,
        bool applyTax)
    {
        var adjustedPrice = basePrice;
        if (applyDiscount) adjustedPrice -= discountAmount;
        if (applyFee) adjustedPrice -= 2;
        if (applyTax) adjustedPrice *= 0.9m;
        return adjustedPrice < 0 ? 0 : adjustedPrice;
    }

}
