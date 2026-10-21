// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The replicating variance-swap engine at a nonzero dividend yield. QuantLib's fair variance is
/// 2·r − (2/T)·((S/D − f)/f + ln(f/S)) + Π/D, with r and D the risk-free zero rate and discount
/// (<c>replicatingvarianceswapengine.hpp:153-175</c>), as if the dividend yield were zero, where the log contract's
/// replication needs the drift r − q and the forward S·D_q/D_r: at a flat 20% volatility, 5% rates and a 3% yield,
/// QuantLib reads 0.0412007, against 0.0417209 on the equivalent zero-dividend process, whose risk-free discount is
/// D_r/D_q. nanoql's drift is 2·(z_r − z_q) and its forward S·D_q/D_r, the dividend curve read by the maturity date,
/// so it reads the equivalent process's value; at a zero yield it is QuantLib's, and the NPV keeps QuantLib's
/// discounting.
/// </summary>
public class ReplicatingVarianceSwapDividendTests
{
    [Fact]
    public void TheDividendYieldEntersTheDriftAndTheForward()
    {
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        double[] calls = [100.0, 105.0, 110.0, 115.0, 120.0, 125.0, 130.0, 135.0];
        double[] puts = [50.0, 55.0, 60.0, 65.0, 70.0, 75.0, 80.0, 85.0, 90.0, 95.0, 100.0];
        var swap = new VarianceSwap(PositionType.Long, 0.04, 50000, today, today + 90);

        VarianceSwapResults Price(double r, double q) => ReplicatingVarianceSwapEngine.Calculate(
            swap,
            context,
            new BlackScholesMertonProcess(
                100.0, new FlatForward(today, q, dc), new FlatForward(today, r, dc), new BlackConstantVol(today, null, 0.2, dc)),
            5.0,
            calls,
            puts);

        // QuantLib at r = 5%, q = 3%: variance 0.04120073802084082, NPV 59.3012641362909; on the equivalent process,
        // r = 2% and q = 0: 0.04172092504394879
        VarianceSwapResults withYield = Price(0.05, 0.03);
        Assert.Equal(0.04172092504394879, withYield.Variance!.Value, 1e-14);
        Assert.Equal(Price(0.02, 0.0).Variance!.Value, withYield.Variance!.Value, 1e-14);

        // the NPV discounts on the risk-free curve, as QuantLib's
        double discount = new FlatForward(today, 0.05, dc).Discount(today + 90);
        Assert.Equal(discount * 50000 * (withYield.Variance!.Value - 0.04), withYield.NPV!.Value, 1e-9);

        // at a zero yield, QuantLib's: 0.04169440403252741, NPV 83.68211828267292
        VarianceSwapResults noYield = Price(0.05, 0.0);
        Assert.Equal(0.04169440403252741, noYield.Variance!.Value, 1e-15);
        Assert.Equal(83.68211828267292, noYield.NPV!.Value, 1e-9);
    }
}
