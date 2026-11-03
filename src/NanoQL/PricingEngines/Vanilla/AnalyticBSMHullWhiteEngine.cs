// SPDX-FileCopyrightText: Copyright (C) 2007 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analyticbsmhullwhiteengine.hpp ql/pricingengines/vanilla/analyticbsmhullwhiteengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic European option pricer including stochastic interest rates: the Black-Scholes engine on the equity
/// variance shifted by the Hull-White rate's variance and its covariance with the equity (Brigo and Mercurio,
/// "Interest Rate Models").
/// </summary>
/// <remarks>
/// <para>
/// The engine reports the analytic European engine's results on the shifted variance. The correctness of the returned
/// value is tested by reproducing results available in web and literature.
/// </para>
/// <para>
/// QuantLib's offset takes the Hull-White terms over the risk-free curve's time and the equity volatility as its own
/// structure quotes it (<c>analyticbsmhullwhiteengine.cpp:86-102</c>), which differ where the two are counted
/// differently; here the equity volatility in the covariance is its variance per unit of the risk-free curve's time,
/// the offset's own, QuantLib's where they share their times (<c>Divergences/BsmHullWhiteMismatchedTimesTests.cs</c>).
/// Below a·t = 1e-2 the offset is its series to (a·t)⁴, QuantLib's three terms first, where QuantLib's closed form,
/// used above (a·t)⁴ = ε, cancels to a relative error of about 3ε/(a·t)³
/// (<c>Divergences/BsmHullWhiteSmallReversionTests.cs</c>).
/// </para>
/// </remarks>
public static class AnalyticBSMHullWhiteEngine
{
    // where the series to (a·t)⁴ replaces the closed form
    private const double SeriesCut = 1e-2;

    /// <summary>Prices a European vanilla option with a Hull-White short rate.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="equityShortRateCorrelation">The correlation of the equity with the short rate.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="model">The Hull-White model.</param>
    /// <returns>The option's results; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The underlying isn't positive, the payoff isn't striked, or the analytic European engine refuses.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        double equityShortRateCorrelation,
        GeneralizedBlackScholesProcess process,
        HullWhite model)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(model);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();

        if (!(process.X0 > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        Exercise exercise = option.Exercise;
        Date lastDate = exercise.LastDate();

        double t = process.RiskFreeRate.DayCounter.YearFraction(process.RiskFreeRate.ReferenceDate, lastDate);

        double a = model.A();
        double sigma = model.Sigma;

        // the equity volatility per unit of the offset's time, QuantLib's quote where they share their times
        BlackVolTermStructure vol = process.BlackVolatility;
        double eta = vol.SharesTimes(process.RiskFreeRate)
            ? vol.BlackVol(lastDate, payoff.Strike)
            : (t > 0.0 ? Math.Sqrt(vol.AccruedVariance(lastDate, payoff.Strike) / t) : 0.0);
        double rho = equityShortRateCorrelation;

        double varianceOffset;
        if (a * t > SeriesCut)
        {
            double v = sigma * sigma / (a * a)
                * (t + (2 / a * Math.Exp(-a * t)) - (1 / (2 * a) * Math.Exp(-2 * a * t)) - (3 / (2 * a)));
            double mu = 2 * rho * sigma * eta / a * (t - (1 / a * (1 - Math.Exp(-a * t))));

            varianceOffset = v + mu;
        }
        else
        {
            // low-a algebraic limit, to (a·t)⁴
            double v = sigma * sigma * t * t * t
                * ((1 / 3.0) - (0.25 * a * t) + (7 / 60.0 * a * a * t * t)
                   - (a * a * a * t * t * t / 24.0) + (31 / 2520.0 * a * a * a * a * t * t * t * t));
            double mu = rho * sigma * eta * t * t
                * (1 - (a * t / 3.0) + (a * a * t * t / 12.0)
                   - (a * a * a * t * t * t / 60.0) + (a * a * a * a * t * t * t * t / 360.0));

            varianceOffset = v + mu;
        }

        var volTS = new ShiftedBlackVolTermStructure(varianceOffset, vol);

        var adjProcess = new GeneralizedBlackScholesProcess(
            process.StateVariable, process.DividendYield, process.RiskFreeRate, volTS);

        OneAssetOptionResults results = AnalyticEuropeanEngine.Body(
            new VanillaOption(payoff, exercise), adjProcess, adjProcess.RiskFreeRate);
        return option.FetchResults(results);
    }

    // The volatility with a constant variance offset, on the original's dates, day counter and strikes.
    private sealed class ShiftedBlackVolTermStructure(double varianceOffset, BlackVolTermStructure volTS)
        : BlackVolTermStructure(volTS.ReferenceDate, volTS.Calendar, BusinessDayConvention.Following, volTS.DayCounter)
    {
        public override Date MaxDate => volTS.MaxDate;

        public override double MinStrike() => volTS.MinStrike();

        public override double MaxStrike() => volTS.MaxStrike();

        protected override double BlackVarianceImpl(double t, double strike) =>
            volTS.BlackVariance(t, strike, true) + varianceOffset;

        protected override double BlackVolImpl(double t, double strike)
        {
            double nonZeroMaturity = t == 0.0 ? 0.00001 : t;
            double var = BlackVarianceImpl(nonZeroMaturity, strike);
            return Math.Sqrt(var / nonZeroMaturity);
        }
    }
}
