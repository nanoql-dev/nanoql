// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The lognormal CMS spread pricer's vectors, <c>tests/vectors/experimental/coupons/lognormalcmsspreadpricer.json</c>,
/// on QuantLib's cmsspread test market: a spread coupon's rate and price and the pricer's caplet and floorlet rates and
/// prices on each volatility, correlation, integration rule and coupon discount curve, a coupon fixed on stored fixings,
/// and the constructor's and initialization's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LognormalCmsSpreadPricerVectorTests
{
    private static readonly Date Ref = new(23, Month.February, 2018);

    [Fact]
    public void LognormalCmsSpreadPricerVector()
    {
        var file = VectorFile.Load("experimental/coupons/lognormalcmsspreadpricer");
        var check = new VectorAssert(file);
        YieldTermStructure yts2 = new FlatForward(Ref, 0.02, new Actual365Fixed());
        SwapIndex cms10y = new EuriborSwapIsdaFixA(10 * TimeUnit.Years, yts2, yts2);
        SwapIndex cms2y = new EuriborSwapIsdaFixA(2 * TimeUnit.Years, yts2, yts2);
        var index = new SwapSpreadIndex("cms10y2y", cms10y, cms2y);
        var pay = new Date(23, Month.February, 2029);
        var start = new Date(23, Month.February, 2028);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Name.StartsWith("coupon", StringComparison.Ordinal))
            {
                var pricer = new LognormalCmsSpreadPricer(
                    CmsPricer(c.String("vol"), yts2), c.Double("correlation"), c.Bool("discounted") ? yts2 : null, c.Int("points"));
                CmsSpreadCoupon coupon = Coupon(index, pay, start, pay).SetPricer(pricer);
                double[] strikes = c.Doubles("strikes");
                check.Output(c, "rate", coupon.Rate(context));
                check.Output(c, "price", coupon.Price(yts2, context));
                check.Output(c, "caplets", strikes.Select(k => pricer.CapletRate(coupon, k, context)).ToArray());
                check.Output(c, "floorlets", strikes.Select(k => pricer.FloorletRate(coupon, k, context)).ToArray());
                check.Output(c, "capletPrices", strikes.Select(k => pricer.CapletPrice(coupon, k, context)).ToArray());
                check.Output(c, "floorletPrices", strikes.Select(k => pricer.FloorletPrice(coupon, k, context)).ToArray());
            }
            else if (c.Name.StartsWith("fixed", StringComparison.Ordinal))
            {
                var pricer = new LognormalCmsSpreadPricer(CmsPricer("lognormal", yts2), 0.6, yts2, 32);
                Date valueDate = index.ValueDate(Ref);
                CmsSpreadCoupon coupon = Coupon(index, valueDate + (1 * TimeUnit.Years), valueDate, valueDate + (1 * TimeUnit.Years))
                    .SetPricer(pricer);
                double[] strikes = c.Doubles("strikes");
                check.Output(c, "rate", coupon.Rate(context));
                check.Output(c, "caplets", strikes.Select(k => pricer.CapletRate(coupon, k, context)).ToArray());
                check.Output(c, "floorlets", strikes.Select(k => pricer.FloorletRate(coupon, k, context)).ToArray());
                check.Output(c, "capletPrices", strikes.Select(k => pricer.CapletPrice(coupon, k, context)).ToArray());
                check.Output(c, "floorletPrices", strikes.Select(k => pricer.FloorletPrice(coupon, k, context)).ToArray());
            }
            else if (c.Name.Contains("at-the-money", StringComparison.Ordinal))
            {
                check.Output(c, "rate", () => Coupon(index, pay, start, pay)
                    .SetPricer(new LognormalCmsSpreadPricer(
                        CmsPricer("lognormal", yts2), 0.6, yts2, 16, VolatilityType.ShiftedLognormal, 0.01, 0.01))
                    .Rate(context));
            }
            else
            {
                check.Output(c, "construction", () => new LognormalCmsSpreadPricer(
                    CmsPricer("lognormal", yts2), 0.6, yts2, 16, null, 0.01, 0.01).Correlation!.Value);
            }
        }

        check.Complete();
    }

    // QuantLib's TestData's linear TSR pricers.
    private static LinearTsrPricer CmsPricer(string vol, YieldTermStructure yts2)
    {
        (double level, VolatilityType type, double shift) = vol switch
        {
            "lognormal" => (0.20, VolatilityType.ShiftedLognormal, 0.0),
            "shifted" => (0.10, VolatilityType.ShiftedLognormal, 0.01),
            _ => (0.0075, VolatilityType.Normal, 0.01),
        };
        var sw = new ConstantSwaptionVolatility(Ref, new TARGET(), BusinessDayConvention.Following, level, new Actual365Fixed(), type, shift);
        return new LinearTsrPricer(sw, 0.01, yts2);
    }

    private static CmsSpreadCoupon Coupon(SwapSpreadIndex index, Date pay, Date start, Date end) =>
        new(pay, 10000.0, start, end, 2, index, 1.0, 0.0, null, null, new Actual360(), false);
}
