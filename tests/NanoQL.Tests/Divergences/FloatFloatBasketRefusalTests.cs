// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The float-float engine's calibration basket refuses, with QuantLib's master's texts (#2845, after the pin), the two
/// expiries its initial guess can't be built at: master's
/// <c>testGaussian1dFloatFloatSwaptionCalibrationInitialGuessBoundaries</c>, on its fixture (Euribor 6M and 3M, 100 a
/// leg, a flat 5% from the settlement date, GSR σ = 0.01, a = 0.03), the evaluation date fixed at 15 January 2025.
/// QuantLib 1.44 refuses both with a schedule's text instead: on the wheel, "effective date (October 21st, 2025) later
/// than or equal to termination date (October 21st, 2025)" and "effective date (January 19th, 2027) later than or equal
/// to termination date (January 19th, 2027)" (the first exercise a one-date Bermudan with a one-element rebate vector,
/// the wheel binding no constructor with one rebate).
/// </summary>
public class FloatFloatBasketRefusalTests
{
    private static readonly Calendar Cal = new TARGET();
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly Date Settlement = Cal.Advance(Today, 2, TimeUnit.Days);
    private static readonly YieldTermStructure Curve = new FlatForward(Settlement, 0.05, new Actual365Fixed());
    private static readonly SwaptionVolatilityStructure Vol = ConstantSwaptionVolatility.Create(
        2, Cal, BusinessDayConvention.Following, 0.20, new Actual365Fixed(), Context);
    private static readonly SwapIndex StandardSwapBase = new EuriborSwapIsdaFixA(new Period(5, TimeUnit.Years), Curve);
    private static readonly Gaussian1dFloatFloatSwaptionEngine Engine = new(new Gsr(Curve, [], [0.01], 0.03), 12, 5.0);

    [Fact]
    public void AnExpiryAfterLeg1sLastResetIsRefused()
    {
        var index = new Euribor(new Period(6, TimeUnit.Months), Curve);
        Schedule oneYear = Schedule(1, 6);
        var swap = new FloatFloatSwap(SwapType.Payer, 100.0, 100.0, oneYear, index, index.DayCounter, oneYear, index, index.DayCounter, Context);
        Date expiry = Cal.Advance(Settlement, 9, TimeUnit.Months);
        var swaption = new FloatFloatSwaption(swap, new RebatedExercise(new EuropeanExercise(expiry), 100.0, 2, Cal));

        Assert.Equal(
            $"no leg 1 reset dates remain at calibration expiry {expiry}",
            Assert.Throws<NanoQLException>(() => swaption.CalibrationBasket(Engine, StandardSwapBase, Vol, Context)).Message);
    }

    [Fact]
    public void Leg1NotionalsSummingToZeroAreRefused()
    {
        var index = new Euribor(new Period(3, TimeUnit.Months), Curve);
        Schedule quarterly = Schedule(5, 3);
        double[] notionals1 = new double[quarterly.Size - 1];
        notionals1[^2] = 100.0;
        notionals1[^1] = -100.0;
        var swap = new FloatFloatSwap(
            SwapType.Payer, notionals1, Enumerable.Repeat(100.0, quarterly.Size - 1), quarterly, index, index.DayCounter, quarterly, index,
            index.DayCounter, Context);
        Date expiry = Cal.Advance(Today, 2, TimeUnit.Years);
        var swaption = new FloatFloatSwaption(swap, new EuropeanExercise(expiry));

        Assert.Equal(
            $"remaining leg 1 notionals sum to zero at calibration expiry {expiry}",
            Assert.Throws<NanoQLException>(() => swaption.CalibrationBasket(Engine, StandardSwapBase, Vol, Context)).Message);
    }

    private static Schedule Schedule(int years, int months) => new(
        Settlement,
        Cal.Advance(Settlement, years, TimeUnit.Years),
        new Period(months, TimeUnit.Months),
        Cal,
        BusinessDayConvention.ModifiedFollowing,
        BusinessDayConvention.ModifiedFollowing,
        DateGenerationRule.Forward,
        false,
        Context);
}
