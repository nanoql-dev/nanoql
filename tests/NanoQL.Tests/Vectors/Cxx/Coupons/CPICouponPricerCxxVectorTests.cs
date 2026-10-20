// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's CPI coupon pricer vectors, <c>tests/vectors/cxx/cashflows/cpicouponpricer.json</c>:
/// <see cref="CPICouponPricer"/>'s swaplet, caplet and floorlet prices and rates, accrued rates and inspectors on CPI
/// coupons, with and without a volatility and a nominal curve, the live optionlets refused; and
/// <see cref="CPICouponPricer.SetCapletVolatility"/>, which refuses an empty volatility.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CPICouponPricerCxxVectorTests
{
    [Fact]
    public void CPICouponPricerCxxVector()
    {
        var file = VectorFile.Load("cxx/cashflows/cpicouponpricer");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var pricer = (CPICouponPricer)InflationCashFlowInputs.PricerOf(c.Input("pricer"), context);
            if (c.String("kind") == "setCapletVolatility")
            {
                CPIVolatilitySurface? vol = c.Input("volatility").ValueKind == System.Text.Json.JsonValueKind.Null
                    ? null
                    : InflationInputs.CPIVolatilityOf(c.Input("volatility"), context);
                check.Output(c, "setCapletVolatility", () => pricer.SetCapletVolatility(vol!).CapletVolatility is not null);
                continue;
            }

            var index = (ZeroInflationIndex)InflationInputs.IndexOf(c.Input("index"));
            CPICoupon coupon = InflationCashFlowInputs.CPICouponOf(c.Input("coupon"), index);
            double[] strikes = c.Doubles("strikes");
            Date[] dates = c.DateInputs("dates");

            check.Output(c, "swapletPrice", () => pricer.SwapletPrice(coupon, context));
            check.Output(c, "swapletRate", () => pricer.SwapletRate(coupon, context));
            check.Output(c, "accruedRate", Each(dates, d => pricer.AccruedRate(coupon, d, context)));
            check.Output(c, "capletVolatility", pricer.CapletVolatility is not null);
            check.Output(c, "nominalTermStructure", pricer.NominalTermStructure is not null);
            check.Output(c, "capletPrice", Each(strikes, k => pricer.CapletPrice(coupon, k, context)));
            check.Output(c, "capletRate", Each(strikes, k => pricer.CapletRate(coupon, k, context)));
            check.Output(c, "floorletPrice", Each(strikes, k => pricer.FloorletPrice(coupon, k, context)));
            check.Output(c, "floorletRate", Each(strikes, k => pricer.FloorletRate(coupon, k, context)));
        }

        check.Complete();
    }

    private static Func<object?>[] Each<T>(T[] xs, Func<T, double> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];
}
