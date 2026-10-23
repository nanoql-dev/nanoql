// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Gaussian1d model and engines on curves on another clock (the S-g1d plan's G1R6 (xvi); master plan §2.2's
/// mismatched-dates row): QuantLib reads a discount or forwarding curve at the model curve's times, so a curve on
/// another day counter with the same discount factors by date moves a price. On the wheel, on a GSR model (a = σ =
/// 0.01) on a flat 3% Actual/365 (Fixed) curve, a 5Y into 10Y payer at 3% is worth 0.07095344971961592 with a 2%
/// discount curve on Actual/360, where the same curve restated on Actual/365 (Fixed) gives 0.07076745548830388; with
/// the index forecast on a 3.1% Actual/360 curve, 0.06649840575430056 against 0.06817679085140739 restated. On a model
/// curve on 30/360 dated the 30th, a time doesn't tell the 30th of a 31-day month from the 31st: from 30 June 2025, a
/// Bermudan exercised on 30 January 2026, 30 July 2026 and 30 March 2027 into a 5-year payer at 3%, with a 2.5%
/// Actual/365 (Fixed) discount curve, is worth 0.029341545499651206, against 0.02934088072814103 on the curve restated
/// on 30/360 at every date the engine reads; its Digital probability of the exercise on the 30th (the C++ harness)
/// 0.00013119665716211765 against 0.00013119666147197958, and a zero-bond option expiring on 30 January 2026
/// 0.014136151799917162 against 0.014102533018978234. nanoql reads each curve at the instant QuantLib's argument
/// denotes: by the date QuantLib derives a time from (an exercise, a fixing, an option's expiry), where a read by the
/// time would take the 31st; and the numeraire time, which has no date and whose factor cancels, at the instant it
/// denotes on the model's clock. The nonstandard engine reads alike: on the same swaptions as nonstandard swaptions,
/// QuantLib's same numbers. So does the cap/floor engine at a caplet's fixing: a caplet at 3% on 1,000,000 of Euribor
/// 6M fixing in two years, forecast on the 3.1% Actual/360 curve, is worth 2693.625283396357 against 2793.958767701246
/// restated; on the 30/360 market, one fixing on 30 January 2026 with the discount curve 1528.9285427087634, where
/// QuantLib's swaption engine gives the one-period swaption into the same coupon 1509.9042444210352 and, on the market
/// restated, 1509.8482273231564, the strike's leg read on the discount curve too (G1R6 (ii)). So does the float-float
/// engine, at its exercises and its fixing events: on the same swaptions as float-float swaptions, their first legs
/// fixed at 3% in all but name (a zero gearing on Euribor 1Y, 30/360), QuantLib's engine gives 0.0709550276548306 on
/// the Actual/360 discount curve against 0.07076902784741748 restated; on the 30/360 market, where Euribor 6M fixes on
/// 30 July 2026, 0.029342639252770517 against 0.029341974297365136, and the Digital probability of the exercise on the
/// 30th 0.00013119665716211765 against 0.00013119666147197958, the swaption engine's; with rebates of 0.005, 0.002 and
/// 0.001 paid on the exercise dates, whose bond to their payment is 1 on any curve and in any state (so G1R6 (v)'s
/// reading at y = 0 moves none), 0.030447425948888763 against 0.030446802464572535. QuantLib's check: its engine on
/// the market restated on the model curve's clock (on the 30/360 market, a discount curve with a node at each date to
/// 2031 whose 30/360 time is new, and at the numeraire's 60 years); for a caplet with a discount curve, its swaption
/// engine on the one-period swaption.
/// </summary>
public class Gaussian1dMismatchedTimesTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure ModelCurve = new FlatForward(Today, 0.03, new Actual365Fixed());
    private static readonly Date Exercise = new TARGET().Advance(Today, 5, TimeUnit.Years);

    // the 30/360 market, from the 30th of a month, so a 30/360 time tells no 30th of a 31-day month from the 31st
    private static readonly Date Today30 = new(30, Month.June, 2025);
    private static readonly EvaluationContext Context30 = new() { EvaluationDate = Today30 };
    private static readonly YieldTermStructure ModelCurve30 = new FlatForward(Today30, 0.03, new Thirty360(Thirty360Convention.BondBasis));
    private static readonly YieldTermStructure Discount365 = new FlatForward(Today30, 0.025, new Actual365Fixed());
    private static readonly ImmutableArray<Date> Exercises30 = [new(30, Month.January, 2026), new(30, Month.July, 2026), new(30, Month.March, 2027)];

    [Fact]
    public void ADiscountCurveOnAnotherDayCounterIsReadByDate()
    {
        Gsr model = Gaussian1dInputs.Constant(ModelCurve, 0.01, 0.01);
        var swaption = new Swaption(Gaussian1dInputs.SwapOf(new Euribor6M(ModelCurve), Exercise, 10, 0.03, SwapType.Payer, Context), new EuropeanExercise(Exercise));
        YieldTermStructure discount360 = new FlatForward(Today, 0.02 * 360.0 / 365.0, new Actual360());
        double price = Gaussian1dSwaptionEngine.Calculate(swaption, Context, model, discountCurve: discount360).NPV!.Value;
        Assert.True(Math.Abs((price / 0.07076745548830388) - 1.0) <= 1e-12, $"{price}");
        Assert.True(Math.Abs((price / 0.07095344971961592) - 1.0) > 1e-3);
    }

    [Fact]
    public void AForwardingCurveOnAnotherDayCounterIsReadByDate()
    {
        Gsr model = Gaussian1dInputs.Constant(ModelCurve, 0.01, 0.01);
        YieldTermStructure forward360 = new FlatForward(Today, 0.031 * 360.0 / 365.0, new Actual360());
        var swaption = new Swaption(Gaussian1dInputs.SwapOf(new Euribor6M(forward360), Exercise, 10, 0.03, SwapType.Payer, Context), new EuropeanExercise(Exercise));
        double price = Gaussian1dSwaptionEngine.Calculate(swaption, Context, model).NPV!.Value;
        Assert.True(Math.Abs((price / 0.06817679085140739) - 1.0) <= 1e-12, $"{price}");
        Assert.True(Math.Abs((price / 0.06649840575430056) - 1.0) > 1e-2);
    }

    [Fact]
    public void AnExerciseOnThe30thOfA31DayMonthIsReadByDate()
    {
        double price = Gaussian1dSwaptionEngine.Calculate(Bermudan30(), Context30, Model30(), discountCurve: Discount365).NPV!.Value;
        Assert.True(Math.Abs((price / 0.02934088072814103) - 1.0) <= 1e-12, $"{price}");
        Assert.True(Math.Abs((price / 0.029341545499651206) - 1.0) > 1e-5);
    }

    [Fact]
    public void TheDigitalProbabilitiesReadTheExerciseByDate()
    {
        InstrumentResults results = Gaussian1dSwaptionEngine.Calculate(
            Bermudan30(), Context30, Model30(), 64, 7.0, true, false, Discount365, Gaussian1dSwaptionEngineProbabilities.Digital);
        double[] restated = [0.00013119666147197958, 0.44411124920974154, 0.2487328850267858, 0.30978785594136143];
        double[] quantLib = [0.00013119665716211765, 0.4441112492218249, 0.24873288502666321, 0.30978785594135955];
        ImmutableArray<double> probabilities = results.Result<ImmutableArray<double>>("probabilities");
        Assert.Equal(restated.Length, probabilities.Length);

        // The exercise on 30 January 2026: the restated market's, where QuantLib's reads the 31st's.
        Assert.True(Math.Abs(probabilities[0] - restated[0]) <= 1e-13, $"{probabilities[0]}");
        Assert.True(Math.Abs(probabilities[0] - quantLib[0]) > 4e-12, $"{probabilities[0]}");

        // The later ones: the discount reads cancel in each, the bond to the exercise against the numeraire's
        // denominator, so restating the market moves QuantLib's by rounding only (1.2e-11 at most).
        for (int i = 1; i < restated.Length; i++)
        {
            Assert.True(Math.Abs(probabilities[i] - restated[i]) <= 2e-11, $"{i}: {probabilities[i]}");
        }
    }

    [Fact]
    public void AZerobondOptionExpiringOnThe30thIsReadByDate()
    {
        double price = Model30().ZerobondOption(
            OptionType.Call, new Date(30, Month.January, 2026), new Date(3, Month.February, 2026), new Date(3, Month.February, 2031), 0.88, null, 0.0, Discount365);
        Assert.True(Math.Abs((price / 0.014102533018978234) - 1.0) <= 1e-12, $"{price}");
        Assert.True(Math.Abs((price / 0.014136151799917162) - 1.0) > 1e-3);
    }

    [Fact]
    public void TheNonstandardEnginesDiscountCurveOnAnotherDayCounterIsReadByDate()
    {
        Gsr model = Gaussian1dInputs.Constant(ModelCurve, 0.01, 0.01);
        var swaption = new NonstandardSwaption(
            new Swaption(Gaussian1dInputs.SwapOf(new Euribor6M(ModelCurve), Exercise, 10, 0.03, SwapType.Payer, Context), new EuropeanExercise(Exercise)), Context);
        YieldTermStructure discount360 = new FlatForward(Today, 0.02 * 360.0 / 365.0, new Actual360());
        double price = Gaussian1dNonstandardSwaptionEngine.Calculate(swaption, Context, model, discountCurve: discount360).NPV!.Value;
        Assert.True(Math.Abs((price / 0.07076745548830388) - 1.0) <= 1e-12, $"{price}");
        Assert.True(Math.Abs((price / 0.07095344971961592) - 1.0) > 1e-3);
    }

    [Fact]
    public void TheNonstandardEnginesDigitalProbabilitiesReadTheExerciseByDate()
    {
        InstrumentResults results = Gaussian1dNonstandardSwaptionEngine.Calculate(
            new NonstandardSwaption(Bermudan30(), Context30),
            Context30,
            Model30(),
            discountCurve: Discount365,
            probabilities: Gaussian1dNonstandardSwaptionEngineProbabilities.Digital);
        double[] restated = [0.00013119666147197958, 0.44411124920974154, 0.2487328850267858, 0.30978785594136143];
        ImmutableArray<double> probabilities = results.Result<ImmutableArray<double>>("probabilities");
        Assert.True(Math.Abs((results.NPV!.Value / 0.02934088072814103) - 1.0) <= 1e-12, $"{results.NPV}");
        Assert.Equal(restated.Length, probabilities.Length);

        // The exercise on 30 January 2026: the restated market's, where QuantLib's reads the 31st's
        // (0.00013119665716211765); the later ones, as the swaption engine's, to rounding.
        Assert.True(Math.Abs(probabilities[0] - restated[0]) <= 1e-13, $"{probabilities[0]}");
        Assert.True(Math.Abs(probabilities[0] - 0.00013119665716211765) > 4e-12, $"{probabilities[0]}");
        for (int i = 1; i < restated.Length; i++)
        {
            Assert.True(Math.Abs(probabilities[i] - restated[i]) <= 2e-11, $"{i}: {probabilities[i]}");
        }
    }

    [Fact]
    public void ACapletsForwardingCurveOnAnotherDayCounterIsReadByDate()
    {
        YieldTermStructure forward360 = new FlatForward(Today, 0.031 * 360.0 / 365.0, new Actual360());
        Cap caplet = Caplet(new Euribor6M(forward360), new TARGET().Advance(Today, 2, TimeUnit.Years), Context);
        double price = Gaussian1dCapFloorEngine.Calculate(caplet, Context, Gaussian1dInputs.Constant(ModelCurve, 0.01, 0.01)).NPV!.Value;
        Assert.True(Math.Abs((price / 2793.958767701246) - 1.0) <= 1e-12, $"{price}");
        Assert.True(Math.Abs((price / 2693.625283396357) - 1.0) > 1e-2);
    }

    [Fact]
    public void ACapletFixingOnThe30thIsReadByDate()
    {
        Cap caplet = Caplet(new Euribor6M(ModelCurve30), new Date(30, Month.January, 2026), Context30);
        double price = Gaussian1dCapFloorEngine.Calculate(caplet, Context30, Model30(), discountCurve: Discount365).NPV!.Value;
        Assert.True(Math.Abs((price / 1509.8482273231564) - 1.0) <= 1e-12, $"{price}");
        Assert.True(Math.Abs((price / 1509.9042444210352) - 1.0) > 1e-5);
        Assert.True(Math.Abs((price / 1528.9285427087634) - 1.0) > 1e-2);
    }

    [Fact]
    public void TheFloatFloatEnginesDiscountCurveOnAnotherDayCounterIsReadByDate()
    {
        Gsr model = Gaussian1dInputs.Constant(ModelCurve, 0.01, 0.01);
        var swaption = new FloatFloatSwaption(FloatFloatSwapOf(ModelCurve, new TARGET().Advance(Exercise, 2, TimeUnit.Days), 10, Context), new EuropeanExercise(Exercise));
        YieldTermStructure discount360 = new FlatForward(Today, 0.02 * 360.0 / 365.0, new Actual360());
        double price = Gaussian1dFloatFloatSwaptionEngine.Calculate(swaption, Context, model, discountCurve: discount360).NPV!.Value;
        Assert.True(Math.Abs((price / 0.07076902784741748) - 1.0) <= 1e-11, $"{price}");
        Assert.True(Math.Abs((price / 0.0709550276548306) - 1.0) > 1e-3);
    }

    [Fact]
    public void TheFloatFloatEnginesExercisesAndFixingsOnThe30thAreReadByDate()
    {
        var swaption = new FloatFloatSwaption(FloatFloatSwapOf(ModelCurve30, new Date(3, Month.February, 2026), 5, Context30), new BermudanExercise(Exercises30));
        InstrumentResults results = Gaussian1dFloatFloatSwaptionEngine.Calculate(
            swaption, Context30, Model30(), discountCurve: Discount365, probabilities: Gaussian1dFloatFloatSwaptionEngineProbabilities.Digital);
        double[] restated = [0.00013119666147197958, 0.44411124920974154, 0.24872966048029543, 0.3097924921785468];
        ImmutableArray<double> probabilities = results.Result<ImmutableArray<double>>("probabilities");
        Assert.True(Math.Abs((results.NPV!.Value / 0.029341974297365136) - 1.0) <= 1e-12, $"{results.NPV}");
        Assert.True(Math.Abs((results.NPV!.Value / 0.029342639252770517) - 1.0) > 1e-5);
        Assert.Equal(restated.Length, probabilities.Length);

        // The exercise on 30 January 2026: the restated market's, where QuantLib's reads the 31st's
        // (0.00013119665716211765); the later ones, as the swaption engine's, to rounding.
        Assert.True(Math.Abs(probabilities[0] - restated[0]) <= 1e-13, $"{probabilities[0]}");
        Assert.True(Math.Abs(probabilities[0] - 0.00013119665716211765) > 4e-12, $"{probabilities[0]}");
        for (int i = 1; i < restated.Length; i++)
        {
            Assert.True(Math.Abs(probabilities[i] - restated[i]) <= 2e-11, $"{i}: {probabilities[i]}");
        }
    }

    [Fact]
    public void TheFloatFloatEnginesRebatesOnThe30thAreReadByDate()
    {
        var exercise = new RebatedExercise(new BermudanExercise(Exercises30), [0.005, 0.002, 0.001], 0, new TARGET(), BusinessDayConvention.Following);
        var swaption = new FloatFloatSwaption(FloatFloatSwapOf(ModelCurve30, new Date(3, Month.February, 2026), 5, Context30), exercise);
        double price = Gaussian1dFloatFloatSwaptionEngine.Calculate(swaption, Context30, Model30(), discountCurve: Discount365).NPV!.Value;

        // Each rebate paid on its exercise date, its bond 1, so the rebate's numeraire is the read the price holds.
        Assert.Equal(Exercises30, Enumerable.Range(0, Exercises30.Length).Select(exercise.RebatePaymentDate));
        Assert.True(Math.Abs((price / 0.030446802464572535) - 1.0) <= 1e-12, $"{price}");
        Assert.True(Math.Abs((price / 0.030447425948888763) - 1.0) > 1e-5);
    }

    private static Gsr Model30() => Gaussian1dInputs.Constant(ModelCurve30, 0.01, 0.01);

    // A float-float payer of 1 from the start, its first leg fixed at 3% in all but name (a zero gearing on Euribor 1Y,
    // yearly, 30/360) against Euribor 6M (Actual/360), both forecast on the curve.
    private static FloatFloatSwap FloatFloatSwapOf(YieldTermStructure curve, Date start, int years, EvaluationContext context)
    {
        var calendar = new TARGET();
        Schedule Schedule(int months) => new(
            start,
            calendar.Advance(start, years, TimeUnit.Years),
            new Period(months, TimeUnit.Months),
            calendar,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Backward,
            false,
            context);
        Schedule yearly = Schedule(12), semiannual = Schedule(6);
        return new FloatFloatSwap(
            SwapType.Payer,
            Enumerable.Repeat(1.0, yearly.Size - 1),
            Enumerable.Repeat(1.0, semiannual.Size - 1),
            yearly,
            new Euribor1Y(curve),
            new Thirty360(Thirty360Convention.BondBasis),
            semiannual,
            new Euribor6M(curve),
            new Actual360(),
            context,
            gearing1: new double[yearly.Size - 1],
            spread1: Enumerable.Repeat(0.03, yearly.Size - 1),
            paymentConvention1: BusinessDayConvention.Following,
            paymentConvention2: BusinessDayConvention.Following);
    }

    // A caplet at 3% on 1,000,000 of the index fixing on the date, its accrual on Actual/360.
    private static Cap Caplet(IborIndex index, Date fixing, EvaluationContext context)
    {
        Date start = index.ValueDate(fixing);
        ImmutableArray<CashFlow> leg = new IborLeg(new Schedule([start, index.MaturityDate(start)], new TARGET()), index)
            .WithNotionals(1e6)
            .WithPaymentDayCounter(new Actual360())
            .ToLeg(context);
        return new Cap(leg, [0.03]);
    }

    private static Swaption Bermudan30()
    {
        VanillaSwap swap = Gaussian1dInputs.SwapOf(new Euribor6M(ModelCurve30), Exercises30[0], 5, 0.03, SwapType.Payer, Context30, start: new Date(3, Month.February, 2026));
        return new Swaption(swap, new BermudanExercise(Exercises30));
    }
}
