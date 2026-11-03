// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The calibration basket from the engine object a caller passes: QuantLib's instrument reads the engine it holds,
/// nanoql's <see cref="NonstandardSwaption.CalibrationBasket"/> the one passed, its model and discount curve. On the
/// wheel, a Bermudan payer exercised in one, two and three years into a 5-year amortizing swap at 3.5% (fixed nominals
/// 1,000,000 to 200,000, annual; Euribor 6M), its engine on a GSR model (a = 0.03, σ = 0.008) on a flat 3% curve with a
/// 2.8% discount curve, the standard swaps the 10-year EUR swap index's at 20%: the naive basket is coterminal at the
/// money on a nominal of 1; the delta-gamma basket matures on 20 August 2029, 21 January 2030 and 20 May 2030 on
/// nominals 834084.0070174834, 676859.2375139953 and 516398.05710079614 at 0.03497210203627414, 0.035009776513416094
/// and 0.03489800698686514, where an engine on the model a = σ = 0.01 without the discount curve gives its first helper
/// 19 September 2029 on 823939.5255605449.
/// </summary>
public class CalibrationBasketTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());
    private static readonly TARGET Calendar = new();

    [Fact]
    public void TheNaiveBasketIsCoterminalAtTheMoney()
    {
        ImmutableArray<SwaptionHelper> basket = Basket(BasketGeneratingEngineCalibrationBasketType.Naive);
        double[] strikes = [0.03047147232859789, 0.03047603150610415, 0.03048363218808705];
        Assert.Equal(3, basket.Length);
        for (int i = 0; i < basket.Length; i++)
        {
            Assert.Equal(Calendar.Advance(Today, i + 1, TimeUnit.Years), basket[i].Swaption.Exercise.LastDate());
            Assert.Equal(new Date(20, Month.January, 2031), basket[i].Underlying.MaturityDate());
            Assert.Equal(1.0, basket[i].Underlying.Nominal());
            Assert.True(Math.Abs(basket[i].Underlying.FixedRate - strikes[i]) <= 1e-15, $"{i}: {basket[i].Underlying.FixedRate}");
            Assert.Equal(0.2, basket[i].Volatility);
        }
    }

    [Fact]
    public void TheDeltaGammaBasketReadsTheEnginesModel()
    {
        ImmutableArray<SwaptionHelper> basket = Basket(BasketGeneratingEngineCalibrationBasketType.MaturityStrikeByDeltaGamma);
        Date[] maturities = [new(20, Month.August, 2029), new(21, Month.January, 2030), new(20, Month.May, 2030)];
        double[] nominals = [834084.0070174834, 676859.2375139953, 516398.05710079614];
        double[] strikes = [0.03497210203627414, 0.035009776513416094, 0.03489800698686514];
        Assert.Equal(3, basket.Length);
        for (int i = 0; i < basket.Length; i++)
        {
            Assert.Equal(Calendar.Advance(Today, i + 1, TimeUnit.Years), basket[i].Swaption.Exercise.LastDate());
            Assert.Equal(maturities[i], basket[i].Underlying.MaturityDate());
            Assert.True(Math.Abs((basket[i].Underlying.Nominal() / nominals[i]) - 1.0) <= 1e-12, $"{i}: {basket[i].Underlying.Nominal()}");
            Assert.True(Math.Abs(basket[i].Underlying.FixedRate - strikes[i]) <= 1e-14, $"{i}: {basket[i].Underlying.FixedRate}");
        }
    }

    private static ImmutableArray<SwaptionHelper> Basket(BasketGeneratingEngineCalibrationBasketType type)
    {
        Date start = Calendar.Advance(Calendar.Advance(Today, 1, TimeUnit.Years), 2, TimeUnit.Days);
        Schedule Schedule(int months) => new(
            start,
            Calendar.Advance(start, 5, TimeUnit.Years),
            new Period(months, TimeUnit.Months),
            Calendar,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Forward,
            false,
            Context);
        double[] fixedNominals = [1e6, 8e5, 6e5, 4e5, 2e5];
        var swap = new NonstandardSwap(
            SwapType.Payer,
            fixedNominals,
            fixedNominals.SelectMany(n => new[] { n, n }),
            Schedule(12),
            Enumerable.Repeat(0.035, 5),
            new Thirty360(Thirty360Convention.BondBasis),
            Schedule(6),
            new Euribor6M(Curve),
            1.0,
            0.0,
            new Actual360(),
            Context,
            paymentConvention: BusinessDayConvention.ModifiedFollowing);
        var swaption = new NonstandardSwaption(swap, new BermudanExercise([.. new[] { 1, 2, 3 }.Select(i => Calendar.Advance(Today, i, TimeUnit.Years))]));
        var engine = new Gaussian1dNonstandardSwaptionEngine(
            Gaussian1dInputs.Constant(Curve, 0.03, 0.008), discountCurve: new FlatForward(Today, 0.028, new Actual365Fixed()));
        SwaptionVolatilityStructure vol = ConstantSwaptionVolatility.Create(
            0, Calendar, BusinessDayConvention.ModifiedFollowing, 0.2, new Actual365Fixed(), Context);
        return swaption.CalibrationBasket(engine, new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), Curve, Curve), vol, Context, type);
    }
}
