// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/integral.hpp ql/math/integrals/integral.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: absoluteError and integrationSuccess, which wait for S-cms: no integrator S-eq ports computes an error, so QuantLib's absoluteError reads an uninitialized member after each of them; S-cms's CMS pricer is their first caller, on its Kronrod rules (integrationSuccess also compares the evaluation count with maxEvaluations, which the trapezoid family uses as an iteration bound)

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// The base of the one-dimensional integrators: QuantLib's absolute accuracy and maximum number of evaluations, and the
/// integral of a function over an interval.
/// </summary>
/// <remarks>
/// Immutable: <see cref="SetAbsoluteAccuracy"/> and <see cref="SetMaxEvaluations"/> return copies. QuantLib keeps the
/// number of evaluations of the last call in a mutable member, which <c>operator()</c> resets
/// (<c>integral.cpp:76-86</c>); here <see cref="IntegrateWithStatistics"/> returns it with the value.
/// </remarks>
public abstract class Integrator
{
    private double absoluteAccuracy;
    private int maxEvaluations;

    /// <summary>Initializes a new instance of the <see cref="Integrator"/> class.</summary>
    /// <param name="absoluteAccuracy">The absolute accuracy, above machine epsilon.</param>
    /// <param name="maxEvaluations">The maximum number of evaluations, or of iterations for the trapezoid
    /// family.</param>
    /// <exception cref="ArgumentException"><paramref name="absoluteAccuracy"/> isn't above machine epsilon.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    protected Integrator(double absoluteAccuracy, int maxEvaluations)
    {
        if (!(absoluteAccuracy > NumericLimits.QL_EPSILON))
        {
            throw new ArgumentException(
                $"required tolerance ({Show(absoluteAccuracy)}) not allowed. It must be > {Show(NumericLimits.QL_EPSILON)}",
                nameof(absoluteAccuracy));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(maxEvaluations);
        this.absoluteAccuracy = absoluteAccuracy;
        this.maxEvaluations = maxEvaluations;
    }

    /// <summary>Gets the absolute accuracy.</summary>
    public double AbsoluteAccuracy => absoluteAccuracy;

    /// <summary>Gets the maximum number of evaluations, or of iterations for the trapezoid family.</summary>
    public int MaxEvaluations => maxEvaluations;

    /// <summary>A copy of the integrator with the absolute accuracy set; QuantLib's setter checks nothing.</summary>
    /// <param name="accuracy">The absolute accuracy.</param>
    /// <returns>The modified copy.</returns>
    public Integrator SetAbsoluteAccuracy(double accuracy)
    {
        var copy = (Integrator)MemberwiseClone();
        copy.absoluteAccuracy = accuracy;
        return copy;
    }

    /// <summary>A copy of the integrator with the maximum number of evaluations set.</summary>
    /// <param name="maxEvaluations">The maximum number of evaluations.</param>
    /// <returns>The modified copy.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    public Integrator SetMaxEvaluations(int maxEvaluations)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxEvaluations);
        var copy = (Integrator)MemberwiseClone();
        copy.maxEvaluations = maxEvaluations;
        return copy;
    }

    /// <summary>The integral of <paramref name="f"/> from <paramref name="a"/> to <paramref name="b"/>.</summary>
    /// <param name="f">The integrand.</param>
    /// <param name="a">The lower limit; above <paramref name="b"/>, the integral is the negative of the reversed
    /// one.</param>
    /// <param name="b">The upper limit.</param>
    /// <returns>The integral; 0 where the limits are equal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The rule doesn't reach its accuracy within its maximum.</exception>
    public double Integrate(Func<double, double> f, double a, double b) => IntegrateWithStatistics(f, a, b).Value;

    /// <summary>
    /// The integral of <paramref name="f"/> from <paramref name="a"/> to <paramref name="b"/>, with the number of
    /// evaluations the call made, QuantLib's <c>numberOfEvaluations()</c> after it.
    /// </summary>
    /// <param name="f">The integrand.</param>
    /// <param name="a">The lower limit; above <paramref name="b"/>, the integral is the negative of the reversed
    /// one.</param>
    /// <param name="b">The upper limit.</param>
    /// <returns>The integral and the number of evaluations; 0 and 0 where the limits are equal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The rule doesn't reach its accuracy within its maximum.</exception>
    public IntegrationResult IntegrateWithStatistics(Func<double, double> f, double a, double b)
    {
        ArgumentNullException.ThrowIfNull(f);
        if (a == b)
        {
            return new IntegrationResult(0.0, 0);
        }

        if (b > a)
        {
            return IntegrateCore(f, a, b);
        }
        else
        {
            IntegrationResult reversed = IntegrateCore(f, b, a);
            return reversed with { Value = -reversed.Value };
        }
    }

    /// <summary>The rule's integral over [a, b], a below b, with the number of evaluations it made.</summary>
    /// <param name="f">The integrand.</param>
    /// <param name="a">The lower limit.</param>
    /// <param name="b">The upper limit.</param>
    /// <returns>The integral and the number of evaluations.</returns>
    protected abstract IntegrationResult IntegrateCore(Func<double, double> f, double a, double b);

    private static string Show(double value) => value.ToString(CultureInfo.InvariantCulture);
}
