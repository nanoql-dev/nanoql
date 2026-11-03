// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The FD Heston-Hull-White engine with cash dividends: <c>FdmDividendHandler</c>'s three-dimensional defect
/// (<c>Divergences/FdmDividendHandler3dTests.cs</c>) reaching the engine. QuantLib's handler moves, on the engine's
/// grid of the log spot, the variance and the short rate, only the lines through the other directions' first points, so
/// most of the grid misses each dividend: an American call with dividends of 2 and 3 is 8.768 at a vanishing short-rate
/// volatility, where QuantLib's FD Heston engine gives 6.738 (QuantLib-Python 1.44). nanoql's handler moves every line,
/// and the engine there is the FD Heston engine's price.
/// </summary>
public class FdHestonHullWhiteDividendsTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AtAVanishingRateVolatilityTheDividendsAreTheFiniteDifferenceHestonEnginesPrice()
    {
        YieldTermStructure r = new FlatForward(Today, 0.05, A365);
        var model = new HestonModel(new HestonProcess(r, new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, 0.4, -0.6));
        ImmutableArray<Dividend> dividends = DividendFunctions.DividendVector([Today + 91, Today + 273], [2.0, 3.0]);
        var call = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new AmericanExercise(Today, Today + 365));
        double npv = FdHestonHullWhiteVanillaEngine.Calculate(
            call, Context, model, new HullWhiteProcess(r, 0.1, 1e-6), dividends, 0.0, 50, 100, 20, 11, 0, false).NPV!.Value;

        // QuantLib's FD Heston engine 6.737990305135711; its FD Heston-Hull-White engine 8.767966996691234
        Assert.True(Math.Abs(npv - 6.737990305135711) <= 0.02, $"{npv}");
    }
}
