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
/// The Monte Carlo Hull-White cap engine on a collar: QuantLib's path pricer reads the cap rates only for a cap
/// (<c>mchullwhiteengine.cpp:54</c>, <c>:94-99</c>), so it prices a collar as its floor alone, long: on the C++
/// harness, a 3-year collar at 3.5% / 2.5% on Euribor 6M from the spot date, notional 1,000,000, on Hull-White
/// (0.05, 0.01) on a flat 2.7% curve, 2,000 samples, seed 42, prices 8,734.001429909456, its floor's price, where its
/// cap prices 3,713.709005573455. nanoql prices the collar as its cap less its floor, as the analytic and tree engines
/// do. QuantLib's check: its own cap and floor on the same paths.
/// </summary>
public class McHullWhiteCollarTests
{
    [Fact]
    public void TheCollarIsItsCapLessItsFloor()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var discount = new FlatForward(today, 0.027, new Actual365Fixed());
        var index = new Euribor6M(new FlatForward(today, 0.031, new Actual365Fixed()));
        var calendar = new TARGET();
        Date spot = calendar.Advance(today, 2, TimeUnit.Days);
        var schedule = new Schedule(
            spot, calendar.Advance(spot, new Period(3, TimeUnit.Years), BusinessDayConvention.ModifiedFollowing),
            new Period(6, TimeUnit.Months), calendar, BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Forward, false, context);
        ImmutableArray<CashFlow> leg = new IborLeg(schedule, index)
            .WithNotionals(1e6)
            .WithPaymentDayCounter(index.DayCounter)
            .WithPaymentAdjustment(BusinessDayConvention.ModifiedFollowing)
            .WithFixingDays(2)
            .ToLeg(context);
        var model = new HullWhite(discount, 0.05, 0.01);

        double Price(CapFloor c) => MCHullWhiteCapFloorEngine.Calculate<PseudoRandom, GeneralStatistics>(
            c, context, model, false, false, 2000, null, null, 42).NPV!.Value;
        double cap = Price(new Cap(leg, [0.035])), floor = Price(new Floor(leg, [0.025]));
        double collar = Price(new Collar(leg, [0.035], [0.025]));

        Assert.True(Tolerance.Within(3713.709005573455, cap, 1e6), $"{cap}");
        Assert.True(Tolerance.Within(8734.001429909456, floor, 1e6), $"{floor}");
        Assert.True(Math.Abs(collar - (cap - floor)) <= 1e-9, $"{collar} against {cap - floor}");
    }
}
