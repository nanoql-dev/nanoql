// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The last floating-rate coupon of a cap on an empty leg. QuantLib's <c>lastFloatingRateCoupon()</c> reads
/// <c>floatingLeg_.back()</c> (<c>capfloor.cpp:187-193</c>), which is undefined for an empty leg; the wheel doesn't bind
/// it. nanoql refuses with <see cref="NanoQLException"/> and the text QuantLib's <c>CashFlows</c> gives an empty leg,
/// "empty leg", as the wheel's <c>startDate()</c> and <c>maturityDate()</c> on the same cap do. The rest of the cap is
/// QuantLib's defined behaviour: it is expired, prices at 0 and has an ATM rate of 0.
/// </summary>
public class CapFloorEmptyLegTests
{
    [Fact]
    public void TheLastCouponOfAnEmptyLegIsRefused()
    {
        var today = new Date(16, Month.June, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var cap = new Cap([], [0.03]);

        Assert.Equal("empty leg", Assert.Throws<NanoQLException>(() => cap.LastFloatingRateCoupon()).Message);

        // QuantLib's defined answers on the same cap: the wheel's texts and values.
        Assert.StartsWith("empty leg", Assert.Throws<ArgumentException>(() => cap.StartDate()).Message, StringComparison.Ordinal);
        Assert.StartsWith("empty leg", Assert.Throws<ArgumentException>(() => cap.MaturityDate()).Message, StringComparison.Ordinal);
        Assert.True(cap.IsExpired(context));
        var curve = new FlatForward(today, 0.04, new Actual365Fixed());
        Assert.Equal(0.0, BlackCapFloorEngine.Calculate(cap, context, curve, 0.2).NPV);
        Assert.Equal(0.0, cap.AtmRate(curve, context));
    }
}
