// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's year-on-year pricer vectors, <c>tests/vectors/cxx/cashflows/inflationcouponpricer.json</c>: the
/// base, Black, unit-displaced Black and Bachelier pricers' swaplet, caplet and floorlet prices and rates and
/// inspectors on year-on-year coupons, with and without a volatility and a nominal curve;
/// <see cref="YoYInflationCouponPricer.SetCapletVolatility"/>, which refuses an empty volatility; and a pricer given a
/// CPI coupon, refused.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InflationCouponPricerCxxVectorTests
{
    [Fact]
    public void InflationCouponPricerCxxVector()
    {
        var file = VectorFile.Load("cxx/cashflows/inflationcouponpricer");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var pricer = (YoYInflationCouponPricer)InflationCashFlowInputs.PricerOf(c.Input("pricer"), context);
            switch (c.String("kind"))
            {
                case "setCapletVolatility":
                    YoYOptionletVolatilitySurface? vol = c.Input("volatility").ValueKind == JsonValueKind.Null
                        ? null
                        : InflationInputs.YoYVolatilityOf(c.Input("volatility"), context);
                    check.Output(
                        c, "setCapletVolatility", () => pricer.SetCapletVolatility(vol!).CapletVolatility is not null);
                    continue;
                case "initialize":
                    var zero = (ZeroInflationIndex)InflationInputs.IndexOf(c.Input("index"));
                    CPICoupon cpi = InflationCashFlowInputs.CPICouponOf(c.Input("coupon"), zero);
                    check.Output(c, "initialize", () =>
                    {
                        _ = pricer.SwapletRate(cpi, context);
                        return true;
                    });
                    continue;
            }

            var index = (YoYInflationIndex)InflationInputs.IndexOf(c.Input("index"));
            YoYInflationCoupon coupon = InflationCashFlowInputs.YoYCouponOf(c.Input("coupon"), index);
            double[] strikes = c.Doubles("strikes");

            check.Output(c, "swapletPrice", () => pricer.SwapletPrice(coupon, context));
            check.Output(c, "swapletRate", () => pricer.SwapletRate(coupon, context));
            check.Output(c, "capletVolatility", pricer.CapletVolatility is not null);
            check.Output(c, "nominalTermStructure", pricer.NominalTermStructure is not null);
            check.Output(c, "capletPrice", Each(strikes, k => pricer.CapletPrice(coupon, k, context)));
            check.Output(c, "capletRate", Each(strikes, k => pricer.CapletRate(coupon, k, context)));
            check.Output(c, "floorletPrice", Each(strikes, k => pricer.FloorletPrice(coupon, k, context)));
            check.Output(c, "floorletRate", Each(strikes, k => pricer.FloorletRate(coupon, k, context)));
        }

        check.Complete();
    }

    private static Func<object?>[] Each(double[] xs, Func<double, double> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];
}
