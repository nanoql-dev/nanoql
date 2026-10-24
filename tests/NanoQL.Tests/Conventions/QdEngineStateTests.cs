// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The QD engines' state (the S-van plan's VD7). QuantLib builds QD+'s exercise boundary inside each calculation
/// (<c>qdplusamericanengine.cpp:350-374</c>), so an engine object keeps nothing between prices; nanoql's likewise, its
/// boundary evaluator a working object per solve. The fixed-point engine's three named schemes are function statics in
/// QuantLib (<c>qdfpamericanengine.cpp:411-427</c>); nanoql's are static singletons, immutable, so threads share them.
/// </summary>
public class QdEngineStateTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AQdPlusEngineObjectKeepsNothingBetweenPrices()
    {
        // one engine object pricing a put, another strike, and the put again: each a fresh engine's bits
        var engine = new QdPlusAmericanEngine(Process(100.0, 0.25));
        double first = engine.Calculate(Put(100.0), Context).NPV!.Value;
        Assert.Equal(
            QdPlusAmericanEngine.Calculate(Put(80.0), Context, Process(100.0, 0.25)).NPV!.Value,
            engine.Calculate(Put(80.0), Context).NPV!.Value);
        Assert.Equal(first, engine.Calculate(Put(100.0), Context).NPV!.Value);
        Assert.Equal(first, QdPlusAmericanEngine.Calculate(Put(100.0), Context, Process(100.0, 0.25)).NPV!.Value);
    }

    [Fact]
    public void TheNamedSchemesAreSingletons()
    {
        Assert.Same(QdFpAmericanEngine.FastScheme(), QdFpAmericanEngine.FastScheme());
        Assert.Same(QdFpAmericanEngine.AccurateScheme(), QdFpAmericanEngine.AccurateScheme());
        Assert.Same(QdFpAmericanEngine.HighPrecisionScheme(), QdFpAmericanEngine.HighPrecisionScheme());
    }

    [Fact]
    public void EightThreadsSharingASchemeGiveOnePrice()
    {
        double one = QdFpAmericanEngine.Calculate(Put(100.0), Context, Process(100.0, 0.25), QdFpAmericanEngine.HighPrecisionScheme())
            .NPV!.Value;
        var prices = new double[8];
        Parallel.For(
            0,
            prices.Length,
            i => prices[i] = QdFpAmericanEngine.Calculate(
                Put(100.0), Context, Process(100.0, 0.25), QdFpAmericanEngine.HighPrecisionScheme()).NPV!.Value);
        Assert.All(prices, p => Assert.Equal(one, p));
    }

    private static GeneralizedBlackScholesProcess Process(double spot, double vol) =>
        new BlackScholesMertonProcess(
            spot, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365), new BlackConstantVol(Today, new NullCalendar(), vol, A365));

    private static VanillaOption Put(double strike) =>
        new(new PlainVanillaPayoff(OptionType.Put, strike), new AmericanExercise(Today, Today + 365));
}
