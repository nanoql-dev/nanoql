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
/// The Gaussian1d cap/floor engine's discount curve (the S-g1d plan's G1R6 (ii)): QuantLib's engine discounts the
/// floating leg on the discount curve but the strike's leg on the model curve
/// (<c>gaussian1dcapfloorengine.cpp:86-89</c>,
/// <c>:158-161</c>), where its swaption engine discounts both on the discount curve. On the wheel, on a GSR model (a =
/// σ = 0.01) on a flat 3% Actual/365 (Fixed) curve, a caplet at 3% on 1,000,000 of Euribor 6M fixing in a year is worth
/// 1934.325172314739 with a 2% discount curve, where QuantLib's swaption engine gives the one-period swaption into the
/// same coupon against 3% 1898.5255274075153; without the discount curve the caplet's 1870.0808332724143 is the
/// swaption's. nanoql discounts the strike's leg on the discount curve. QuantLib's check: its swaption engine on the
/// one-period swaption.
/// </summary>
public class Gaussian1dCapFloorDiscountTests
{
    [Fact]
    public void TheStrikesLegIsDiscountedOnTheDiscountCurve()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.03, new Actual365Fixed());
        YieldTermStructure discount = new FlatForward(today, 0.02, new Actual365Fixed());
        var index = new Euribor6M(curve);
        Gsr model = Gaussian1dInputs.Constant(curve, 0.01, 0.01);
        var calendar = new TARGET();
        Date start = index.ValueDate(calendar.Advance(today, 1, TimeUnit.Years));
        ImmutableArray<CashFlow> leg = new IborLeg(new Schedule([start, index.MaturityDate(start)], calendar), index)
            .WithNotionals(1e6)
            .WithPaymentDayCounter(new Actual360())
            .ToLeg(context);
        var caplet = new Cap(leg, [0.03]);

        double price = Gaussian1dCapFloorEngine.Calculate(caplet, context, model, discountCurve: discount).NPV!.Value;
        Assert.True(Math.Abs((price / 1898.5255274075153) - 1.0) <= 1e-12, $"{price}");
        Assert.True(Math.Abs((price / 1934.325172314739) - 1.0) > 1e-2, $"{price}");

        // without the discount curve, QuantLib's own
        double own = Gaussian1dCapFloorEngine.Calculate(caplet, context, model).NPV!.Value;
        Assert.True(Math.Abs((own / 1870.0808332724143) - 1.0) <= 1e-12, $"{own}");
    }
}
