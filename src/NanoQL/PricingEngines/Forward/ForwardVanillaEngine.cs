// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/forward/forwardengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The forward-start engine for vanilla options: a vanilla engine run on the forward process from the reset date, at
/// the moneyness times the spot, its results discounted on the dividend curve to the reset date.
/// </summary>
/// <remarks>
/// <para>
/// An extension point, as QuantLib's template: <see cref="ForwardPerformanceVanillaEngine"/> derives it and overrides
/// <see cref="OriginalResults"/>. Immutable; it holds the process and the inner engine, a delegate in D14's form taking
/// the forward process (the S-exo plan's ER2 (iii)); the forward process is built per call.
/// </para>
/// <para>
/// The payoff must be a plain vanilla one: QuantLib builds the inner option's plain vanilla payoff from the payoff's
/// type alone, so a cash-or-nothing forward start prices as the vanilla (the plan's ER5 (xxiii),
/// <c>Divergences/ForwardNonPlainPayoffTests.cs</c>); it is refused with the text of QuantLib's Monte Carlo forward
/// engines. The vega is the derivative under a parallel shift of the input volatilities, the inner vega in the
/// period's forward volatility times dσ_f/dε, where QuantLib's is the inner vega alone (ER5 (xxxvii),
/// <c>Divergences/ForwardVegaTermStructureTests.cs</c>): exactly QuantLib's where the volatilities at the reset and at
/// expiry are equal. The dividend rate read for the theta is on the process's clock
/// (<see cref="FdmBlackScholesOp.Clock"/>), each curve read by date, and the dividend rho's reset term on the dividend
/// curve's own time, the derivative of its zero rates (ER5 (xxiv), <c>Divergences/ExoticMismatchedTimesTests.cs</c>);
/// on structures dated alike, QuantLib's reads.
/// </para>
/// </remarks>
public class ForwardVanillaEngine
{
    /// <summary>Initializes a new instance of the <see cref="ForwardVanillaEngine"/> class.</summary>
    /// <param name="process">The underlying's process.</param>
    /// <param name="engine">The vanilla engine run on the forward process.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ForwardVanillaEngine(
        GeneralizedBlackScholesProcess process,
        Func<VanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults> engine)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(engine);
        Process = process;
        Engine = engine;
    }

    /// <summary>Gets the underlying's process.</summary>
    protected GeneralizedBlackScholesProcess Process { get; }

    /// <summary>Gets the vanilla engine run on the forward process.</summary>
    protected Func<VanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults> Engine { get; }

    /// <summary>Prices a forward-start option: QuantLib's <c>ForwardVanillaEngine&lt;Engine&gt;</c>.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The underlying's process.</param>
    /// <param name="engine">The vanilla engine run on the forward process.</param>
    /// <returns>The option's results: the value and the inner engine's Greeks, carried to today.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The option's terms are invalid, its payoff isn't plain vanilla, the spot isn't positive, or the inner engine
    /// refuses.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        ForwardVanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        Func<VanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults> engine) =>
        new ForwardVanillaEngine(process, engine).Calculate(option, context);

    /// <summary>Prices a forward-start option: QuantLib's <c>calculate</c>.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The option's results; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The option's terms are invalid, its payoff isn't plain vanilla, the spot isn't positive, or the inner engine
    /// refuses.
    /// </exception>
    public OneAssetOptionResults Calculate(ForwardVanillaOption option, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate(context);
        OneAssetOptionResults original = Engine(Underlying(option), context, ForwardProcess(option));
        return option.FetchResults(OriginalResults(option, original));
    }

    /// <summary>The inner vanilla option: QuantLib's <c>setup</c>, the plain vanilla payoff at the moneyness times the spot.</summary>
    /// <param name="option">The forward-start option.</param>
    /// <returns>The vanilla option the inner engine prices.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The payoff isn't plain vanilla.</exception>
    public VanillaOption Underlying(ForwardVanillaOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (option.Payoff is not PlainVanillaPayoff argumentsPayoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        return new VanillaOption(
            new PlainVanillaPayoff(argumentsPayoff.OptionType, option.Moneyness * Process.X0), option.Exercise);
    }

    /// <summary>The forward process: QuantLib's <c>setup</c>, the curves and the volatility implied from the reset date.</summary>
    /// <param name="option">The forward-start option.</param>
    /// <returns>The process the inner engine reads.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The spot isn't positive.</exception>
    public GeneralizedBlackScholesProcess ForwardProcess(ForwardVanillaOption option)
    {
        ArgumentNullException.ThrowIfNull(option);

        // maybe the forward value is "better", in some fashion
        // the right level is needed in order to interpolate
        // the vol
        double spot = Process.X0;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        var dividendYield = new ImpliedTermStructure(Process.DividendYield, option.ResetDate);
        var riskFreeRate = new ImpliedTermStructure(Process.RiskFreeRate, option.ResetDate);

        // The following approach is ok if the vol is at most
        // time-dependent. It is plain wrong if it is asset-dependent.
        // In the latter case the right solution would be stochastic
        // volatility or at least local volatility (which unfortunately
        // implies an unrealistic time-decreasing smile)
        var blackVolatility = new ImpliedVolTermStructure(Process.BlackVolatility, option.ResetDate);
        return new GeneralizedBlackScholesProcess(spot, dividendYield, riskFreeRate, blackVolatility);
    }

    /// <summary>
    /// The forward-start option's results from the inner engine's: QuantLib's <c>getOriginalResults</c>, with the vega
    /// under a parallel shift and the reset term on the dividend curve's own time.
    /// </summary>
    /// <param name="option">The forward-start option.</param>
    /// <param name="originalResults">The inner engine's results.</param>
    /// <returns>The forward-start option's results.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    protected virtual OneAssetOptionResults OriginalResults(
        ForwardVanillaOption option, OneAssetOptionResults originalResults)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(originalResults);
        YieldTermStructure qTS = Process.DividendYield;
        TermStructure clock = FdmBlackScholesOp.Clock(Process);
        double resetTime = ResetTime(option, qTS);
        double discQ = qTS.AccruedDiscount(option.ResetDate);
        double value = discQ * originalResults.NPV!.Value;

        // I need the strike derivative here ...
        double? delta = originalResults.Delta is double d && originalResults.StrikeSensitivity is double s
            ? discQ * (d + (option.Moneyness * s))
            : null;
        double? vega = originalResults.Vega is double v
            ? discQ * ShiftedVega(option, v)
            : null;
        double? rho = originalResults.Rho is double r ? discQ * r : null;
        double? dividendRho = originalResults.DividendRho is double q
            ? (-resetTime * value) + (discQ * q)
            : null;
        return new OneAssetOptionResults
        {
            NPV = value,
            Delta = delta,
            Gamma = 0.0,
            Theta = ResetRate(qTS, option, clock) * value,
            Vega = vega,
            Rho = rho,
            DividendRho = dividendRho,
        };
    }

    /// <summary>
    /// The time to the reset on a curve's own clock, none before its date: the time over which a parallel shift of the
    /// curve's zero rates discounts the value to the reset, where QuantLib reads the risk-free curve's year fraction for
    /// every curve (the S-exo plan's ER5 (xxiv)).
    /// </summary>
    /// <param name="option">The forward-start option.</param>
    /// <param name="curve">The curve.</param>
    /// <returns>The reset time.</returns>
    protected static double ResetTime(ForwardVanillaOption option, TermStructure curve)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(curve);
        return curve.AccrualTimeTo(option.ResetDate);
    }

    /// <summary>
    /// A curve's continuous zero rate to the reset: QuantLib's <c>zeroRate(resetDate, dayCounter, Continuous)</c> where
    /// the curve shares the clock's times; otherwise the rate of the discount it accrues over the clock's reset time.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <param name="option">The forward-start option.</param>
    /// <param name="clock">The process's clock.</param>
    /// <returns>The rate.</returns>
    protected static double ResetRate(YieldTermStructure curve, ForwardVanillaOption option, TermStructure clock)
    {
        ArgumentNullException.ThrowIfNull(option);
        return RateTo(curve, option.ResetDate, clock);
    }

    /// <summary>
    /// The vega under a parallel shift of the input volatilities (<see cref="ShiftedVega(BlackVolTermStructure, Date,
    /// Date, double, bool, double)"/>) over the option's period, from the reset to expiry, at the inner strike, the
    /// period at its forward where the spot's forward over it is close to that strike.
    /// </summary>
    /// <param name="option">The forward-start option.</param>
    /// <param name="innerVega">The inner engine's vega.</param>
    /// <returns>The shifted vega.</returns>
    protected double ShiftedVega(ForwardVanillaOption option, double innerVega)
    {
        ArgumentNullException.ThrowIfNull(option);
        Date reset = option.ResetDate, expiry = option.Exercise.LastDate();
        TermStructure clock = FdmBlackScholesOp.Clock(Process);
        YieldTermStructure rTS = Process.RiskFreeRate, qTS = Process.DividendYield;
        double rDiscount = rTS.AccruedDiscountOn(clock, expiry) / rTS.AccruedDiscountOn(clock, reset);
        double qDiscount = qTS.AccruedDiscountOn(clock, expiry) / qTS.AccruedDiscountOn(clock, reset);
        double strike = option.Moneyness * Process.X0;
        bool atTheForward = ComparisonFunctions.Close(Process.X0 * qDiscount / rDiscount, strike);
        return ShiftedVega(Process.BlackVolatility, reset, expiry, strike, atTheForward, innerVega);
    }

    // A curve's continuous zero rate to a date: QuantLib's zeroRate(date, dayCounter, Continuous, NoFrequency) where the
    // curve shares the clock's times; otherwise the rate of the discount it accrues over the clock's time to the date.
    internal static double RateTo(YieldTermStructure curve, Date d, TermStructure clock)
    {
        ArgumentNullException.ThrowIfNull(curve);
        ArgumentNullException.ThrowIfNull(clock);
        return curve.SharesTimes(clock)
            ? curve.ZeroRate(d, curve.DayCounter, Compounding.Continuous, Frequency.NoFrequency).Rate
            : curve.ContinuousForwardRateAt(clock, 0.0, clock.TimeFromReference(d));
    }

    // The vega of a forward-start period under a parallel shift ε of the input volatilities, from its vega in the
    // period's forward volatility σ_f (the S-exo plan's ER5 (xxxvii)): times dσ_f/dε = (σ₂t₂ − σ₁t₁)/(σ_f·Δt), the
    // volatilities at the period's start and end read at the strike on the volatility's own times. Exactly the given
    // vega where the two volatilities are equal, zero among them, or the period starts on or before the volatility's
    // reference date; at a zero forward variance between unequal volatilities the one-sided limit, infinite where the
    // period is at its forward (atTheForward: Black's at-the-money condition, close(forward, strike), where the price
    // rises with the square root of the shift) and 0 elsewhere. The given vega can't tell them apart: Black's vega is 0
    // at a zero deviation.
    internal static double ShiftedVega(
        BlackVolTermStructure vol, Date start, Date end, double strike, bool atTheForward, double periodVega)
    {
        double t1 = vol.AccrualTimeTo(start);
        if (t1 == 0.0)
        {
            return periodVega;
        }

        double t2 = vol.AccrualTimeTo(end);
        double s1 = vol.BlackVol(start, strike);
        double s2 = vol.BlackVol(end, strike);
        if (s1 == s2)
        {
            return periodVega;
        }

        double forwardVariance = (s2 * s2 * t2) - (s1 * s1 * t1);
        if (!(forwardVariance > 0.0))
        {
            return atTheForward ? double.PositiveInfinity : 0.0;
        }

        return periodVega * ((s2 * t2) - (s1 * t1)) / Math.Sqrt(forwardVariance * (t2 - t1));
    }
}
