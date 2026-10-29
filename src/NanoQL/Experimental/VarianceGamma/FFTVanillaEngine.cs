// SPDX-FileCopyrightText: Copyright (C) 2010 Adrian O' Neill
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/variancegamma/fftvanillaengine.hpp ql/experimental/variancegamma/fftvanillaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The fast-Fourier-transform engine for European options under Black-Scholes, on a constant volatility: one transform
/// prices every option of an expiry (<see cref="FFTEngine"/>).
/// </summary>
/// <remarks>
/// Immutable. Each curve's discount is read by date, nothing accrued before its own date, and the time on the
/// risk-free clock from its reference date, as QuantLib's (the S-xeq plan's XER5 (xxxiii)). Every moment is finite, so
/// the damping and grid are always QuantLib's.
/// </remarks>
[Experimental("NQL0001")]
public sealed class FFTVanillaEngine : FFTEngine
{
    /// <summary>Initializes a new instance of the <see cref="FFTVanillaEngine"/> class.</summary>
    /// <param name="process">The Black-Scholes process, on a constant volatility.</param>
    /// <param name="logStrikeSpacing">The grid's log-strike spacing λ.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    public FFTVanillaEngine(GeneralizedBlackScholesProcess process, double logStrikeSpacing = 0.001)
        : base(logStrikeSpacing)
    {
        ArgumentNullException.ThrowIfNull(process);
        Process = process;
    }

    /// <summary>Gets the Black-Scholes process.</summary>
    public GeneralizedBlackScholesProcess Process { get; }

    /// <inheritdoc/>
    protected override double X0 => Process.X0;

    /// <summary>Prices a European option: QuantLib's uncached route.</summary>
    /// <param name="option">The option: European, on a striked payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process, on a constant volatility.</param>
    /// <param name="logStrikeSpacing">The grid's log-strike spacing λ.</param>
    /// <returns>The option's results: the value; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The exercise isn't European, the payoff isn't striked, or the volatility isn't
    /// constant.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option, EvaluationContext context, GeneralizedBlackScholesProcess process, double logStrikeSpacing = 0.001) =>
        new FFTVanillaEngine(process, logStrikeSpacing).Calculate(option, context);

    /// <summary>Prices a list of European options, one transform per expiry (the S-xeq plan's XER1).</summary>
    /// <param name="options">The options, each European on a striked payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process, on a constant volatility.</param>
    /// <param name="logStrikeSpacing">The grid's log-strike spacing λ.</param>
    /// <returns>Each option's results in the list's order.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">An exercise isn't European, a payoff isn't striked, or the volatility isn't
    /// constant.</exception>
    public static ImmutableArray<OneAssetOptionResults> Calculate(
        IReadOnlyList<VanillaOption> options,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        double logStrikeSpacing = 0.001) =>
        new FFTVanillaEngine(process, logStrikeSpacing).Calculate(options, context);

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The volatility isn't a <see cref="BlackConstantVol"/> ("Constant volatility
    /// required").</exception>
    protected override ExpiryState PrecalculateExpiry(Date d, EvaluationContext context)
    {
        double dividendDiscount = Process.DividendYield.AccruedDiscount(d);
        double riskFreeDiscount = Process.RiskFreeRate.AccruedDiscount(d);
        DayCounter rfdc = Process.RiskFreeRate.DayCounter;
        double t = rfdc.YearFraction(Process.RiskFreeRate.ReferenceDate, d);
        if (Process.BlackVolatility is not BlackConstantVol constVol)
        {
            throw new NanoQLException("Constant volatility required");
        }

        double vol = constVol.BlackVol(0.0, 0.0);
        return new State(Process.X0, dividendDiscount, riskFreeDiscount, t, vol * vol);
    }

    // QuantLib's expiry members: the discounts, the time and the variance
    private sealed class State(double s, double dividendDiscount, double riskFreeDiscount, double t, double var)
        : ExpiryState
    {
        public override double DiscountFactor => riskFreeDiscount;

        public override double DividendYield => dividendDiscount;

        public override Complex ComplexFourierTransform(Complex u)
        {
            var i1 = new Complex(0, 1);
            Complex phi = Complex.Exp((i1 * u * (Math.Log(s) - (var * t / 2.0))) - (var * u * u * t / 2.0));
            phi *= Complex.Pow(dividendDiscount / riskFreeDiscount, i1 * u);
            return phi;
        }
    }
}
