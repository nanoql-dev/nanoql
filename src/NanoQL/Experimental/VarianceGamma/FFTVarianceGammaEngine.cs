// SPDX-FileCopyrightText: Copyright (C) 2010 Adrian O' Neill
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/variancegamma/fftvariancegammaengine.hpp ql/experimental/variancegamma/fftvariancegammaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The fast-Fourier-transform engine for European options under the variance gamma process: one transform prices every
/// option of an expiry (<see cref="FFTEngine"/>).
/// </summary>
/// <remarks>
/// Immutable. Each curve's discount is read by date, nothing accrued before its own date, and the time on the
/// risk-free clock from its reference date, as QuantLib's (the S-xeq plan's XER5 (xxxiii)). The state reports the strip
/// of exponents u with E[S^u] finite, the roots of 1 − θνu − σ²νu²/2, which the base's damping reads (XER5 (xviii));
/// where 1 − θν − σ²ν/2 isn't positive the spot's mean is infinite and the engine refuses, where QuantLib returns NaN
/// (XER5 (xvii)).
/// </remarks>
[Experimental("NQL0001")]
public sealed class FFTVarianceGammaEngine : FFTEngine
{
    /// <summary>Initializes a new instance of the <see cref="FFTVarianceGammaEngine"/> class.</summary>
    /// <param name="process">The variance gamma process.</param>
    /// <param name="logStrikeSpacing">The grid's log-strike spacing λ.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    public FFTVarianceGammaEngine(VarianceGammaProcess process, double logStrikeSpacing = 0.001)
        : base(logStrikeSpacing)
    {
        ArgumentNullException.ThrowIfNull(process);
        Process = process;
    }

    /// <summary>Gets the variance gamma process.</summary>
    public VarianceGammaProcess Process { get; }

    /// <inheritdoc/>
    protected override double X0 => Process.X0;

    /// <summary>Prices a European option: QuantLib's uncached route.</summary>
    /// <param name="option">The option: European, on a striked payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The variance gamma process.</param>
    /// <param name="logStrikeSpacing">The grid's log-strike spacing λ.</param>
    /// <returns>The option's results: the value; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't striked, 1 − θν − σ²ν/2 isn't positive, or the grid can't resolve
    /// the market.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option, EvaluationContext context, VarianceGammaProcess process, double logStrikeSpacing = 0.001) =>
        new FFTVarianceGammaEngine(process, logStrikeSpacing).Calculate(option, context);

    /// <summary>Prices a list of European options, one transform per expiry (the S-xeq plan's XER1).</summary>
    /// <param name="options">The options, each European on a striked payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The variance gamma process.</param>
    /// <param name="logStrikeSpacing">The grid's log-strike spacing λ.</param>
    /// <returns>Each option's results in the list's order.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// An exercise isn't European, a payoff isn't striked, 1 − θν − σ²ν/2 isn't positive, or the grid can't resolve the
    /// market.
    /// </exception>
    public static ImmutableArray<OneAssetOptionResults> Calculate(
        IReadOnlyList<VanillaOption> options,
        EvaluationContext context,
        VarianceGammaProcess process,
        double logStrikeSpacing = 0.001) =>
        new FFTVarianceGammaEngine(process, logStrikeSpacing).Calculate(options, context);

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">1 − θν − σ²ν/2 isn't positive.</exception>
    protected override ExpiryState PrecalculateExpiry(Date d, EvaluationContext context)
    {
        double sigma = Process.Sigma, nu = Process.Nu, theta = Process.Theta;
        VarianceGammaEngine.CheckMoment(sigma, nu, theta);
        double dividendDiscount = Process.DividendYield.AccruedDiscount(d);
        double riskFreeDiscount = Process.RiskFreeRate.AccruedDiscount(d);
        DayCounter rfdc = Process.RiskFreeRate.DayCounter;
        double t = rfdc.YearFraction(Process.RiskFreeRate.ReferenceDate, d);
        return new State(Process.X0, dividendDiscount, riskFreeDiscount, t, sigma, nu, theta);
    }

    // QuantLib's expiry members: the discounts, the time and the parameters; the strip from the moment condition
    private sealed class State(
        double s, double dividendDiscount, double riskFreeDiscount, double t, double sigma, double nu, double theta)
        : ExpiryState
    {
        public override double DiscountFactor => riskFreeDiscount;

        public override double DividendYield => dividendDiscount;

        // the roots of 1 − θνu − σ²νu²/2 = 0, u = (−θν ∓ √(θ²ν² + 2σ²ν))/(σ²ν)
        public override double? StripLower => ((-theta * nu) - Root) / (sigma * sigma * nu);

        public override double? StripUpper => ((-theta * nu) + Root) / (sigma * sigma * nu);

        private double Root => Math.Sqrt((theta * theta * nu * nu) + (2.0 * sigma * sigma * nu));

        public override Complex ComplexFourierTransform(Complex u)
        {
            var i1 = new Complex(0, 1);
            double omega = Math.Log(1.0 - (theta * nu) - (sigma * sigma * nu / 2.0)) / nu;
            Complex phi = Complex.Exp(i1 * u * (Math.Log(s) + (omega * t)))
                * Complex.Pow(dividendDiscount / riskFreeDiscount, i1 * u);
            phi *= Complex.Pow(1.0 - (i1 * theta * nu * u) + (sigma * sigma * nu * u * u / 2.0), -t / nu);
            return phi;
        }
    }
}
