// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A lognormal CMS option struck at or below −shift (its Hagan and TSR parts): QuantLib's Hagan caplet on a lognormal
/// volatility prices <c>optionletPrice(Call, max(effectiveCap, 1e-10))</c> (<c>conundrumpricer.cpp:171-178</c>), E[R] −
/// 1e-10 for a strike below 0, dropping −K; its linear TSR pricer refuses the strike in its section ("strike +
/// displacement (-0.0045371 + 0) must be non-negative"). On the wheel, on a flat 3% curve and a 20% volatility, a
/// capped CMS coupon fixing in a year with a spread of 1% and a cap of 0.5% pays 0.010000000099999993 on the analytic
/// pricer and 0.009996991868222522 on the numeric one, where R > 0 makes the right rate the cap; and a digital
/// replicated by caplets at ±1e-4 around 0 gives a probability of 0.49999950000000765. nanoql prices a caplet at or
/// below −shift, on a forward above −shift, as the coupon's expected rate less the strike and a floorlet there as 0,
/// the limits of each pricer's own formula, in each pricer before it prices an option. A forward at or below −shift has
/// no lognormal rate distribution, and QuantLib's refusal stands there. QuantLib's checks: its own swaplets, which give
/// the expected rate, its values above −shift, and its refusals on such a forward.
/// </summary>
public class LognormalStrikeBelowShiftTests
{
    private static readonly SwapIndex Index = CmsInputs.SwapIndex(new Period(10, TimeUnit.Years));

    [Fact]
    public void ACapBelowTheSpreadPaysTheCap()
    {
        CappedFlooredCmsCoupon c = CmsInputs.Capped(Index, 1, 1, 0.01, 0.005);
        foreach (CmsCouponPricer pricer in new CmsCouponPricer[]
        {
            new AnalyticHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.Standard, 0.0),
            new NumericHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.Standard, 0.0),
            new LinearTsrPricer(CmsInputs.Volatility(), 0.0),
        })
        {
            double rate = c.SetPricer(pricer).Rate(CmsInputs.Context);
            Assert.True(Math.Abs(rate - 0.005) <= 1e-15, $"{pricer.GetType().Name}: {rate}");
        }
    }

    [Fact]
    public void ADigitalAtZeroReplicatesToOne()
    {
        CmsCoupon c = CmsInputs.Coupon(Index, 1, 1);
        const double eps = 1e-4;
        foreach (CmsCouponPricer pricer in new CmsCouponPricer[]
        {
            new AnalyticHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.Standard, 0.0),
            new LinearTsrPricer(CmsInputs.Volatility(), 0.0),
        })
        {
            double below = pricer.CapletRate(c, -eps, CmsInputs.Context), above = pricer.CapletRate(c, eps, CmsInputs.Context);
            double probability = (below - above) / (2 * eps);
            Assert.True(Math.Abs(probability - 1.0) <= 1e-6, $"{pricer.GetType().Name}: {probability}");
            Assert.Equal(0.0, pricer.FloorletRate(c, -eps, CmsInputs.Context));

            // below −shift the caplet is the swaplet's rate plus the strike's distance
            Assert.True(Math.Abs(below - (pricer.SwapletRate(c, CmsInputs.Context) + eps)) <= 1e-15, $"{below}");
        }

        // above it, QuantLib's values
        var analytic = new AnalyticHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.Standard, 0.0);
        Assert.True(Math.Abs(analytic.CapletRate(c, eps, CmsInputs.Context) - 0.030536002916545926) <= 1e-15);
        Assert.True(Math.Abs(analytic.SwapletRate(c, CmsInputs.Context) - 0.03063600291654592) <= 1e-15);
    }

    [Fact]
    public void OnAShiftedSectionTheBoundIsMinusShift()
    {
        // on a 1% shift, QuantLib's TSR prices the caplet at −0.5% and refuses it at −1.5% ("strike + displacement
        // (-0.0144067 + 0.01) must be non-negative"); below −1% the caplet is the swaplet's rate plus the strike's
        // distance, which at −0.5% would be 0.03879502443628942, 9.1e-7 off QuantLib's
        CmsCoupon c = CmsInputs.Coupon(Index);
        var tsr = new LinearTsrPricer(CmsInputs.Volatility(shift: 0.01), 0.0);
        double swaplet = tsr.SwapletRate(c, CmsInputs.Context);
        Assert.Equal(0.03379502443628942, swaplet, 1e-15);
        Assert.Equal(0.038795933303573546, tsr.CapletRate(c, -0.005, CmsInputs.Context), 1e-15);
        Assert.Equal(9.088672846340302e-07, tsr.FloorletRate(c, -0.005, CmsInputs.Context), 1e-17);
        Assert.Equal(swaplet + 0.015, tsr.CapletRate(c, -0.015, CmsInputs.Context), 1e-15);
        Assert.Equal(0.0, tsr.FloorletRate(c, -0.015, CmsInputs.Context));
    }

    [Fact]
    public void AForwardAtOrBelowMinusShiftIsRefused()
    {
        // QuantLib's values and refusals on the wheel: a 1-year coupon fixing in 5 years on a flat −0.5% curve, its
        // forward −0.4991600372370923%, on a 20% lognormal volatility; and on a flat −1.5% curve with a 1% shift. Its
        // texts print six digits ("forward + displacement (-0.0049916 + 0) must be positive"), nanoql's Black formula
        // and smile section the values in full.
        CmsCoupon below = CouponOn(-0.005);
        Refused(() => below.SetPricer(new LinearTsrPricer(CmsInputs.Volatility(), 0.0)).Rate(CmsInputs.Context),
                "forward + displacement (-0.004991600372370923 + 0) must be positive");
        CmsCoupon shifted = CouponOn(-0.015);
        Refused(() => shifted.SetPricer(new LinearTsrPricer(CmsInputs.Volatility(shift: 0.01), 0.0)).Rate(CmsInputs.Context),
                "forward + displacement (-0.01490012494394369 + 0.01) must be positive");

        var analytic = new AnalyticHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.Standard, 0.0);
        Assert.Equal(-0.004966438871910542, analytic.SwapletRate(below, CmsInputs.Context), 1e-17);
        Refused(() => analytic.CapletRate(below, -0.01, CmsInputs.Context), "forward + displacement (-0.004991600372370923 + 0) must be positive");
        Refused(() => analytic.CapletRate(below, 0.01, CmsInputs.Context), "forward + displacement (-0.004991600372370923 + 0) must be positive");
        Assert.Equal(0.0, analytic.FloorletRate(below, -0.01, CmsInputs.Context));

        var numeric = new NumericHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.Standard, 0.0);
        Refused(() => below.SetPricer(numeric).Rate(CmsInputs.Context), "strike + displacement (-0.004991600372370923 + 0) must be non-negative");
    }

    // The flat market's 1-year coupon fixing in 5 years, on its 10-year index over a flat curve of the rate given.
    private static CmsCoupon CouponOn(double rate)
    {
        var curve = new FlatForward(CmsInputs.Today, rate, new Actual365Fixed());
        var index = new SwapIndex(
            "EuriborSwapIsdaFixA",
            new Period(10, TimeUnit.Years),
            2,
            new EURCurrency(),
            new TARGET(),
            new Period(1, TimeUnit.Years),
            BusinessDayConvention.ModifiedFollowing,
            new Thirty360(Thirty360Convention.BondBasis),
            new Euribor6M(curve));
        return CmsInputs.Coupon(index, 5);
    }

    private static void Refused(Func<double> f, string message)
    {
        Exception e = Assert.ThrowsAny<Exception>(() => f());
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }
}
