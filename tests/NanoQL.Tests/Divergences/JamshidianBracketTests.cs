// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Gaussian1d Jamshidian engine's bracket: QuantLib solves for y* on the fixed bracket [−8, 8]
/// (<c>gaussian1djamshidianswaptionengine.cpp:142-147</c>) and refuses short-dated swaptions away from the money: on
/// the wheel, on a GSR model (a = 0.02, σ = 0.01) on a flat 3% curve, a 1-day payer on a 5-year swap at 1% throws "root
/// not bracketed: f[-8,8] -> [7.575587e-02,1.111667e-01]", as does a 1-day receiver at 6% and a 1-month receiver at 6%
/// ("[-2.604803e-01,-2.535408e-02]"), which Hull-White's Jamshidian engine on the equal constant-parameter model prices
/// at 0.09363555834330271, 0.13506769382915945 and 0.13466680676149342. nanoql's bracket is QuantLib's first and is
/// doubled only where it doesn't bracket the root, so where QuantLib brackets, its solver's call and path are
/// QuantLib's. QuantLib's checks: Hull-White's Jamshidian engine and QuantLib's own values where it prices.
/// </summary>
public class JamshidianBracketTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());
    private static readonly Gsr Model = Gaussian1dInputs.Constant(Curve, 0.02, 0.01);

    [Fact]
    public void ShortDatedSwaptionsAwayFromTheMoneyArePriced()
    {
        var calendar = new TARGET();
        Date oneDay = calendar.Advance(Today, 1, TimeUnit.Days);
        Date oneMonth = calendar.Advance(Today, 1, TimeUnit.Months);
        foreach ((Date exercise, double strike, SwapType type, double hullWhite) in new[]
        {
            (oneDay, 0.01, SwapType.Payer, 0.09363555834330271),
            (oneDay, 0.06, SwapType.Receiver, 0.13506769382915945),
            (oneMonth, 0.06, SwapType.Receiver, 0.13466680676149342),
        })
        {
            double price = Price(exercise, strike, type);
            Assert.True(Math.Abs((price / hullWhite) - 1.0) <= 2e-7, $"{exercise} {strike} {type}: {price} against {hullWhite}");
            double check = JamshidianSwaptionEngine.Calculate(Swaption(exercise, strike, type), Context, new HullWhite(Curve, 0.02, 0.01)).NPV!.Value;
            Assert.True(Math.Abs((check / hullWhite) - 1.0) <= 1e-13, $"{check}");
        }

        // out of the money, both 0
        Assert.True(Math.Abs(Price(oneDay, 0.06, SwapType.Payer)) <= 1e-15);
    }

    [Fact]
    public void WhereQuantLibBracketsItsPathStands()
    {
        var calendar = new TARGET();
        double year = Price(calendar.Advance(Today, 1, TimeUnit.Years), 0.03, SwapType.Payer);
        Assert.True(Math.Abs((year / 0.018289342688119454) - 1.0) <= 1e-13, $"{year}");
        double month = Price(calendar.Advance(Today, 1, TimeUnit.Months), 0.01, SwapType.Payer);
        Assert.True(Math.Abs((month / 0.09335755991668652) - 1.0) <= 1e-13, $"{month}");
    }

    private static Swaption Swaption(Date exercise, double strike, SwapType type) =>
        new(Gaussian1dInputs.SwapOf(new Euribor6M(Curve), exercise, 5, strike, type, Context), new EuropeanExercise(exercise));

    private static double Price(Date exercise, double strike, SwapType type) =>
        Gaussian1dJamshidianSwaptionEngine.Calculate(Swaption(exercise, strike, type), Context, Model).NPV!.Value;
}
