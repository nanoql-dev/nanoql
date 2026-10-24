// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The shape of the Hagan g-function's shift (the S-cms plan's CMR5 (v)): QuantLib's <c>shapeOfShift</c> computes
/// (1 − e^(−a·x))/a, which cancels as the reversion a nears 0 from above, and reads a negative reversion as 0
/// (<c>conundrumpricer.cpp:929-938</c>): on the wheel, on a flat 3% curve and a 20% volatility, a 1-year coupon fixing
/// in 10 years prices 0.03239868837727989 on the numeric pricer's non-parallel shifts at a = 0 and at every negative a,
/// and 0.032398699033410853 at a = 1e-12, 1.1e-8 away (the analytic pricer 9.5e-9). nanoql takes −expm1(−a·x)/a below
/// 1e-4, negative reversions included, and x at 0, with QuantLib's form at and above 1e-4. QuantLib's checks: its own
/// values at 0 and at 1e-4, on the numeric pricer, whose values CMR5 (xxii) leaves QuantLib's.
/// </summary>
public class HaganShiftShapeTests
{
    [Fact]
    public void ASmallReversionIsContinuous()
    {
        double zero = Numeric(0.0);
        Assert.True(Math.Abs(zero - 0.03239868837727989) <= 1e-14 * zero, $"{zero}");
        Assert.True(Math.Abs(Numeric(1e-12) - zero) <= 1e-14, $"{Numeric(1e-12)}");
        double at = Numeric(1e-4);
        Assert.True(Math.Abs(at - 0.03239876735093427) <= 1e-14 * at, $"{at}");
        Assert.True(Math.Abs(Numeric(1e-4 * (1.0 - 1e-12)) - at) <= 1e-12);
        Assert.True(Math.Abs(Analytic(1e-12) - Analytic(0.0)) <= 1e-14);
        Assert.True(Math.Abs(Analytic(1e-4 * (1.0 - 1e-12)) - Analytic(1e-4)) <= 1e-12);

        // QuantLib's: 1.1e-8 at 1e-12
        Assert.True(Math.Abs(0.032398699033410853 - 0.03239868837727989) > 1e-8);
    }

    [Fact]
    public void ANegativeReversionIsRead()
    {
        double zero = Analytic(0.0);
        double negative = Analytic(-0.01);
        double positive = Analytic(0.01);

        // the price moves through 0 as it moves above it, where QuantLib's is flat
        Assert.True(negative < zero && zero < positive, $"{negative} {zero} {positive}");
        Assert.True(Math.Abs(Analytic(-1e-12) - zero) <= 1e-14);

        // its slope through 0 is continuous
        double left = (zero - Analytic(-1e-6)) / 1e-6, right = (Analytic(1e-6) - zero) / 1e-6;
        Assert.True(Math.Abs(left - right) <= 0.01 * right, $"{left} {right}");
    }

    private static double Analytic(double a) => Rate(new AnalyticHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.NonParallelShifts, a));

    private static double Numeric(double a) => Rate(new NumericHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.NonParallelShifts, a));

    private static double Rate(CmsCouponPricer pricer) =>
        CmsInputs.Coupon(CmsInputs.SwapIndex(new Period(10, TimeUnit.Years))).SetPricer(pricer).Rate(CmsInputs.Context);
}
