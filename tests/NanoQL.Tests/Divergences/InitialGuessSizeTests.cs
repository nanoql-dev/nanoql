// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A global bootstrap's initial-guess function that gives fewer values than the curve has nodes after the first is
/// refused when the curve is built, with <see cref="NanoQLException"/> "initial guess has n values but the curve has m
/// pillars", QuantLib-Python's wording. QuantLib's optimizer starts from the guess, and the bootstrap writes each node
/// from the optimizer's argument, whose size is the guess's (<c>globalbootstrap.hpp:394, 442-448</c>), reading past
/// its end, a result C++ leaves undefined; the wheel's guess function refuses a guess of any other size than the
/// nodes'. A longer guess is QuantLib's defined path where the traits' transforms don't read a node's time (the
/// discount traits', and the zero-yield and forward-rate traits', which have none), kept: its extra values are
/// variables no error term reads. The simple-zero traits' transforms read each node's time
/// (<c>bootstraptraits.hpp:408-416</c>), and for a longer guess QuantLib reads past its times, which nanoql refuses,
/// <see cref="NanoQLException"/> "initial guess longer than the curve's m pillars: the simple-zero transform reads
/// node k".
/// </summary>
public class InitialGuessSizeTests
{
    private static readonly Date Today = new(15, Month.January, 2024);
    private static readonly Date Spot = new(17, Month.January, 2024);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void AGuessShorterThanTheCurvesPillarsIsRefused()
    {
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => new PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap>(
                Spot,
                Helpers(),
                new Actual365Fixed(),
                Context,
                bootstrap: new GlobalBootstrap(initialGuessFn: (_, _) => new Vector(3, 0.95))));
        Assert.Equal("initial guess has 3 values but the curve has 4 pillars", e.Message);
    }

    [Fact]
    public void ALongerGuessUnderTheSimpleZeroTraitsIsRefused()
    {
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => new PiecewiseYieldCurve<SimpleZeroYield, Linear, GlobalBootstrap>(
                Spot,
                Helpers(),
                new Actual365Fixed(),
                Context,
                bootstrap: new GlobalBootstrap(initialGuessFn: (_, _) => new Vector(6, 0.03))));
        Assert.Equal(
            "initial guess longer than the curve's 4 pillars: the simple-zero transform reads node 5", e.Message);
    }

    // A six-month deposit and three swaps: four pillars after the reference date.
    private static List<RateHelper> Helpers()
    {
        var helpers = new List<RateHelper>
        {
            new DepositRateHelper(
                0.030, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
                new Actual360(), Context),
        };
        foreach ((int years, double rate) in new[] { (2, 0.031), (5, 0.032), (10, 0.033) })
        {
            helpers.Add(new SwapRateHelper(
                rate,
                new Period(years, TimeUnit.Years),
                new TARGET(),
                Frequency.Annual,
                BusinessDayConvention.Unadjusted,
                new Thirty360(Thirty360Convention.BondBasis),
                new Euribor6M(),
                Context));
        }

        return helpers;
    }
}
