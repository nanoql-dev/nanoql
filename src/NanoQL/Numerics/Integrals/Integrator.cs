// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/integral.hpp ql/math/integrals/integral.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// The base of the one-dimensional integrators: QuantLib's absolute accuracy and maximum number of evaluations, and the
/// integral of a function over an interval.
/// </summary>
/// <remarks>
/// Immutable: <see cref="SetAbsoluteAccuracy"/> and <see cref="SetMaxEvaluations"/> return copies. QuantLib keeps the
/// number of evaluations and the error estimate of the last call in mutable members, the count reset by
/// <c>operator()</c> (<c>integral.cpp:76-86</c>); here <see cref="IntegrateWithStatistics"/> returns both with the
/// value, the error <see langword="null"/> where QuantLib's integrator computes none, and
/// <see cref="IntegrationSuccess"/> reads them from the result.
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
    /// <returns>
    /// The integral, the number of evaluations and the rule's error estimate, if it computes one; 0, 0 and an error of
    /// 0 where the limits are equal.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The rule doesn't reach its accuracy within its maximum.</exception>
    public IntegrationResult IntegrateWithStatistics(Func<double, double> f, double a, double b)
    {
        ArgumentNullException.ThrowIfNull(f);
        if (a == b)
        {
            return new IntegrationResult(0.0, 0) { AbsoluteError = 0.0 };
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

    /// <summary>
    /// Whether an integration of this integrator succeeded: QuantLib's <c>integrationSuccess()</c>, its number of
    /// evaluations at most <see cref="MaxEvaluations"/> and its error estimate at most <see cref="AbsoluteAccuracy"/>
    /// (<c>integral.cpp:71-74</c>).
    /// </summary>
    /// <param name="result">The integration's result, from this integrator.</param>
    /// <returns>Whether it succeeded.</returns>
    /// <exception cref="NanoQLException">
    /// The result carries no error estimate: its integrator computes none, where QuantLib's reads an unset member.
    /// </exception>
    public bool IntegrationSuccess(IntegrationResult result)
    {
        double error = result.AbsoluteError ?? throw new NanoQLException("the integrator computes no error estimate");
        return result.NumberOfEvaluations <= maxEvaluations && error <= absoluteAccuracy;
    }

    /// <summary>
    /// The rule's integral over [a, b], a below b, with the number of evaluations it made and its error estimate, if
    /// it computes one.
    /// </summary>
    /// <param name="f">The integrand.</param>
    /// <param name="a">The lower limit.</param>
    /// <param name="b">The upper limit.</param>
    /// <returns>The integral, the number of evaluations and the error estimate, or <see langword="null"/> for
    /// none.</returns>
    protected abstract IntegrationResult IntegrateCore(Func<double, double> f, double a, double b);

    private static string Show(double value) => value.ToString(CultureInfo.InvariantCulture);
}
