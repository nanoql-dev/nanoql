// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Gaussian1d cap/floor engine's at-the-money forwards (the S-g1d plan's G1R6 (iii)): QuantLib's engine declares
/// <c>optionletsAtmForward</c> and leaves it 0 (<c>gaussian1dcapfloorengine.cpp:33</c>, <c>:219</c>): on the wheel,
/// a 3-year cap at 3% on Euribor 6M from a year out, on a GSR model on a flat 3% curve, reports six zeros. nanoql
/// reports each coupon's forward, as Black's engine does. QuantLib's check: Black's engine's results.
/// </summary>
public class Gaussian1dCapFloorResultsTests
{
    [Fact]
    public void TheAtmForwardsAreTheCoupons()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.03, new Actual365Fixed());
        var index = new Euribor6M(curve);
        var calendar = new TARGET();
        var schedule = new Schedule(
            calendar.Advance(today, 1, TimeUnit.Years),
            calendar.Advance(today, 4, TimeUnit.Years),
            new Period(6, TimeUnit.Months),
            calendar,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Forward,
            false,
            context);
        ImmutableArray<CashFlow> leg = new IborLeg(schedule, index).WithNotionals(1e6).WithPaymentDayCounter(new Actual360()).ToLeg(context);
        var cap = new Cap(leg, [0.03]);

        ImmutableArray<double?> forwards = Gaussian1dCapFloorEngine.Calculate(cap, context, Gaussian1dInputs.Constant(curve, 0.01, 0.01))
            .Result<ImmutableArray<double?>>("optionletsAtmForward");
        ImmutableArray<double?> black = BlackCapFloorEngine.Calculate(cap, context, curve, 0.2).Result<ImmutableArray<double?>>("optionletsAtmForward");
        Assert.Equal(6, forwards.Length);
        Assert.Equal(black, forwards);

        // QuantLib's zeros
        Assert.All(forwards, f => Assert.True(f > 0.02, $"{f}"));
    }
}
