// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A caplet's expiry on the analytic and tree cap engines: QuantLib's analytic engine values a caplet still to be fixed
/// as a bond option expiring at its accrual start (<c>analyticcapfloorengine.cpp:94-112</c>) and its tree exercises it
/// there (<c>discretizedcapfloor.cpp:53-55</c>), where the instrument fixes its rate, QuantLib's Monte Carlo engine
/// reads it and its Black engine takes the volatility, at the fixing date, so a caplet with a fixing lag is priced on
/// the variance to its start. On the wheel, a 1-year semiannual cap at 4% on Euribor 6M from six months after the spot
/// date, two fixing days, notional 1,000,000, on Hull-White (0.1, 0.01) on a flat 4% Actual/365 (Fixed) curve on 15
/// January 2025, the analytic engine prices 3,024.081283636271 and the tree (200 steps) 3,019.994708839063; QuantLib's
/// own five-argument bond option (<c>hullwhite.cpp:108-131</c>), the option at the fixing on the bond from the start,
/// which its Jamshidian engine uses, gives 3,005.838255466084. nanoql's tree values each caplet still to be fixed
/// before its start at its fixing, on every model, from its two discount bonds rolled back there, and its analytic
/// engine on Hull-White takes the five-argument option; Vasicek's, CIR's and G2's analytic options keep QuantLib's
/// expiry at the start, since QuantLib has no such option for them. A caplet fixed on its start is QuantLib's on both
/// engines. QuantLib's check: its five-argument bond option, its Monte Carlo engine, and the tree converging to both.
/// </summary>
public class CapletFixingExpiryTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.04, new Actual365Fixed());
    private static readonly HullWhite Model = new(Curve, 0.1, 0.01);

    // QuantLib's analytic prices on the wheel, then its price with the caplets on its five-argument bond option at
    // their fixings (hullwhite.cpp:108-131, transcribed, since the wheel wraps the four-argument option alone).
    [Fact]
    public void TheHullWhiteAnalyticOptionExpiresAtTheFixing()
    {
        foreach ((string name, CapFloor capFloor, double quantLib, double atFixing) in new (string, CapFloor, double, double)[]
                 {
                     ("cap at 4%", new Cap(Leg(), [0.04]), 3024.081283636271, 3005.838255466084),
                     ("cap at 5%", new Cap(Leg(), [0.05]), 415.72242484044216, 407.3245302559998),
                     ("floor at 4%", new Floor(Leg(), [0.04]), 3169.2841879442703, 3151.04115977414),
                     ("collar at 4.5% / 3.5%", new Collar(Leg(), [0.045], [0.035]), -70.52665465865994, -70.22934875676833),
                 })
        {
            double analytic = AnalyticCapFloorEngine.Calculate(capFloor, Context, Model).NPV!.Value;
            Assert.True(Tolerance.Within(atFixing, analytic, 1e6), $"{name}: {analytic}");
            Assert.True(Math.Abs(analytic - quantLib) > 0.25, $"{name}: {analytic} against QuantLib's {quantLib}");
        }
    }

    [Fact]
    public void TheTreeConvergesToTheFixingValue()
    {
        var cap = new Cap(Leg(), [0.04]);
        double e200 = (TreeCapFloorEngine.Calculate(cap, Context, Model, 200).NPV!.Value / 3005.838255466084) - 1.0;
        double e800 = (TreeCapFloorEngine.Calculate(cap, Context, Model, 800).NPV!.Value / 3005.838255466084) - 1.0;
        Assert.True(Math.Abs(e800) <= 2e-4, $"{e200:E}, {e800:E}");
        Assert.True(Math.Abs(e800) < Math.Abs(e200), $"{e200:E}, {e800:E}");
    }

    [Fact]
    public void TheMonteCarloEngineAgreesWithTheFixingValue()
    {
        var cap = new Cap(Leg(), [0.04]);
        InstrumentResults mc = MCHullWhiteCapFloorEngine.Calculate<PseudoRandom, GeneralStatistics>(
            cap, Context, Model, false, true, 200000, null, null, 42);
        double error = mc.ErrorEstimate!.Value;
        Assert.True(Math.Abs(mc.NPV!.Value - 3005.838255466084) <= 2.0 * error, $"{mc.NPV} ± {error}");
        Assert.True(Math.Abs(mc.NPV!.Value - 3024.081283636271) > 3.0 * error, $"{mc.NPV} ± {error}");
    }

    // QuantLib on the wheel, the cap at 4% on Vasicek (r0 = 0.04, a = 0.1, b = 0.04, sigma = 0.01) with the curve given:
    // the analytic engine 3,001.1439405547217, its option at the caplets' starts.
    [Fact]
    public void OnVasicekTheAnalyticOptionKeepsQuantLibsExpiry()
    {
        var cap = new Cap(Leg(), [0.04]);
        double analytic = AnalyticCapFloorEngine.Calculate(cap, Context, new Vasicek(0.04, 0.1, 0.04, 0.01), Curve).NPV!.Value;
        Assert.True(Tolerance.Within(3001.1439405547217, analytic, 1e6), $"{analytic}");
    }

    private static ImmutableArray<CashFlow> Leg()
    {
        var calendar = new TARGET();
        Date spot = calendar.Advance(Today, 2, TimeUnit.Days);
        Date start = calendar.Advance(spot, new Period(6, TimeUnit.Months));
        var schedule = new Schedule(
            start, calendar.Advance(start, new Period(1, TimeUnit.Years)), new Period(6, TimeUnit.Months), calendar,
            BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, Context);
        return new IborLeg(schedule, new Euribor6M(Curve)).WithNotionals(1e6).ToLeg(Context);
    }
}
