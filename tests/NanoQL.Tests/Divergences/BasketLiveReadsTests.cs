// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A basket's live reads by the live names, not by the live list's positions (the S-xcr plan's XR8 (ii)): QuantLib's
/// <c>remainingNotionals(d)</c> and <c>remainingProbabilities(d)</c> read the first live-list-length positions of the
/// pool (<c>basket.cpp:196-220</c>), so after the first of three names defaults they read the first two names. QuantLib's
/// values are the planner's <c>xp_basket.cpp</c> (D2, D3); the check is QuantLib on the basket of the live names alone,
/// whose reads are those names' notionals and curves.
/// </summary>
public class BasketLiveReadsTests
{
    [Fact]
    public void TheLiveReadsTakeTheLiveNames()
    {
        // QuantLib, X defaulted and Y, Z alive: remainingNotionals 10, 20 and remainingProbabilities 0.0295810536,
        // 0.0582870685 (X's and Y's); Y and Z's are 20, 30 and 0.0582870685, 0.0861439293
        var d = new Date(31, August, 2009);
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.01), Flat(Today, 0.02), Flat(Today, 0.03)];
        Pool pool = PoolOf(["X", "Y", "Z"], curves, [[June2006Bankruptcy()], null, null]);
        var basket = new Basket(new Date(1, January, 2006), ["X", "Y", "Z"], [10.0, 20.0, 30.0], pool, Context);

        Assert.Equal(new[] { 1, 2 }, basket.LiveList(d));
        Assert.Equal(new[] { 20.0, 30.0 }, basket.RemainingNotionals(d));
        Assert.Equal(new[] { 20.0, 30.0 }, basket.RemainingNotionals());
        Assert.Equal(new[] { curves[1].DefaultProbability(d, true), curves[2].DefaultProbability(d, true) }, basket.RemainingProbabilities(d));
        Assert.Equal(0.0861439293, basket.RemainingProbabilities(d)[1], 1e-10);
    }
}
