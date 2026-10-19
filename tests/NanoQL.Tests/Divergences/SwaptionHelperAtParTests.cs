// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// When a swaption helper reads the at-par switch. QuantLib's helper builds its swaps, whose Ibor legs read the global
/// switch, at its first calculation and again at each recalculation after a notification
/// (<c>swaptionhelper.cpp:152-199, 212-214</c>; <c>calibrationhelper.hpp:73</c>), so a helper built at par gives the
/// indexed swap's values once the switch is flipped and its quote moves (the wheel: a 1Y x 5Y helper's market value
/// 0.011261310927040481 at par, then 0.011261495561724687, the value of a helper built with indexed coupons). nanoql's
/// helper builds its swaps under the context it is given at construction, and keeps them: its prices under a context
/// with the other switch are its own swap's.
/// </summary>
public class SwaptionHelperAtParTests
{
    [Fact]
    public void TheAtParSwitchIsReadAtConstruction()
    {
        var atPar = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2025) };
        EvaluationContext indexed = atPar with { UseAtParCoupons = false };
        var discount = new FlatForward(atPar.EvaluationDate, 0.027, new Actual365Fixed());
        var forecast = new FlatForward(atPar.EvaluationDate, 0.031, new Actual365Fixed());
        var index = new IborIndex(
            "Euribor",
            new Period(6, TimeUnit.Months),
            2,
            null,
            new TARGET(),
            BusinessDayConvention.ModifiedFollowing,
            true,
            new Actual360(),
            forecast);

        SwaptionHelper Helper(EvaluationContext context) => new(
            new Period(1, TimeUnit.Years),
            new Period(5, TimeUnit.Years),
            0.2,
            index,
            new Period(1, TimeUnit.Years),
            new Thirty360(Thirty360Convention.BondBasis),
            new Actual360(),
            discount,
            context);

        // QuantLib's values for a helper built at par and one built with indexed coupons
        SwaptionHelper builtAtPar = Helper(atPar);
        Assert.Equal(0.011261310927040481, builtAtPar.MarketValue);
        Assert.Equal(0.0314712870575523, builtAtPar.Underlying.FixedRate);
        SwaptionHelper builtIndexed = Helper(indexed);
        Assert.Equal(0.011261495561724687, builtIndexed.MarketValue);
        Assert.Equal(0.03147180304465045, builtIndexed.Underlying.FixedRate);

        // Under the indexed context, the helper built at par prices its own swap, where QuantLib's, recalculated after
        // the switch flips, gives the indexed one's 0.011261495561724687.
        Assert.Equal(builtAtPar.MarketValue, builtAtPar.BlackPrice(0.2, indexed));
        Assert.Equal(0.0314712870575523, builtAtPar.Underlying.FixedRate);
    }
}
