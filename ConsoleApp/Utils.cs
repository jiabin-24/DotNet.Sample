using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp
{
    public static class Utils
    {
        // 计算商品的折扣后价格
        public static decimal CalculateDiscountedPrice(decimal originalPrice, decimal discountPercentage)
        {
            if (discountPercentage < 0 || discountPercentage > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(discountPercentage), "折扣百分比必须在0到100之间。");
            }

            decimal discountAmount = originalPrice * (discountPercentage / 100);
            decimal discountedPrice = originalPrice - discountAmount;
            return discountedPrice;
        }
    }
}
