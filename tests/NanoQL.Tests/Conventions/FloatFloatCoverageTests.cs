// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// QuantLib's master's coverage of the Gaussian1d float-float swaption (#2845, after the pin; the S-g1d plan's G1R5
/// (iv)): <c>testGaussian1dFloatFloatSwaptionAndCalibrationBasket</c> and
/// <c>testGaussian1dFloatFloatSwaptionCoverage</c> (master's <c>test-suite/floatfloatswap.cpp</c>), their checks that
/// hold on 1.44, on its fixture: Euribor 3M against 6M, 100 on each leg, a flat 5% from the settlement date, GSR (σ =
/// 0.01, a = 0.03), exercises in two and three years. The evaluation date is fixed at 15 January 2025, where master's
/// test runs on the day's; its values at 1e-10 (4.1503974670013793e-06 and the rest) are that day's, so they are left
/// out, as are its spread index legs, which wait for S-cms's <c>CmsSpreadLeg</c>.
/// </summary>
public class FloatFloatCoverageTests
{
    private static readonly Calendar Cal = new TARGET();
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly Date Settlement = Cal.Advance(Today, 2, TimeUnit.Days);
    private static readonly YieldTermStructure Curve = new FlatForward(Settlement, 0.05, new Actual365Fixed());
    private static readonly IborIndex Index1 = new Euribor(new Period(3, TimeUnit.Months), Curve);
    private static readonly IborIndex Index2 = new Euribor(new Period(6, TimeUnit.Months), Curve);
    private static readonly Gsr Model = new(Curve, [], [0.01], 0.03);
    private static readonly BermudanExercise Exercise = new([Cal.Advance(Today, 2, TimeUnit.Years), Cal.Advance(Today, 3, TimeUnit.Years)]);

    [Fact]
    public void TheSwaptionAndItsCalibrationBaskets()
    {
        var swaption = new FloatFloatSwaption(
            new FloatFloatSwap(SwapType.Payer, 100.0, 100.0, Schedule(3), Index1, Index1.DayCounter, Schedule(6), Index2, Index2.DayCounter, Context),
            Exercise);
        var engine = new Gaussian1dFloatFloatSwaptionEngine(Model, 16, 5.0);
        InstrumentResults results = engine.Calculate(swaption, Context);
        SwaptionVolatilityStructure vol = ConstantSwaptionVolatility.Create(
            2, Cal, BusinessDayConvention.Following, 0.20, new Actual365Fixed(), Context);
        var standardSwapBase = new EuriborSwapIsdaFixA(new Period(5, TimeUnit.Years), Curve);

        Assert.True(double.IsFinite(results.NPV!.Value));
        Assert.True(double.IsFinite(results.Result<double>("underlyingValue")));
        Assert.Equal(2, swaption.CalibrationBasket(engine, standardSwapBase, vol, Context, BasketGeneratingEngineCalibrationBasketType.Naive).Length);
        Assert.Equal(2, swaption.CalibrationBasket(engine, standardSwapBase, vol, Context).Length);
    }

    [Fact]
    public void TheEnginesSettingsPriceFinitely()
    {
        FloatFloatSwap payer = Swap(SwapType.Payer, Index1, Index2, false, false);
        FloatFloatSwap receiver = Swap(SwapType.Receiver, Index1, Index2, false, false);

        double nonFlatCall = Price(payer, Exercise, Gaussian1dFloatFloatSwaptionEngineProbabilities.None, true, false, false);
        double flatCall = Price(payer, Exercise, Gaussian1dFloatFloatSwaptionEngineProbabilities.Naive, true, true, true);
        double flatCallWithoutOas = Price(payer, Exercise, Gaussian1dFloatFloatSwaptionEngineProbabilities.Naive, true, true, false);
        double receiverPrice = Price(receiver, Exercise, Gaussian1dFloatFloatSwaptionEngineProbabilities.None, true, false, false);
        Price(receiver, Exercise, Gaussian1dFloatFloatSwaptionEngineProbabilities.Digital, false, false, false);
        Price(payer, Exercise, Gaussian1dFloatFloatSwaptionEngineProbabilities.Digital, true, false, false);
        Price(receiver, Exercise, Gaussian1dFloatFloatSwaptionEngineProbabilities.Digital, true, false, false);
        var rebated = new RebatedExercise(Exercise, [100.0, 50.0], 2, Cal);
        Price(payer, rebated, Gaussian1dFloatFloatSwaptionEngineProbabilities.Naive, true, false, true);
        Price(Swap(SwapType.Payer, Index1, Index2, true, true), Exercise, Gaussian1dFloatFloatSwaptionEngineProbabilities.None, true, false, false);
        Price(
            Swap(SwapType.Payer, new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), Curve), Index2, false, false),
            Exercise,
            Gaussian1dFloatFloatSwaptionEngineProbabilities.None,
            false,
            false,
            false);

        Assert.True(nonFlatCall > 0.0);
        Assert.True(receiverPrice > 0.0);
        Assert.True(Math.Abs(flatCall - flatCallWithoutOas) > 1e-12);
    }

    [Fact]
    public void AnExpiredSwaptionIsWorthNothingAndACashSettledOneIsRefused()
    {
        FloatFloatSwap payer = Swap(SwapType.Payer, Index1, Index2, false, false);
        var expired = new FloatFloatSwaption(payer, new EuropeanExercise(Settlement));
        var cashSettled = new FloatFloatSwaption(payer, new EuropeanExercise(Exercise.Dates[0]), SettlementType.Cash, SettlementMethod.ParYieldCurve);

        Assert.Equal(0.0, Gaussian1dFloatFloatSwaptionEngine.Calculate(expired, Context, Model, 12, 5.0).NPV);
        Assert.StartsWith(
            "cash settled (ParYieldCurve) swaptions not priced",
            Assert.Throws<NanoQLException>(() => Gaussian1dFloatFloatSwaptionEngine.Calculate(cashSettled, Context, Model, 12, 5.0)).Message);
    }

    [Fact]
    public void TheSwaptionsTypeAndTheEnginesDiscountingCurve()
    {
        // QuantLib's type(), the swap's (floatfloatswaption.hpp:59), and discountingCurve(), the model's curve where
        // none is given (gaussian1dfloatfloatswaptionengine.hpp:127-130)
        var swaption = new FloatFloatSwaption(Swap(SwapType.Receiver, Index1, Index2, false, false), Exercise);
        YieldTermStructure discount = new FlatForward(Settlement, 0.04, new Actual365Fixed());

        Assert.Equal(SwapType.Receiver, swaption.Type);
        Assert.Same(Model.TermStructure, new Gaussian1dFloatFloatSwaptionEngine(Model).DiscountingCurve);
        Assert.Same(discount, new Gaussian1dFloatFloatSwaptionEngine(Model, discountCurve: discount).DiscountingCurve);
    }

    private static Schedule Schedule(int months) => new(
        Settlement,
        Cal.Advance(Settlement, 5, TimeUnit.Years),
        new Period(months, TimeUnit.Months),
        Cal,
        BusinessDayConvention.ModifiedFollowing,
        BusinessDayConvention.ModifiedFollowing,
        DateGenerationRule.Forward,
        false,
        Context);

    // master's makeSwap: spreads of ±10bp, and with capAndFloor a cap of 1% and a floor of 0.1% on both legs
    private static FloatFloatSwap Swap(SwapType type, InterestRateIndex index1, InterestRateIndex index2, bool exchangePrincipal, bool capAndFloor)
    {
        double? cap = capAndFloor ? 0.01 : null, floor = capAndFloor ? 0.001 : null;
        return new FloatFloatSwap(
            type, 100.0, 100.0, Schedule(3), index1, index1.DayCounter, Schedule(6), index2, index2.DayCounter, Context, exchangePrincipal,
            exchangePrincipal, 1.0, 0.001, cap, floor, 1.0, -0.001, cap, floor);
    }

    // master's price: the value and the underlying's finite, and the probabilities, one per exercise date and one for
    // none, each in [0, 1 + 1e-12], their sum positive
    private static double Price(
        FloatFloatSwap swap,
        Exercise exercise,
        Gaussian1dFloatFloatSwaptionEngineProbabilities probabilities,
        bool extrapolate,
        bool flatExtrapolation,
        bool useOas)
    {
        InstrumentResults r = Gaussian1dFloatFloatSwaptionEngine.Calculate(
            new FloatFloatSwaption(swap, exercise), Context, Model, 12, 5.0, extrapolate, flatExtrapolation, useOas ? 0.002 : null, Curve, false, probabilities);
        Assert.True(double.IsFinite(r.NPV!.Value));
        Assert.True(double.IsFinite(r.Result<double>("underlyingValue")));
        if (probabilities != Gaussian1dFloatFloatSwaptionEngineProbabilities.None)
        {
            ImmutableArray<double> p = r.Result<ImmutableArray<double>>("probabilities");
            Assert.Equal(exercise.Dates.Length + 1, p.Length);
            Assert.All(p, x => Assert.InRange(x, 0.0, 1.0 + 1e-12));
            Assert.True(p.Sum() > 0.0);
        }

        return r.NPV.Value;
    }
}
