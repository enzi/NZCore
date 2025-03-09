// <copyright project="NZCore" file="BigMath.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

//namespace NZCore
//{
    // public static class BigMath
    // {
    //     private static readonly Random Random = new Random();
    //
    //     /// <summary>
    //     /// This doesn't follow any kind of sane random distribution, so use this for testing purposes only.
    //     /// <para>5% of the time, mantissa is 0.</para>
    //     /// <para>10% of the time, mantissa is round.</para>
    //     /// </summary>
    //     public static BigDouble RandomBigDouble(double absMaxExponent)
    //     {
    //         if (Random.NextDouble() * 20 < 1)
    //         {
    //             return BigDouble.Normalize(0, 0);
    //         }
    //
    //         var mantissa = Random.NextDouble() * 10;
    //         if (Random.NextDouble() * 10 < 1)
    //         {
    //             mantissa = Math.Round(mantissa);
    //         }
    //
    //         mantissa *= Math.Sign(Random.NextDouble() * 2 - 1);
    //         var exponent = (long)(Math.Floor(Random.NextDouble() * absMaxExponent * 2) - absMaxExponent);
    //         return BigDouble.Normalize(mantissa, exponent);
    //     }
    //
    //     /// <summary>
    //     /// If you're willing to spend 'resourcesAvailable' and want to buy something with
    //     /// exponentially increasing cost each purchase (start at priceStart, multiply by priceRatio,
    //     /// already own currentOwned), how much of it can you buy?
    //     /// <para>
    //     /// Adapted from Trimps source code.
    //     /// </para>
    //     /// </summary>
    //     public static BigDouble AffordGeometricSeries(BigDouble resourcesAvailable, BigDouble priceStart,
    //         BigDouble priceRatio, BigDouble currentOwned)
    //     {
    //         var actualStart = priceStart * BigDouble.Pow(priceRatio, currentOwned);
    //
    //         //return Math.floor(log10(((resourcesAvailable / (priceStart * Math.pow(priceRatio, currentOwned))) * (priceRatio - 1)) + 1) / log10(priceRatio));
    //
    //         return BigDouble.Floor(BigDouble.Log10(resourcesAvailable / actualStart * (priceRatio - 1) + 1) / BigDouble.Log10(priceRatio));
    //     }
    //
    //     /// <summary>
    //     /// How much resource would it cost to buy (numItems) items if you already have currentOwned,
    //     /// the initial price is priceStart and it multiplies by priceRatio each purchase?
    //     /// </summary>
    //     public static BigDouble SumGeometricSeries(BigDouble numItems, BigDouble priceStart, BigDouble priceRatio,
    //         BigDouble currentOwned)
    //     {
    //         var actualStart = priceStart * BigDouble.Pow(priceRatio, currentOwned);
    //
    //         return actualStart * (1 - BigDouble.Pow(priceRatio, numItems)) / (1 - priceRatio);
    //     }
    //
    //     /// <summary>
    //     /// If you're willing to spend 'resourcesAvailable' and want to buy something with
    //     /// additively increasing cost each purchase (start at priceStart, add by priceAdd,
    //     /// already own currentOwned), how much of it can you buy?
    //     /// </summary>
    //     public static BigDouble AffordArithmeticSeries(BigDouble resourcesAvailable, BigDouble priceStart,
    //         BigDouble priceAdd, BigDouble currentOwned)
    //     {
    //         var actualStart = priceStart + currentOwned * priceAdd;
    //
    //         //n = (-(a-d/2) + sqrt((a-d/2)^2+2dS))/d
    //         //where a is actualStart, d is priceAdd and S is resourcesAvailable
    //         //then floor it and you're done!
    //
    //         var b = actualStart - priceAdd / 2;
    //         var b2 = BigDouble.Pow(b, 2);
    //
    //         return BigDouble.Floor(
    //             (BigDouble.Sqrt(b2 + priceAdd * resourcesAvailable * 2) - b) / priceAdd
    //         );
    //     }
    //
    //     /// <summary>
    //     /// How much resource would it cost to buy (numItems) items if you already have currentOwned,
    //     /// the initial price is priceStart and it adds priceAdd each purchase?
    //     /// <para>
    //     /// Adapted from http://www.mathwords.com/a/arithmetic_series.htm
    //     /// </para>
    //     /// </summary>
    //     public static BigDouble SumArithmeticSeries(BigDouble numItems, BigDouble priceStart, BigDouble priceAdd,
    //         BigDouble currentOwned)
    //     {
    //         var actualStart = priceStart + currentOwned * priceAdd;
    //
    //         //(n/2)*(2*a+(n-1)*d)
    //
    //         return numItems / 2 * (2 * actualStart + (numItems - 1) * priceAdd);
    //     }
    //
    //     /// <summary>
    //     /// When comparing two purchases that cost (resource) and increase your resource/sec by (delta_RpS),
    //     /// the lowest efficiency score is the better one to purchase.
    //     /// <para>
    //     /// From Frozen Cookies: http://cookieclicker.wikia.com/wiki/Frozen_Cookies_(JavaScript_Add-on)#Efficiency.3F_What.27s_that.3F
    //     /// </para>
    //     /// </summary>
    //     public static BigDouble EfficiencyOfPurchase(BigDouble cost, BigDouble currentRpS, BigDouble deltaRpS)
    //     {
    //         return cost / currentRpS + cost / deltaRpS;
    //     }
    // }
//}