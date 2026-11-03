// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/quanto/quantoengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The quanto engine: an option's own engine run on the underlying's process with its dividend curve carrying the
/// quanto adjustment, with the quanto sensitivities added.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's <c>QuantoEngine&lt;Instr, Engine&gt;</c> as a static class over a delegate in the engines' form: each
/// overload runs the instrument's steps, builds QuantLib's quanto process (<see cref="QuantoTermStructure"/> as its
/// dividend curve, at the drift's strike and the exchange rate's at-the-money level 1) and calls the engine on the
/// instrument's terms as the inner engine's own instrument, QuantLib's arguments copy; the generic core takes any
/// one-asset instrument and its terms, so later engines and a user's own quanto instruments plug in.
/// </para>
/// <para>
/// <see cref="QuantoOptionResults.Qvega"/> and <see cref="QuantoOptionResults.Qlambda"/> read the underlying's
/// volatility at the drift's strike, where QuantLib reads it at the spot, so on a smile they are the derivatives of the
/// price the drift gives (<c>Divergences/QuantoSmileTests.cs</c>); on a flat volatility, QuantLib's. A quanto
/// forward-start option's drift is read at the moneyness times the spot, the strike its forward engine prices at, where
/// QuantLib reads it at the payoff's strike, which the forward engine ignores.
/// </para>
/// <para>
/// The rho, vega, <see cref="QuantoOptionResults.Qrho"/>, <see cref="QuantoOptionResults.Qvega"/> and
/// <see cref="QuantoOptionResults.Qlambda"/> come from the inner dividend rho through each input's change to the quanto
/// curve's log discount, by date, at the dates the inner engine reads: at expiry, and for a forward-start option at the
/// reset too. QuantLib's assume the input moves the quanto zero rate by one factor at expiry, which misses where a
/// structure counts or dates its time apart from the dividend curve, whose clock the quanto curve keeps, and for a
/// forward-start option where an input's contribution varies between the reset and expiry, as on a volatility term
/// structure (<c>Divergences/QuantoGreeksTermStructureTests.cs</c>). Where every structure shares the dividend curve's
/// times and the factor is one, QuantLib's formula.
/// </para>
/// </remarks>
public static class QuantoEngine
{
    /// <summary>Prices a quanto vanilla option: QuantLib's <c>QuantoEngine&lt;VanillaOption, Engine&gt;</c>.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The underlying's process, in its own currency.</param>
    /// <param name="foreignRiskFreeRate">The underlying currency's risk-free curve.</param>
    /// <param name="exchangeRateVolatility">The exchange rate's volatility.</param>
    /// <param name="correlation">The correlation of the underlying and the exchange rate.</param>
    /// <param name="engine">The vanilla engine run on the quanto process.</param>
    /// <returns>The option's results, with the quanto sensitivities.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The spot isn't positive, or the inner engine refuses.</exception>
    public static QuantoOptionResults Calculate(
        QuantoVanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        YieldTermStructure foreignRiskFreeRate,
        BlackVolTermStructure exchangeRateVolatility,
        double correlation,
        Func<VanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults> engine)
    {
        ArgumentNullException.ThrowIfNull(option);
        var underlying = new VanillaOption((StrikedTypePayoff)option.Payoff!, option.Exercise);
        return (QuantoOptionResults)Calculate(
            option, underlying, context, process, foreignRiskFreeRate, exchangeRateVolatility, correlation, engine);
    }

    /// <summary>Prices a quanto barrier option: QuantLib's <c>QuantoEngine&lt;BarrierOption, Engine&gt;</c>.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The underlying's process, in its own currency.</param>
    /// <param name="foreignRiskFreeRate">The underlying currency's risk-free curve.</param>
    /// <param name="exchangeRateVolatility">The exchange rate's volatility.</param>
    /// <param name="correlation">The correlation of the underlying and the exchange rate.</param>
    /// <param name="engine">The barrier engine run on the quanto process, such as
    /// <c>AnalyticBarrierEngine.Calculate</c>.</param>
    /// <returns>The option's results, with the quanto sensitivities where the barrier engine gives a dividend
    /// rho.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The spot isn't positive, or the inner engine refuses.</exception>
    /// <remarks>
    /// The barrier engine prices the option's terms as a <see cref="BarrierOption"/> of its own; it reads the quanto
    /// curve at the option's expiry alone, as the analytic barrier engine does, so the derived Greeks' terms at expiry
    /// are its own.
    /// </remarks>
    public static QuantoOptionResults Calculate(
        QuantoBarrierOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        YieldTermStructure foreignRiskFreeRate,
        BlackVolTermStructure exchangeRateVolatility,
        double correlation,
        Func<BarrierOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults> engine)
    {
        ArgumentNullException.ThrowIfNull(option);
        var underlying = new BarrierOption(
            option.BarrierType, option.Barrier, option.Rebate, (StrikedTypePayoff)option.Payoff!, option.Exercise);
        return (QuantoOptionResults)Calculate(
            option, underlying, context, process, foreignRiskFreeRate, exchangeRateVolatility, correlation, engine);
    }

    /// <summary>
    /// Prices a quanto double barrier option: QuantLib's <c>QuantoEngine&lt;DoubleBarrierOption, Engine&gt;</c>.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The underlying's process, in its own currency.</param>
    /// <param name="foreignRiskFreeRate">The underlying currency's risk-free curve.</param>
    /// <param name="exchangeRateVolatility">The exchange rate's volatility.</param>
    /// <param name="correlation">The correlation of the underlying and the exchange rate.</param>
    /// <param name="engine">The double-barrier engine run on the quanto process, such as
    /// <c>AnalyticDoubleBarrierEngine.Calculate</c>.</param>
    /// <returns>The option's results, with the quanto sensitivities where the double-barrier engine gives a dividend
    /// rho.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The spot isn't positive, or the inner engine refuses.</exception>
    /// <remarks>
    /// The double-barrier engine prices the option's terms as a <see cref="DoubleBarrierOption"/> of its own;
    /// QuantLib's analytic double-barrier engine gives no Greeks, so with it the derived sensitivities are absent, as
    /// QuantLib's are.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.Experimental("NQL0001")]
    public static QuantoOptionResults Calculate(
        Experimental.QuantoDoubleBarrierOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        YieldTermStructure foreignRiskFreeRate,
        BlackVolTermStructure exchangeRateVolatility,
        double correlation,
        Func<DoubleBarrierOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults> engine)
    {
        ArgumentNullException.ThrowIfNull(option);
        var underlying = new DoubleBarrierOption(
            option.BarrierType,
            option.BarrierLo,
            option.BarrierHi,
            option.Rebate,
            (StrikedTypePayoff)option.Payoff!,
            option.Exercise);
        return (QuantoOptionResults)Calculate(
            option, underlying, context, process, foreignRiskFreeRate, exchangeRateVolatility, correlation, engine);
    }

    /// <summary>
    /// Prices a quanto forward-start option: QuantLib's <c>QuantoEngine&lt;ForwardVanillaOption, Engine&gt;</c>, the
    /// drift read at the forward engine's strike.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The underlying's process, in its own currency.</param>
    /// <param name="foreignRiskFreeRate">The underlying currency's risk-free curve.</param>
    /// <param name="exchangeRateVolatility">The exchange rate's volatility.</param>
    /// <param name="correlation">The correlation of the underlying and the exchange rate.</param>
    /// <param name="engine">The forward-start engine run on the quanto process.</param>
    /// <returns>The option's results, with the quanto sensitivities.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The option's terms are invalid, its payoff isn't plain vanilla, the spot isn't positive, or the inner engine
    /// refuses.
    /// </exception>
    public static QuantoOptionResults Calculate(
        QuantoForwardVanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        YieldTermStructure foreignRiskFreeRate,
        BlackVolTermStructure exchangeRateVolatility,
        double correlation,
        Func<ForwardVanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults> engine)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate(context);

        // the forward engine prices a plain vanilla at the moneyness times the spot, its payoff's strike unread
        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        var underlying = new ForwardVanillaOption(
            option.Moneyness,
            option.ResetDate,
            new PlainVanillaPayoff(payoff.OptionType, option.Moneyness * process.X0),
            option.Exercise);
        return (QuantoOptionResults)Core(
            option, underlying, context, process, foreignRiskFreeRate, exchangeRateVolatility, correlation, engine,
            option.ResetDate);
    }

    /// <summary>
    /// Prices a quanto option through an engine on its terms: QuantLib's <c>QuantoEngine&lt;Instr, Engine&gt;</c>, the
    /// generic core the overloads call.
    /// </summary>
    /// <remarks>
    /// The derived Greeks (rho, vega, qrho, qvega, qlambda) take each input's change to the quanto curve at the
    /// underlying's expiry alone: they assume the inner engine reads the quanto curve's dividend discount there and
    /// nowhere earlier, as a European, barrier or double-barrier engine does. An inner engine reading it at an earlier
    /// date too, as the forward-start engine does at its reset, needs that date's terms, which the forward-start
    /// overload adds.
    /// </remarks>
    /// <typeparam name="TOption">The type of the inner engine's instrument.</typeparam>
    /// <param name="option">The quanto instrument, whose steps run and whose results are returned.</param>
    /// <param name="underlying">The instrument's terms as the inner engine's own instrument, its payoff striked.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The underlying's process, in its own currency.</param>
    /// <param name="foreignRiskFreeRate">The underlying currency's risk-free curve.</param>
    /// <param name="exchangeRateVolatility">The exchange rate's volatility.</param>
    /// <param name="correlation">The correlation of the underlying and the exchange rate.</param>
    /// <param name="engine">The inner engine run on the quanto process.</param>
    /// <returns>The instrument's results: a <see cref="QuantoOptionResults"/> as the instrument fetches it.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The underlying's payoff isn't striked, the spot isn't positive, or the inner engine refuses.
    /// </exception>
    public static OneAssetOptionResults Calculate<TOption>(
        OneAssetOption option,
        TOption underlying,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        YieldTermStructure foreignRiskFreeRate,
        BlackVolTermStructure exchangeRateVolatility,
        double correlation,
        Func<TOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults> engine)
        where TOption : OneAssetOption =>
        Core(option, underlying, context, process, foreignRiskFreeRate, exchangeRateVolatility, correlation, engine, null);

    // The generic core, given the date a forward-start inner engine resets on, where it reads the quanto curve besides
    // expiry.
    private static OneAssetOptionResults Core<TOption>(
        OneAssetOption option,
        TOption underlying,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        YieldTermStructure foreignRiskFreeRate,
        BlackVolTermStructure exchangeRateVolatility,
        double correlation,
        Func<TOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults> engine,
        Date? resetDate)
        where TOption : OneAssetOption
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(underlying);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(foreignRiskFreeRate);
        ArgumentNullException.ThrowIfNull(exchangeRateVolatility);
        ArgumentNullException.ThrowIfNull(engine);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(
            Body(underlying, context, process, foreignRiskFreeRate, exchangeRateVolatility, correlation, engine, resetDate));
    }

    // QuantLib's calculate().
    private static QuantoOptionResults Body<TOption>(
        TOption underlying,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        YieldTermStructure foreignRiskFreeRate,
        BlackVolTermStructure exchangeRateVolatility,
        double correlation,
        Func<TOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults> engine,
        Date? resetDate)
        where TOption : OneAssetOption
    {
        // ATM exchangeRate level needed here
        const double exchangeRateATMlevel = 1.0;

        // determine strike from payoff
        if (underlying.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        double strike = payoff.Strike;
        double spot = process.X0;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying");
        }

        // dividendTS needs modification
        var dividendYield = new QuantoTermStructure(
            process.DividendYield,
            process.RiskFreeRate,
            foreignRiskFreeRate,
            process.BlackVolatility,
            strike,
            exchangeRateVolatility,
            exchangeRateATMlevel,
            correlation);
        var quantoProcess = new GeneralizedBlackScholesProcess(
            spot, dividendYield, process.RiskFreeRate, process.BlackVolatility);

        underlying.Validate();
        OneAssetOptionResults originalResults = engine(underlying, context, quantoProcess);

        // each input's effect through the quanto curve, from the inner dividend rho
        var drift = new DriftShifts(
            dividendYield,
            process.RiskFreeRate,
            foreignRiskFreeRate,
            process.BlackVolatility,
            strike,
            exchangeRateVolatility,
            exchangeRateATMlevel,
            correlation,
            underlying.Exercise.LastDate(),
            resetDate);
        double? rho = null, dividendRho = null;
        if (originalResults.Rho is double r && originalResults.DividendRho is double q)
        {
            rho = r + drift.Effect(DriftInput.RiskFree, q, originalResults.NPV);
            dividendRho = q;
        }

        double? vega = originalResults.Vega is double v && originalResults.DividendRho is double qv
            ? v + drift.Effect(DriftInput.Volatility, qv, originalResults.NPV)
            : null;

        double? qvega = null, qrho = null, qlambda = null;
        if (originalResults.DividendRho is double dq)
        {
            // the volatility at the drift's strike, where QuantLib reads it at the spot
            qvega = drift.Effect(DriftInput.ExchangeRateVolatility, dq, originalResults.NPV);
            qrho = drift.Effect(DriftInput.ForeignRiskFree, dq, originalResults.NPV);
            qlambda = drift.Effect(DriftInput.Correlation, dq, originalResults.NPV);
        }

        return new QuantoOptionResults
        {
            NPV = originalResults.NPV,
            Delta = originalResults.Delta,
            Gamma = originalResults.Gamma,
            Theta = originalResults.Theta,
            Rho = rho,
            DividendRho = dividendRho,
            Vega = vega,
            Qvega = qvega,
            Qrho = qrho,
            Qlambda = qlambda,
        };
    }

    // The inputs that move the quanto drift besides the dividend curve.
    private enum DriftInput
    {
        RiskFree,
        ForeignRiskFree,
        Volatility,
        ExchangeRateVolatility,
        Correlation,
    }

    // The change each input makes to the quanto curve's log discount, per unit of its parallel shift, at the dates the
    // inner engine reads.
    private sealed class DriftShifts(
        YieldTermStructure quanto,
        YieldTermStructure riskFree,
        YieldTermStructure foreign,
        BlackVolTermStructure volatility,
        double strike,
        BlackVolTermStructure exchangeRateVolatility,
        double exchangeRateATMlevel,
        double correlation,
        Date lastDate,
        Date? resetDate)
    {
        private readonly bool shared = riskFree.SharesTimes(quanto) && foreign.SharesTimes(quanto)
            && volatility.SharesTimes(quanto) && exchangeRateVolatility.SharesTimes(quanto);

        // An input's effect on the value, from the inner dividend rho: at expiry, the dividend rho times the quanto
        // zero rate's change; for a forward-start option whose input moves the rate unevenly between the reset and
        // expiry, the reset discount's and the forward period's parts apart.
        public double Effect(DriftInput input, double dividendRho, double? value)
        {
            double factorAtExpiry = Factor(input, lastDate);
            if (resetDate is not Date reset || value is not double npv)
            {
                return factorAtExpiry * dividendRho;
            }

            double t1 = quanto.AccrualTimeTo(reset), t2 = quanto.AccrualTimeTo(lastDate);
            if (!(t1 > 0.0) || Factor(input, reset) == factorAtExpiry)
            {
                return factorAtExpiry * dividendRho;
            }

            // -Δ(t1)·V + (dividend rho + t1·V)·(Δ(T) − Δ(t1))/(t_T − t1), Δ the log discount's change
            double logShift1 = LogShift(input, reset, t1), logShift2 = LogShift(input, lastDate, t2);
            return (-logShift1 * npv) + ((dividendRho + (t1 * npv)) * (logShift2 - logShift1) / (t2 - t1));
        }

        // The quanto zero rate's change at a date: QuantLib's factor where every structure shares the quanto curve's
        // times (1, −1, ρ·σ_X, ρ·σ, σ_X·σ), otherwise the log discount's change over the curve's time.
        private double Factor(DriftInput input, Date d)
        {
            if (shared)
            {
                double exchangeRateFlatVol = exchangeRateVolatility.BlackVol(d, exchangeRateATMlevel, true);
                double vol = volatility.BlackVol(d, strike, true);
                return input switch
                {
                    DriftInput.RiskFree => 1.0,
                    DriftInput.ForeignRiskFree => -1.0,
                    DriftInput.Volatility => correlation * exchangeRateFlatVol,
                    DriftInput.ExchangeRateVolatility => correlation * vol,
                    _ => exchangeRateFlatVol * vol,
                };
            }

            double t = quanto.AccrualTimeTo(d);
            return t > 0.0 ? LogShift(input, d, t) / t : Factor(input, d, 0.0001);
        }

        // At a time of 0, QuantLib's rate over its forward-rate step, as the quanto curve's.
        private double Factor(DriftInput input, Date d, double step) => LogShift(input, d, step) / step;

        // The change of the quanto curve's −log discount to time t (the date's, or a step) per unit of the input's shift:
        // a curve's accrued time between the quanto curve's instants 0 and t; a volatility's through the variances'
        // product, ρ·√(V_S·V_X), each variance accrued between those instants on its own clock.
        private double LogShift(DriftInput input, Date d, double t)
        {
            if (shared)
            {
                return Factor(input, d) * t;
            }

            switch (input)
            {
                case DriftInput.RiskFree:
                    return riskFree.AccrualTime(quanto, t) - riskFree.AccrualTime(quanto, 0.0);
                case DriftInput.ForeignRiskFree:
                    return -(foreign.AccrualTime(quanto, t) - foreign.AccrualTime(quanto, 0.0));
            }

            double varianceS = volatility.BlackForwardVarianceAt(quanto, 0.0, t, strike, true);
            double varianceX = exchangeRateVolatility.BlackForwardVarianceAt(quanto, 0.0, t, exchangeRateATMlevel, true);
            return input switch
            {
                DriftInput.Volatility => correlation * Math.Sqrt(varianceX) * DeviationShift(volatility, t, strike, varianceS),
                DriftInput.ExchangeRateVolatility =>
                    correlation * Math.Sqrt(varianceS)
                    * DeviationShift(exchangeRateVolatility, t, exchangeRateATMlevel, varianceX),
                _ => Math.Sqrt(varianceS * varianceX),
            };
        }

        // d√V/dε of a variance accrued between the quanto curve's instants 0 and t under a parallel shift ε of the
        // Black volatility: (σ(b)·τ_b − σ(a)·τ_a)/√V, τ the volatility's own times; √(τ_b − τ_a) at a zero variance.
        private double DeviationShift(BlackVolTermStructure vol, double t, double k, double variance)
        {
            double tauA = vol.AccrualTime(quanto, 0.0), tauB = vol.AccrualTime(quanto, t);
            if (!(variance > 0.0))
            {
                return Math.Sqrt(tauB - tauA);
            }

            double atA = tauA > 0.0 ? vol.BlackVol(tauA, k, true) * tauA : 0.0;
            double atB = tauB > 0.0 ? vol.BlackVol(tauB, k, true) * tauB : 0.0;
            return (atB - atA) / Math.Sqrt(variance);
        }
    }
}
