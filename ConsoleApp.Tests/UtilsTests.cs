using System;
using ConsoleApp;
using Xunit;

namespace ConsoleApp.Tests;

public class UtilsTests
{
    [Theory]
    [InlineData(100, 0, 100)]
    [InlineData(100, 10, 90)]
    [InlineData(250, 25, 187.5)]
    [InlineData(99.99, 50, 49.995)]
    [InlineData(100, 100, 0)]
    public void CalculateDiscountedPrice_ValidDiscount_ReturnsExpectedPrice(decimal originalPrice, decimal discountPercentage, decimal expected)
    {
        var result = Utils.CalculateDiscountedPrice(originalPrice, discountPercentage);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void CalculateDiscountedPrice_InvalidDiscount_ThrowsArgumentOutOfRangeException(decimal invalidDiscount)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Utils.CalculateDiscountedPrice(100m, invalidDiscount));

        Assert.Equal("discountPercentage", ex.ParamName);
    }
}
