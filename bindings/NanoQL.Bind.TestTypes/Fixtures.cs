// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Extensions.Conventions;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Quotes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Bind.TestTypes;

/// <summary>
/// The bound-engine cases' markets, built in C#, so a conformance suite exercises only what it tests through the C ABI:
/// the engine bound, the member taking it, the result. Each case's C# lambda route is in <see cref="References"/>.
/// </summary>
public static class Fixtures
{
    private static readonly Date Today = new(15, Month.September, 2015);

    /// <summary>Gets the cases' evaluation context.</summary>
    public static EvaluationContext Context { get; } = new() { EvaluationDate = Today };

    /// <summary>Gets a flat 3% curve, continuous, Actual/365 (Fixed).</summary>
    public static YieldTermStructure RiskFree { get; } = new FlatForward(Today, 0.03, ConventionSet.QuantLib.DayCounter("Actual365Fixed"), Compounding.Continuous, Frequency.Annual);

    /// <summary>
    /// Gets Andreasen–Huge's calibration set on the cases' curves: nine options, three expiries by three strikes, each
    /// with its implied volatility, a collection of tuples nanoql returns (docs/bindings.md, "Values").
    /// </summary>
    public static IReadOnlyList<(VanillaOption Option, double Volatility)> CalibrationSet { get; } =
    [
        .. from months in new[] { 6, 12, 24 }
           from strike in new[] { 80.0, 100.0, 120.0 }
           select (new VanillaOption(
                   new PlainVanillaPayoff(strike >= 100 ? OptionType.Call : OptionType.Put, strike),
                   new EuropeanExercise(new TARGET().Advance(Today, months, TimeUnit.Months))),
               0.20 + (0.0005 * (100 - strike) * (100 - strike) / 10) + (0.01 * months / 12)),
    ];

    /// <summary>Gets a flat 1% dividend curve.</summary>
    public static YieldTermStructure Dividend { get; } = new FlatForward(Today, 0.01, ConventionSet.QuantLib.DayCounter("Actual365Fixed"), Compounding.Continuous, Frequency.Annual);

    /// <summary>Gets a flat 2% foreign curve, the quanto's.</summary>
    public static YieldTermStructure Foreign { get; } = new FlatForward(Today, 0.02, ConventionSet.QuantLib.DayCounter("Actual365Fixed"), Compounding.Continuous, Frequency.Annual);

    /// <summary>Gets a ten-year Euribor 6M swap's builder on <see cref="RiskFree"/>.</summary>
    public static MakeVanillaSwap SwapBuilder { get; } = new(new Period(10, TimeUnit.Years), ConventionSet.QuantLib.IborIndex("Euribor", new Period(6, TimeUnit.Months), RiskFree));

    /// <summary>Gets a Heston model on <see cref="RiskFree"/> and <see cref="Dividend"/>.</summary>
    public static HestonModel Heston { get; } = new(new HestonProcess(RiskFree, Dividend, 100.0, 0.04, 1.5, 0.04, 0.3, -0.6));

    /// <summary>Gets a one-year Heston calibration helper, struck at 105, its engine unset.</summary>
    public static HestonModelHelper HestonHelper { get; } = new(new Period(1, TimeUnit.Years), new TARGET(), 100.0, 105.0, 0.2, RiskFree, Dividend, Context);

    /// <summary>Gets a Hull-White model on <see cref="RiskFree"/>, the Heston–Hull-White engines' second model.</summary>
    public static HullWhite HullWhite { get; } = new(RiskFree, 0.01, 0.01);

    /// <summary>Gets a one-year quanto call struck at 105.</summary>
    public static QuantoVanillaOption Quanto { get; } = new(
        new PlainVanillaPayoff(OptionType.Call, 105.0),
        new EuropeanExercise(new TARGET().Advance(Today, new Period(1, TimeUnit.Years), BusinessDayConvention.Following)));

    /// <summary>Gets a Black–Scholes–Merton process at 20% volatility.</summary>
    public static GeneralizedBlackScholesProcess BlackScholes { get; } =
        new BlackScholesMertonProcess(100.0, Dividend, RiskFree, new BlackConstantVol(Today, new TARGET(), 0.2, new Actual365Fixed()));

    /// <summary>Gets the quanto's exchange-rate volatility, 10%.</summary>
    public static BlackVolTermStructure ExchangeVolatility { get; } = new BlackConstantVol(Today, new TARGET(), 0.1, new Actual365Fixed());

    /// <summary>Gets the OAS cases' context, a day later.</summary>
    public static EvaluationContext BondContext { get; } = new() { EvaluationDate = new Date(16, Month.September, 2015) };

    /// <summary>Gets the OAS cases' flat 3% curve from settlement.</summary>
    public static YieldTermStructure BondCurve { get; } = new FlatForward(new TARGET().Advance(BondContext.EvaluationDate, 2, TimeUnit.Days), 0.03, new Actual365Fixed());

    /// <summary>Gets the OAS cases' Hull-White model, the tree engine's.</summary>
    public static HullWhite BondModel { get; } = new(BondCurve);

    /// <summary>
    /// Gets a ten-year 5% callable bond issued 100 days before the bond context's date, called once at par in four years
    /// (nanoql's BlackCallableEngineTests' bond).
    /// </summary>
    public static CallableFixedRateBond CallableBond { get; } = Bond(new TARGET(), BondContext.EvaluationDate);

    /// <summary>Gets the Vanna–Volga case's at-the-money quote.</summary>
    public static DeltaVolQuote AtmQuote { get; } = new(0.1, DeltaVolQuoteDeltaType.Fwd, 1.0, DeltaVolQuoteAtmType.AtmDeltaNeutral);

    /// <summary>Gets the Vanna–Volga case's 25-delta put quote.</summary>
    public static DeltaVolQuote Put25 { get; } = new(-0.25, 0.11, 1.0, DeltaVolQuoteDeltaType.Fwd);

    /// <summary>Gets the Vanna–Volga case's 25-delta call quote.</summary>
    public static DeltaVolQuote Call25 { get; } = new(0.25, 0.11, 1.0, DeltaVolQuoteDeltaType.Fwd);

    /// <summary>Gets the Vanna–Volga case's flat 1% curve.</summary>
    public static YieldTermStructure FxCurve { get; } = new FlatForward(Today, 0.01, new Actual365Fixed());

    /// <summary>Gets the Vanna–Volga case's knock-out double barrier call, barriers 80 and 120, struck at 100.</summary>
    public static DoubleBarrierOption DoubleBarrier { get; } = new(
        DoubleBarrierType.KnockOut, 80.0, 120.0, 0.0, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 182));

    /// <summary>Gets the calibrations' context.</summary>
    public static EvaluationContext CalibrationContext { get; } = new() { EvaluationDate = new Date(15, Month.January, 2025) };

    /// <summary>Gets the calibrations' flat 3% curve.</summary>
    public static YieldTermStructure CalibrationCurve { get; } = new FlatForward(CalibrationContext.EvaluationDate, 0.03, new Actual365Fixed());

    /// <summary>Gets a GSR model with two volatility steps (nanoql's GsrIterativeCalibrationTests basket's).</summary>
    public static Gsr Gsr { get; } = new(
        CalibrationCurve,
        [new TARGET().Advance(CalibrationContext.EvaluationDate, 1, TimeUnit.Years), new TARGET().Advance(CalibrationContext.EvaluationDate, 2, TimeUnit.Years)],
        [0.006, 0.006, 0.006], [0.02, 0.03, 0.04]);

    /// <summary>Gets a Hull-White model on the calibrations' curve.</summary>
    public static HullWhite CalibrationHullWhite { get; } = new(CalibrationCurve);

    /// <summary>Gets the calibrations' Levenberg–Marquardt method.</summary>
    public static LevenbergMarquardt Method { get; } = new(1e-8, 1e-8, 1e-8);

    /// <summary>Gets the calibrations' end criteria.</summary>
    public static EndCriteria Criteria { get; } = new(10000, 100, 1e-6, 1e-8, 1e-8);

    /// <summary>Gets three deposits, 1, 2 and 3 years, for a piecewise curve built with its defaults.</summary>
    public static ImmutableArray<BootstrapHelper<YieldTermStructure>> DepositHelpers { get; } =
    [
        Deposit(1, 0.030), Deposit(2, 0.032), Deposit(3, 0.034),
    ];

    /// <summary>Gets three swaption helpers, 1x3, 2x2 and 3x1, their engines unset.</summary>
    public static ImmutableArray<SwaptionHelper> Swaptions { get; } =
    [
        Swaption(1, 0.20), Swaption(2, 0.19), Swaption(3, 0.18),
    ];

    /// <summary>Gets three cap helpers, 2, 5 and 10 years, their engines unset.</summary>
    public static ImmutableArray<CapHelper> Caps { get; } =
    [
        Cap(2, 0.20), Cap(5, 0.18), Cap(10, 0.16),
    ];

    private static IborIndex Euribor6M => new Euribor6M(CalibrationCurve);

    private static DepositRateHelper Deposit(int years, double rate) =>
        new(rate, new Period(years, TimeUnit.Years), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, true, new Actual360(), Context);

    private static SwaptionHelper Swaption(int option, double vol) => new(
        new Period(option, TimeUnit.Years), new Period(4 - option, TimeUnit.Years), vol, Euribor6M, new Period(1, TimeUnit.Years),
        new Thirty360(Thirty360Convention.BondBasis), new Actual360(), CalibrationCurve, CalibrationContext);

    private static CapHelper Cap(int years, double vol) =>
        new(new Period(years, TimeUnit.Years), vol, Euribor6M, Frequency.Annual, new Actual360(), false, CalibrationCurve, CalibrationContext);

    private static CallableFixedRateBond Bond(Calendar calendar, Date today)
    {
        Date issue = calendar.Adjust(today - 100);
        Schedule schedule = new MakeSchedule().From(issue).To(calendar.Advance(issue, 10, TimeUnit.Years))
            .WithCalendar(calendar).WithFrequency(Frequency.Semiannual)
            .WithConvention(BusinessDayConvention.ModifiedFollowing).WithRule(DateGenerationRule.Backward).ToSchedule();
        return new CallableFixedRateBond(
            3, 100.0, schedule, [0.05], new Thirty360(Thirty360Convention.BondBasis),
            BusinessDayConvention.ModifiedFollowing, 100.0, issue,
            [new Callability(new BondPrice(100.0, BondPriceType.Clean), CallabilityType.Call, calendar.Advance(today, 4, TimeUnit.Years))]);
    }
}
