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
/// A caplet fixed before today on the Gaussian1d cap/floor engine (the S-g1d plan's G1R6 (iv)): QuantLib's engine
/// values its known payoff undiscounted (<c>gaussian1dcapfloorengine.cpp:63-66</c>, <c>:138-141</c>). On the wheel, a
/// caplet at 2.5% on 1,000,000 of Euribor 6M fixed at 3.15% on 16 December 2024, paid on 18 June 2025 (accrual
/// 182/360), seen from 15 January 2025, is worth 3286.1111111111104 on a GSR model on a flat 3% curve, where Black's
/// engine gives 3244.7791791813, the payoff discounted to the payment. nanoql discounts it. QuantLib's check: Black's
/// engine's value of the known payoff.
/// </summary>
public class Gaussian1dCapFloorFixedCapletTests
{
    [Fact]
    public void TheKnownPayoffIsDiscounted()
    {
        var today = new Date(15, Month.January, 2025);
        YieldTermStructure curve = new FlatForward(today, 0.03, new Actual365Fixed());
        var index = new Euribor6M(curve);
        var calendar = new TARGET();
        Date fixing = calendar.Advance(today, -1, TimeUnit.Months);
        var plain = new EvaluationContext { EvaluationDate = today };
        var context = new EvaluationContext { EvaluationDate = today, Fixings = plain.Fixings.AddFixing(index, fixing, 0.0315) };
        Date start = index.ValueDate(fixing);
        ImmutableArray<CashFlow> leg = new IborLeg(new Schedule([start, index.MaturityDate(start)], calendar), index)
            .WithNotionals(1e6)
            .WithPaymentDayCounter(new Actual360())
            .ToLeg(context);
        var caplet = new Cap(leg, [0.025]);

        double price = Gaussian1dCapFloorEngine.Calculate(caplet, context, Gaussian1dInputs.Constant(curve, 0.01, 0.01)).NPV!.Value;
        double black = BlackCapFloorEngine.Calculate(caplet, context, curve, 0.2).NPV!.Value;
        Assert.True(Math.Abs((price / black) - 1.0) <= 1e-14, $"{price} against Black's {black}");
        Assert.True(Math.Abs((price / 3244.7791791813) - 1.0) <= 1e-12, $"{price}");

        // QuantLib's, undiscounted
        Assert.True(Math.Abs((price / 3286.1111111111104) - 1.0) > 1e-2, $"{price}");
    }
}
