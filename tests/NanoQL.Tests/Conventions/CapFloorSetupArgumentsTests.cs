// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A cash flow of the leg that isn't a floating-rate coupon is refused when the instrument is priced, not when it is
/// built: QuantLib's check, "non-FloatingRateCoupon given", is in <c>setupArguments</c> (<c>capfloor.cpp:233-236</c>),
/// which <c>Instrument::calculate</c> reaches after the expiry check (<c>instrument.hpp:130-153</c>), so a cap on a
/// fixed-rate leg builds, answers its inspectors, and refuses with <see cref="NanoQLException"/> in every engine and in
/// its implied volatility, while an expired one prices at 0 without the check, as in QuantLib.
/// </summary>
public class CapFloorSetupArgumentsTests
{
    [Fact]
    public void ANonFloatingCouponIsRefusedAtPricing()
    {
        var today = new Date(16, Month.June, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var curve = new FlatForward(today, 0.04, new Actual365Fixed());
        ImmutableArray<CashFlow> FixedLeg(Date start) => new FixedRateLeg(new Schedule(
                start, start + new Period(1, TimeUnit.Years), new Period(6, TimeUnit.Months), new TARGET(),
                BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward,
                false, context))
            .WithNotionals([1.0])
            .WithCouponRates([0.03], new Actual365Fixed())
            .ToLeg();

        // It builds and answers what doesn't price.
        var cap = new Cap(FixedLeg(new Date(18, Month.June, 2025)), [0.03]);
        Assert.False(cap.IsExpired(context));
        Assert.Equal(new Date(18, Month.June, 2025), cap.StartDate());
        Assert.Null(cap.LastFloatingRateCoupon());
        Assert.Equal(0.03, cap.AtmRate(curve, context), 1e-12);

        // It is refused when priced.
        const string text = "non-FloatingRateCoupon given";
        Assert.Equal(text, Assert.Throws<NanoQLException>(() => BlackCapFloorEngine.Calculate(cap, context, curve, 0.2)).Message);
        Assert.Equal(text, Assert.Throws<NanoQLException>(() => BachelierCapFloorEngine.Calculate(cap, context, curve, 0.01)).Message);
        Assert.Equal(text, Assert.Throws<NanoQLException>(() => cap.ImpliedVolatility(0.001, curve, 0.2, context)).Message);

        // An expired one isn't: its results are the expired ones.
        var expired = new Floor(FixedLeg(new Date(18, Month.June, 2023)), [0.03]);
        Assert.True(expired.IsExpired(context));
        Assert.Equal(0.0, BlackCapFloorEngine.Calculate(expired, context, curve, 0.2).NPV);
    }
}
