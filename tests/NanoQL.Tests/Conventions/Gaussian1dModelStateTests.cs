// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Gaussian1d models' state (the S-g1d plan's G1R1 (iii); master plan §2.2's Gaussian1d row): QuantLib's model
/// keeps its swaps and GSR's process core its integrals for the model's life, flushed at every parameter change; here a
/// memo lives for one call, so prices on one model are each their own: in either order, from several threads at once,
/// and a public member before and after an engine call give the same bits.
/// </summary>
public class Gaussian1dModelStateTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());
    private static readonly TARGET Calendar = new();

    [Fact]
    public void TwoPricesInEitherOrderGiveTheSameBits()
    {
        Gsr first = Model();
        double bermudanFirst = Price(first, Bermudan());
        double europeanSecond = Price(first, European());

        Gsr second = Model();
        double europeanFirst = Price(second, European());
        double bermudanSecond = Price(second, Bermudan());

        Assert.Equal(bermudanFirst, bermudanSecond);
        Assert.Equal(europeanFirst, europeanSecond);
    }

    [Fact]
    public void OneModelPricedFromEightThreadsGivesOnePrice()
    {
        Gsr model = Model();
        Swaption swaption = Bermudan();
        double expected = Price(Model(), swaption);
        var prices = new double[8];
        Parallel.For(0, prices.Length, new ParallelOptions { MaxDegreeOfParallelism = prices.Length }, i => prices[i] = Price(model, swaption));
        Assert.All(prices, p => Assert.Equal(expected, p));
    }

    [Fact]
    public void APublicMemberGivesTheSameBitsBeforeAndAfterAnEngineCall()
    {
        Gsr model = Model();
        Date maturity = Calendar.Advance(Today, 7, TimeUnit.Years);
        Date reference = Calendar.Advance(Today, 2, TimeUnit.Years);
        double zerobondBefore = model.Zerobond(maturity, reference, 0.7);
        double numeraireBefore = model.Numeraire(3.0, -0.4);
        _ = Price(model, Bermudan());
        Assert.Equal(zerobondBefore, model.Zerobond(maturity, reference, 0.7));
        Assert.Equal(numeraireBefore, model.Numeraire(3.0, -0.4));
        Assert.Equal(zerobondBefore, Model().Zerobond(maturity, reference, 0.7));
    }

    private static Gsr Model() =>
        new(Curve, [Calendar.Advance(Today, 1, TimeUnit.Years), Calendar.Advance(Today, 3, TimeUnit.Years)], [0.008, 0.01, 0.009], [0.02, 0.03, 0.025]);

    private static double Price(Gsr model, Swaption swaption) => Gaussian1dSwaptionEngine.Calculate(swaption, Context, model).NPV!.Value;

    private static Swaption European()
    {
        Date exercise = Calendar.Advance(Today, 2, TimeUnit.Years);
        return new Swaption(Gaussian1dInputs.SwapOf(new Euribor6M(Curve), exercise, 5, 0.03, SwapType.Payer, Context), new EuropeanExercise(exercise));
    }

    private static Swaption Bermudan()
    {
        Date exercise = Calendar.Advance(Today, 1, TimeUnit.Years);
        Date[] dates = [exercise, Calendar.Advance(exercise, 1, TimeUnit.Years), Calendar.Advance(exercise, 2, TimeUnit.Years)];
        return new Swaption(Gaussian1dInputs.SwapOf(new Euribor6M(Curve), exercise, 5, 0.03, SwapType.Receiver, Context), new BermudanExercise(dates));
    }
}
