// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/abcdinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.TermStructures;

namespace NanoQL.Numerics;

/// <summary>
/// The abcd interpolation between discrete points: an <see cref="AbcdCalibration"/> fitted to the volatilities by
/// their times, interpolated by the fitted abcd Black volatility.
/// </summary>
/// <remarks>
/// <para>
/// The fit runs when the interpolation is built: QuantLib's construction followed by its <c>update()</c>, which every
/// QuantLib user calls at once. A new fit is a new interpolation, so <c>update()</c> isn't ported. A guess given as
/// <see langword="null"/> takes QuantLib's default and is free, as QuantLib's <c>Null&lt;Real&gt;()</c>. QuantLib's
/// abcd volatility curve calls <c>update()</c> a second time, a refit from the first fit's coefficients without the
/// constructor's guess check; an internal refit gives it.
/// </para>
/// <para>Immutable and thread-safe once built.</para>
/// </remarks>
public sealed class AbcdInterpolation : Interpolation
{
    // QuantLib's Null<Real>(), which its coefficient holder validates where a guess isn't given.
    private const double NullReal = float.MaxValue;

    private readonly AbcdCalibration abcdCalibrator;
    private readonly bool aIsFixed;
    private readonly bool bIsFixed;
    private readonly bool cIsFixed;
    private readonly bool dIsFixed;
    private readonly bool vegaWeighted;
    private readonly EndCriteria? endCriteria;
    private readonly OptimizationMethod? optMethod;

    /// <summary>Initializes a new instance of the <see cref="AbcdInterpolation"/> class, and fits it.</summary>
    /// <param name="x">The times, sorted.</param>
    /// <param name="y">The volatilities, one per time.</param>
    /// <param name="a">a's guess, or <see langword="null"/> for −0.06, free.</param>
    /// <param name="b">b's guess, or <see langword="null"/> for 0.17, free.</param>
    /// <param name="c">c's guess, or <see langword="null"/> for 0.54, free.</param>
    /// <param name="d">d's guess, or <see langword="null"/> for 0.17, free.</param>
    /// <param name="aIsFixed">Whether a is fixed at its guess, when given.</param>
    /// <param name="bIsFixed">Whether b is fixed at its guess, when given.</param>
    /// <param name="cIsFixed">Whether c is fixed at its guess, when given.</param>
    /// <param name="dIsFixed">Whether d is fixed at its guess, when given.</param>
    /// <param name="vegaWeighted">Whether the errors are weighted by the at-the-money vegas.</param>
    /// <param name="endCriteria">The end criteria, or <see langword="null"/> for
    /// <see cref="AbcdCalibration"/>'s.</param>
    /// <param name="optMethod">The optimizer, or <see langword="null"/> for <see cref="AbcdCalibration"/>'s.</param>
    /// <exception cref="ArgumentException">The sizes differ, there are fewer than two points, or the guesses fail
    /// <see cref="AbcdMathFunction.Validate"/>.</exception>
    /// <exception cref="NanoQLException">The fit fails (<see cref="AbcdCalibration.Compute"/>).</exception>
    public AbcdInterpolation(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        double? a = -0.06,
        double? b = 0.17,
        double? c = 0.54,
        double? d = 0.17,
        bool aIsFixed = false,
        bool bIsFixed = false,
        bool cIsFixed = false,
        bool dIsFixed = false,
        bool vegaWeighted = false,
        EndCriteria? endCriteria = null,
        OptimizationMethod? optMethod = null)
        : base(x, y, 2)
    {
        // QuantLib's AbcdCoeffHolder
        AbcdMathFunction.Validate(a ?? NullReal, b ?? NullReal, c ?? NullReal, d ?? NullReal);
        this.aIsFixed = a is not null && aIsFixed;
        this.bIsFixed = b is not null && bIsFixed;
        this.cIsFixed = c is not null && cIsFixed;
        this.dIsFixed = d is not null && dIsFixed;
        this.vegaWeighted = vegaWeighted;
        this.endCriteria = endCriteria;
        this.optMethod = optMethod;

        // QuantLib's update()
        abcdCalibrator = Fit(a ?? -0.06, b ?? 0.17, c ?? 0.54, d ?? 0.17);
        K = abcdCalibrator.K(X.ToArray(), Y.ToArray());
        RmsError = abcdCalibrator.Error();
        MaxError = abcdCalibrator.MaxError();
    }

    // QuantLib's update() on a fitted interpolation: the fit run again from the fitted coefficients, with the same
    // fixed flags, weighting and optimizer, and no guess check, as QuantLib's update() makes none
    private AbcdInterpolation(AbcdInterpolation fitted)
        : base(fitted.X, fitted.Y, 2)
    {
        aIsFixed = fitted.aIsFixed;
        bIsFixed = fitted.bIsFixed;
        cIsFixed = fitted.cIsFixed;
        dIsFixed = fitted.dIsFixed;
        vegaWeighted = fitted.vegaWeighted;
        endCriteria = fitted.endCriteria;
        optMethod = fitted.optMethod;
        abcdCalibrator = Fit(fitted.A, fitted.B, fitted.C, fitted.D);
        K = abcdCalibrator.K(X.ToArray(), Y.ToArray());
        RmsError = abcdCalibrator.Error();
        MaxError = abcdCalibrator.MaxError();
    }

    /// <summary>Gets the fitted a.</summary>
    public double A => abcdCalibrator.A;

    /// <summary>Gets the fitted b.</summary>
    public double B => abcdCalibrator.B;

    /// <summary>Gets the fitted c.</summary>
    public double C => abcdCalibrator.C;

    /// <summary>Gets the fitted d.</summary>
    public double D => abcdCalibrator.D;

    /// <summary>Gets the adjustment factors that match the volatilities.</summary>
    public ImmutableArray<double> K { get; }

    /// <summary>Gets the fit's weighted root-mean-square error.</summary>
    public double RmsError { get; }

    /// <summary>Gets the fit's largest absolute error.</summary>
    public double MaxError { get; }

    /// <summary>Gets the end criterion the fit met.</summary>
    public EndCriteriaType EndCriteria => abcdCalibrator.EndCriteria;

    /// <summary>
    /// QuantLib's <c>update()</c> called again: a new interpolation fitted from this one's coefficients, on the same
    /// points, fixed flags, weighting and optimizer, without the constructor's guess check.
    /// </summary>
    /// <returns>The refitted interpolation.</returns>
    /// <exception cref="NanoQLException">The fit fails (<see cref="AbcdCalibration.Compute"/>).</exception>
    internal AbcdInterpolation Refit() => new(this);

    /// <summary>The adjustment factor at a time, interpolated linearly between the factors at the given
    /// times.</summary>
    /// <param name="t">The time.</param>
    /// <param name="x">The times the factors are at, sorted.</param>
    /// <returns>The factor.</returns>
    /// <exception cref="ArgumentException">There are fewer times than factors, or fewer than two.</exception>
    /// <exception cref="NanoQLException">The time is out of the times' range.</exception>
    public double KAt(double t, ReadOnlySpan<double> x)
    {
        if (!(x.Length <= K.Length))
        {
            throw new ArgumentException($"{x.Length} times given for {K.Length} factors", nameof(x));
        }

        return new LinearInterpolation(x, K.AsSpan(0, x.Length)).Evaluate(t);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The time is negative.</exception>
    protected override double ValueCore(double x)
    {
        if (!(x >= 0.0))
        {
            throw new ArgumentException($"time must be non negative: {Utilities.DataFormattersFunctions.General(x)} not allowed", nameof(x));
        }

        return abcdCalibrator.Value(x);
    }

    // the calibration from guesses, with this interpolation's flags, weighting and optimizer
    private AbcdCalibration Fit(double a, double b, double c, double d) =>
        new AbcdCalibration(
            X.ToArray(),
            Y.ToArray(),
            a,
            b,
            c,
            d,
            aIsFixed,
            bIsFixed,
            cIsFixed,
            dIsFixed,
            vegaWeighted,
            endCriteria,
            optMethod).Compute();

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: QuantLib's abcd interpolation has no primitive.</exception>
    protected override double PrimitiveCore(double x) => throw new NanoQLException("Abcd primitive not implemented");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: QuantLib's abcd interpolation has no derivative.</exception>
    protected override double DerivativeCore(double x) => throw new NanoQLException("Abcd derivative not implemented");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: QuantLib's abcd interpolation has no second derivative.</exception>
    protected override double SecondDerivativeCore(double x) => throw new NanoQLException("Abcd secondDerivative not implemented");
}
