// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/svismilesection.hpp ql/experimental/volatility/svismilesection.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Experimental;

/// <summary>
/// The stochastic-volatility-inspired (SVI) smile section: the volatility whose total variance at a log-moneyness k
/// is a + b(ρ(k − m) + √((k − m)² + σ²)).
/// </summary>
/// <remarks>
/// The strike is floored at 1e-6, a NaN strike's volatility NaN, and a negative total variance gives a volatility of
/// 0, as QuantLib's <c>std::max</c> calls do. QuantLib's date constructor, whose section floats with the evaluation
/// date, is <see cref="Create"/>, which takes the evaluation context and fixes the date.
/// </remarks>
[Experimental("NQL0001")]
public sealed class SviSmileSection : SmileSection
{
    private readonly double forward;
    private readonly ImmutableArray<double> parameters;

    /// <summary>Initializes a new instance of the <see cref="SviSmileSection"/> class for a time to expiry.</summary>
    /// <param name="timeToExpiry">The time to expiry, positive.</param>
    /// <param name="forward">The forward at the expiry.</param>
    /// <param name="sviParameters">a, b, σ, ρ and m, in that order.</param>
    /// <exception cref="ArgumentException">The time isn't positive, there aren't five parameters, or they fail
    /// <see cref="SviInterpolationFunctions.CheckSviParameters"/>.</exception>
    public SviSmileSection(double timeToExpiry, double forward, ReadOnlySpan<double> sviParameters)
        : base(timeToExpiry)
    {
        this.forward = forward;
        parameters = [.. sviParameters];
        Init(nameof(timeToExpiry));
    }

    private SviSmileSection(
        Date d, double forward, ReadOnlySpan<double> sviParameters, Date referenceDate, DayCounter dc)
        : base(d, dc, referenceDate)
    {
        this.forward = forward;
        parameters = [.. sviParameters];
        Init(nameof(d));
    }

    /// <summary>An SVI smile section for an expiry date, whose reference date is the evaluation date.</summary>
    /// <param name="d">The expiry date, after the evaluation date.</param>
    /// <param name="forward">The forward at the expiry.</param>
    /// <param name="sviParameters">a, b, σ, ρ and m, in that order.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the reference date.</param>
    /// <param name="dc">The day counter of the time to expiry, or <see langword="null"/> for QuantLib's default,
    /// Actual/365 (Fixed).</param>
    /// <returns>The section, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c> or before the evaluation
    /// date, the time to expiry isn't positive, there aren't five parameters, or they fail
    /// <see cref="SviInterpolationFunctions.CheckSviParameters"/>.</exception>
    public static SviSmileSection Create(
        Date d, double forward, ReadOnlySpan<double> sviParameters, EvaluationContext context, DayCounter? dc = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SviSmileSection(d, forward, sviParameters, context.EvaluationDate, dc ?? new Actual365Fixed());
    }

    /// <summary>The minimum strike: 0.</summary>
    /// <returns>The strike.</returns>
    public override double MinStrike() => 0.0;

    /// <summary>The maximum strike: the largest real.</summary>
    /// <returns>The strike.</returns>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <summary>The at-the-money level: the forward.</summary>
    /// <returns>The forward.</returns>
    public override double? AtmLevel() => forward;

    /// <inheritdoc/>
    protected override double VolatilityImpl(double strike)
    {
        double k = Math.Log((strike < 1E-6 ? 1E-6 : strike) / forward);
        double totalVariance = SviInterpolationFunctions.SviTotalVariance(
            parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], k);
        double variance = totalVariance / ExerciseTime;
        return Math.Sqrt(0.0 < variance ? variance : 0.0);
    }

    private void Init(string timeName)
    {
        if (!(ExerciseTime > 0.0))
        {
            throw new ArgumentException("svi expects a strictly positive expiry time", timeName);
        }

        if (!(parameters.Length == 5))
        {
            throw new ArgumentException(
                $"svi expects 5 parameters (a,b,sigma,rho,m) but ({parameters.Length}) given", "sviParameters");
        }

        SviInterpolationFunctions.CheckSviParameters(
            parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], ExerciseTime);
    }
}
