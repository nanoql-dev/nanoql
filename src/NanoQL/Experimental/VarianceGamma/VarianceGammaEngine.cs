// SPDX-FileCopyrightText: Copyright (C) 2010 Adrian O' Neill
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/variancegamma/analyticvariancegammaengine.hpp ql/experimental/variancegamma/analyticvariancegammaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;

namespace NanoQL.Experimental;

/// <summary>
/// The analytic engine for European options under the variance gamma process: the Black-Scholes price conditional on
/// the gamma time, integrated against the gamma density.
/// </summary>
/// <remarks>
/// <para>
/// Each curve's discount is read by date, nothing accrued before its own date, and the time on the risk-free clock
/// from its reference date, as QuantLib's (the S-xeq plan's XER5 (xxxiii)).
/// </para>
/// <para>
/// Documented divergences (<c>Divergences/VarianceGammaEngineTests.cs</c>). The gamma density is computed in logs
/// where QuantLib's denominator, Γ(t/ν)·ν^(t/ν), isn't finite and nonzero (t/ν past about 170, ν at most 0.005 at a
/// year), where QuantLib's product of an overflow and an underflow makes the integrand NaN and the integration throws;
/// elsewhere QuantLib's form stands (XER5 (xvi)); and where ν &lt; t/100 the integration's bound starts at t + 15√(νt),
/// beyond the gamma time's mean, where QuantLib's 15√(νt) lies below it once ν &lt; t/225 and misses the whole mass
/// (the S-xeq run's R2-4). Where 1 − θν − σ²ν/2 isn't positive the spot's mean is infinite and
/// no risk-neutral price exists: the engine refuses, where QuantLib throws "forward (-nan(ind)) must be positive"
/// (XER5 (xvii)).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public static class VarianceGammaEngine
{
    /// <summary>Prices a European option under the variance gamma process.</summary>
    /// <param name="option">The option: European, on a striked payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The variance gamma process.</param>
    /// <param name="absoluteError">The integrations' absolute error, positive.</param>
    /// <returns>The option's results: the value; the expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="absoluteError"/> isn't positive.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't striked, 1 − θν − σ²ν/2 isn't positive, or an integration fails.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option, EvaluationContext context, VarianceGammaProcess process, double absoluteError = 1e-5)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (!(absoluteError > 0))
        {
            throw new ArgumentException("absolute error must be positive", nameof(absoluteError));
        }

        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(new OneAssetOptionResults { NPV = Value(option, process, absoluteError) });
    }

    // QuantLib's 1 − θν − σ²ν/2 > 0, the spot's first moment, without which no price exists (XER5 (xvii))
    internal static void CheckMoment(double sigma, double nu, double theta)
    {
        if (!(1.0 - (theta * nu) - (sigma * sigma * nu / 2.0) > 0.0))
        {
            throw new NanoQLException(
                "1 - theta*nu - sigma^2*nu/2 must be positive: the variance gamma spot's mean is infinite");
        }
    }

    private static double Value(VanillaOption option, VarianceGammaProcess process, double absErr)
    {
        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European Option");
        }

        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        CheckMoment(process.Sigma, process.Nu, process.Theta);

        // by date, nothing accrued before each curve's own date
        Time.Date lastDate = option.Exercise.LastDate();
        double dividendDiscount = process.DividendYield.AccruedDiscount(lastDate);
        double riskFreeDiscount = process.RiskFreeRate.AccruedDiscount(lastDate);

        Time.DayCounter rfdc = process.RiskFreeRate.DayCounter;
        double t = rfdc.YearFraction(process.RiskFreeRate.ReferenceDate, lastDate);

        var f = new Integrand(
            payoff, process.X0, t, riskFreeDiscount, dividendDiscount, process.Sigma, process.Nu, process.Theta);

        double infinity = 15.0 * Math.Sqrt(process.Nu * t);

        // beyond the gamma time's mean where QuantLib's start lies near or below it (R2-4)
        if (process.Nu < t / 100.0)
        {
            infinity += t;
        }
        double target = absErr * 1e-4;
        double val = f.Evaluate(infinity);
        while (Math.Abs(val) > target)
        {
            infinity *= 1.5;
            val = f.Evaluate(infinity);
        }

        // the integration is split due to occasional singularities at 0
        double split = 0.1;
        var integrator1 = new GaussKronrodNonAdaptive(absErr, 1000, 0);
        double pvA = integrator1.Integrate(f.Evaluate, 0, split);

        var integrator2 = new GaussLobattoIntegral(2000, absErr);
        double pvB = integrator2.Integrate(f.Evaluate, split, infinity);

        return pvA + pvB;
    }

    // QuantLib's integrand: the Black-Scholes price at the gamma time x times the gamma density, in logs where
    // QuantLib's denominator isn't finite and nonzero (XER5 (xvi))
    private sealed class Integrand
    {
        private readonly StrikedTypePayoff payoff;
        private readonly double s0;
        private readonly double t;
        private readonly double riskFreeDiscount;
        private readonly double dividendDiscount;
        private readonly double sigma;
        private readonly double nu;
        private readonly double theta;
        private readonly double omega;
        private readonly double gammaDenom;
        private readonly double logGammaDenom;
        private readonly bool inLogs;

        public Integrand(
            StrikedTypePayoff payoff,
            double s0,
            double t,
            double riskFreeDiscount,
            double dividendDiscount,
            double sigma,
            double nu,
            double theta)
        {
            this.payoff = payoff;
            this.s0 = s0;
            this.t = t;
            this.riskFreeDiscount = riskFreeDiscount;
            this.dividendDiscount = dividendDiscount;
            this.sigma = sigma;
            this.nu = nu;
            this.theta = theta;
            omega = Math.Log(1.0 - (theta * nu) - (sigma * sigma * nu / 2.0)) / nu;

            // We can precompute the denominator of the gamma pdf (does not depend on x)
            // shape = t_/nu_, scale = nu_
            var gf = new GammaFunction();
            double logGamma = gf.LogValue(t / nu);
            gammaDenom = Math.Exp(logGamma) * Math.Pow(nu, t / nu);
            inLogs = !(double.IsFinite(gammaDenom) && gammaDenom != 0.0);
            logGammaDenom = logGamma + (t / nu * Math.Log(nu));
        }

        public double Evaluate(double x)
        {
            // Compute adjusted black scholes price
            double s0Adj = s0 * Math.Exp((theta * x) + (omega * t) + (sigma * sigma * x / 2.0));
            double volAdj = sigma * Math.Sqrt(x / t);
            volAdj *= Math.Sqrt(t);

            var bs = new BlackScholesCalculator(payoff, s0Adj, dividendDiscount, volAdj, riskFreeDiscount);
            double bsprice = bs.Value;

            // Multiply by gamma distribution
            double gamp = inLogs
                ? Math.Exp((((t / nu) - 1.0) * Math.Log(x)) - (x / nu) - logGammaDenom)
                : Math.Pow(x, (t / nu) - 1.0) * Math.Exp(-x / nu) / gammaDenom;
            return bsprice * gamp;
        }
    }
}
