// SPDX-FileCopyrightText: Copyright Nick Thompson, 2017
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/quadrature/exp_sinh.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's <c>exp_sinh</c> (<c>quadrature/exp_sinh.hpp</c>) for <c>double</c>: exp-sinh quadrature on half-infinite
/// intervals (Tanaka, Sugihara, Murota and Mori 2009), with the shifts that map [a, ∞) and (−∞, b] onto (0, ∞).
/// </summary>
/// <remarks>
/// Boost's domain errors (a NaN limit, two infinite or two finite limits, a tolerance above 1) throw
/// <see cref="ArgumentOutOfRangeException"/> with Boost's message; its evaluation error throws
/// <see cref="NanoQLException"/> (<see cref="ExpSinhDetail"/>).
/// </remarks>
internal sealed class ExpSinh
{
    private const string Function = "exp_sinh<double>::integrate";

    // tools::max_value<double>()
    private const double MaxValue = double.MaxValue;

    private readonly ExpSinhDetail imp;

    /// <summary>Initializes a new instance of the <see cref="ExpSinh"/> class.</summary>
    /// <param name="maxRefinements">The most refinements; Boost's default is 9.</param>
    internal ExpSinh(int maxRefinements = 9) => imp = new ExpSinhDetail(maxRefinements);

    /// <summary>
    /// The integral of <paramref name="f"/> over [<paramref name="a"/>, <paramref name="b"/>], one of the bounds infinite.
    /// </summary>
    /// <param name="f">The integrand.</param>
    /// <param name="a">The lower bound, finite, or −<see cref="double.MaxValue"/> and below for −∞.</param>
    /// <param name="b">The upper bound, finite, or <see cref="double.MaxValue"/> and above for ∞.</param>
    /// <param name="tolerance">The tolerance, relative to the integral of |f|.</param>
    /// <param name="error">The error estimate.</param>
    /// <param name="l1">The integral of |f|.</param>
    /// <param name="levels">The number of levels the quadrature reached.</param>
    /// <returns>The integral.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A bound is NaN, or both are infinite, or both finite.
    /// </exception>
    /// <exception cref="NanoQLException">The integral of |f| isn't finite.</exception>
    internal double Integrate(
        Func<double, double> f, double a, double b, double tolerance, out double error, out double l1, out int levels)
    {
        // Neither limit may be a NaN:
        if (double.IsNaN(a) || double.IsNaN(b))
        {
            throw DomainError("NaN supplied as one limit of integration - sorry I don't know what to do");
        }

        // Right limit is infinite:
        if (double.IsFinite(a) && (b >= MaxValue))
        {
            // If a = 0, don't use an additional level of indirection:
            if (a == 0)
            {
                return imp.Integrate(f, out error, out l1, tolerance, out levels);
            }

            return imp.Integrate(t => f(t + a), out error, out l1, tolerance, out levels);
        }

        if (double.IsFinite(b) && (a <= -MaxValue))
        {
            return imp.Integrate(t => f(b - t), out error, out l1, tolerance, out levels);
        }

        // Infinite limits:
        if ((a <= -MaxValue) && (b >= MaxValue))
        {
            throw DomainError(
                "Use sinh_sinh quadrature for integration over the whole real line; exp_sinh is for half infinite " +
                "integrals.");
        }

        // If we get to here then both ends must necessarily be finite:
        throw DomainError(
            "Use tanh_sinh quadrature for integration over finite domains; exp_sinh is for half infinite integrals.");
    }

    /// <summary>The integral of <paramref name="f"/> over (0, ∞).</summary>
    /// <param name="f">The integrand.</param>
    /// <param name="tolerance">The tolerance, relative to the integral of |f|, at most 1 in magnitude.</param>
    /// <param name="error">The error estimate.</param>
    /// <param name="l1">The integral of |f|.</param>
    /// <param name="levels">The number of levels the quadrature reached.</param>
    /// <returns>The integral.</returns>
    /// <exception cref="ArgumentOutOfRangeException">|<paramref name="tolerance"/>| is above 1.</exception>
    /// <exception cref="NanoQLException">The integral of |f| isn't finite.</exception>
    internal double Integrate(Func<double, double> f, double tolerance, out double error, out double l1, out int levels)
    {
        if (Math.Abs(tolerance) > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tolerance),
                $"Error in function {Function}: The tolerance provided ({Show(tolerance)}) is unusually large; did " +
                "you confuse it with a domain bound?");
        }

        return imp.Integrate(f, out error, out l1, tolerance, out levels);
    }

    private static ArgumentOutOfRangeException DomainError(string message) =>
        new("a", $"Error in function {Function}: {message}");

    private static string Show(double value) => value.ToString(CultureInfo.InvariantCulture);
}
