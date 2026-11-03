// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/equity/hestonmodelhelper.hpp ql/models/equity/hestonmodelhelper.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>
/// A calibration helper for the Heston model: a European plain-vanilla option, a period after the risk-free curve's
/// reference date, quoted by a Black volatility.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. The constructor builds what QuantLib's helper builds at its first calculation: the exercise date, the
/// period advanced on the calendar from the risk-free curve's reference date; the maturity, the time to it (below); the
/// option, a call where the discounted strike is at least the discounted spot and a put otherwise; and the market
/// value, the Black price at the quoted volatility over the maturity. QuantLib's spot is a quote or a number; here it
/// is the number.
/// </para>
/// <para>
/// The model value is the pricing engine's: <see cref="AnalyticHestonEngine"/> with its defaults, until
/// <see cref="SetPricingEngine{TModel}"/> sets another, which takes the model being calibrated as its last argument;
/// with no engine, pricing an option that hasn't expired refuses with QuantLib's "null pricing engine".
/// </para>
/// <para>
/// The Black price and the option's type read both curves by the exercise date, as the engine's forward does. QuantLib
/// reads them at the risk-free curve's time to it, which on a dividend curve with another day counter or reference date
/// is another date, so its market value and its model value price different forwards
/// (<c>Divergences/HestonForwardByDateTests.cs</c>). Where the curves share the day counter and the reference date, the
/// two reads are the same.
/// </para>
/// <para>
/// The maturity runs from the earlier curve's reference date, as the engine's model time does, so beside a risk-free
/// curve settling later the Black price's variance accrues from the evaluation date, as QuantLib's helper on the market
/// restated from that date reads it; QuantLib's runs from the risk-free curve's date
/// (<c>Divergences/HestonMismatchedTimesTests.cs</c>).
/// </para>
/// </remarks>
public sealed class HestonModelHelper : BlackCalibrationHelper
{
    private readonly YieldTermStructure riskFreeRate;
    private readonly YieldTermStructure dividendYield;
    private readonly double s0;
    private readonly double strikePrice;
    private readonly OptionType type;

    // Written by SetPricingEngine on a fresh copy only.
    private Func<VanillaOption, EvaluationContext, ICalibratedModel, InstrumentResults>? engine =
        (option, context, model) => AnalyticHestonEngine.Calculate(option, context, (HestonModel)model);

    /// <summary>Initializes a new instance of the <see cref="HestonModelHelper"/> class.</summary>
    /// <param name="maturity">The period from the risk-free curve's reference date to the exercise date.</param>
    /// <param name="calendar">The calendar the period is advanced on.</param>
    /// <param name="s0">The underlying's value.</param>
    /// <param name="strikePrice">The strike.</param>
    /// <param name="volatility">The quoted Black volatility.</param>
    /// <param name="riskFreeRate">The risk-free curve.</param>
    /// <param name="dividendYield">The dividend curve.</param>
    /// <param name="context">The evaluation context, under which the market value is computed.</param>
    /// <param name="errorType">How the helper measures its error.</param>
    /// <exception cref="ArgumentNullException">The calendar, a curve or the context is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="errorType"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="ArgumentException">The Black formula refuses its inputs.</exception>
    public HestonModelHelper(
        Period maturity,
        Calendar calendar,
        double s0,
        double strikePrice,
        double volatility,
        YieldTermStructure riskFreeRate,
        YieldTermStructure dividendYield,
        EvaluationContext context,
        BlackCalibrationHelperCalibrationErrorType errorType = BlackCalibrationHelperCalibrationErrorType.RelativePriceError)
        : base(volatility, errorType)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(riskFreeRate);
        ArgumentNullException.ThrowIfNull(dividendYield);
        ArgumentNullException.ThrowIfNull(context);
        this.riskFreeRate = riskFreeRate;
        this.dividendYield = dividendYield;
        this.s0 = s0;
        this.strikePrice = strikePrice;

        ExerciseDate = calendar.Advance(riskFreeRate.ReferenceDate, maturity);

        // the time to the exercise on the risk-free curve's day counter from the earlier curve's reference date, as the
        // engine's model time runs; QuantLib's, the risk-free curve's own, where the dividend curve isn't dated before
        // it
        Maturity = dividendYield.ReferenceDate < riskFreeRate.ReferenceDate
            ? riskFreeRate.DayCounter.YearFraction(dividendYield.ReferenceDate, ExerciseDate)
            : riskFreeRate.TimeFromReference(ExerciseDate);
        type = strikePrice * riskFreeRate.Discount(ExerciseDate) >= s0 * dividendYield.Discount(ExerciseDate)
            ? OptionType.Call
            : OptionType.Put;
        Option = new VanillaOption(new PlainVanillaPayoff(type, strikePrice), new EuropeanExercise(ExerciseDate));
        ComputeMarketValue(context);
    }

    /// <summary>Gets the exercise date.</summary>
    public Date ExerciseDate { get; }

    /// <summary>
    /// Gets the maturity: the time to the exercise date on the risk-free curve's day counter from the earlier of the
    /// two curves' reference dates, the risk-free curve's own time where the dividend curve isn't dated before it.
    /// </summary>
    public double Maturity { get; }

    /// <summary>Gets the option the helper prices.</summary>
    public VanillaOption Option { get; }

    /// <summary>
    /// A copy of the helper whose model value is the given engine's price: QuantLib's <c>setPricingEngine</c>.
    /// </summary>
    /// <typeparam name="TModel">The model type the engine prices with.</typeparam>
    /// <param name="engine">
    /// The engine: the option, the context and the model being calibrated, in the engines' order;
    /// <see langword="null"/> for none.
    /// </param>
    /// <returns>The copy.</returns>
    /// <remarks>
    /// The engine is kept for any model: pricing the copy against a model that isn't a <typeparamref name="TModel"/>
    /// throws <see cref="InvalidCastException"/>.
    /// </remarks>
    public HestonModelHelper SetPricingEngine<TModel>(Func<VanillaOption, EvaluationContext, TModel, InstrumentResults>? engine)
        where TModel : ICalibratedModel
    {
        var copy = (HestonModelHelper)MemberwiseClone();
        copy.engine = engine is null ? null : (o, c, m) => engine(o, c, (TModel)m);
        return copy;
    }

    /// <summary>The option's price according to a model, through the pricing engine.</summary>
    /// <param name="model">The model.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The model value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="context"/> is
    /// null.</exception>
    /// <exception cref="InvalidCastException">The engine prices with another model type.</exception>
    /// <exception cref="NanoQLException">No engine is set and the option hasn't expired, or the engine
    /// fails.</exception>
    public override double ModelValue(ICalibratedModel model, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        if (engine is null)
        {
            // QuantLib's instrument reads its expiry before it asks for its engine.
            if (Option.IsExpired(context))
            {
                return Npv(Option.SetupExpired());
            }

            throw new NanoQLException("null pricing engine");
        }

        return Npv(engine(Option, context, model));
    }

    /// <summary>The option's Black price at a volatility over the maturity, both curves read by the exercise
    /// date.</summary>
    /// <param name="volatility">The volatility.</param>
    /// <param name="context">The evaluation context, which the price doesn't read.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentException">The Black formula refuses its inputs.</exception>
    public override double BlackPrice(double volatility, EvaluationContext context)
    {
        double stdDev = volatility * Math.Sqrt(Maturity);
        return BlackFormulaFunctions.BlackFormula(
            type, strikePrice * riskFreeRate.Discount(ExerciseDate), s0 * dividendYield.Discount(ExerciseDate), stdDev);
    }

    /// <summary>The times a lattice for the option must stop at: none, as QuantLib's <c>addTimesTo</c>.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>No times.</returns>
    public override ImmutableArray<double> Times(EvaluationContext context) => [];

    private static double Npv(InstrumentResults results) => results.NPV ?? throw new NanoQLException("NPV not provided");
}
