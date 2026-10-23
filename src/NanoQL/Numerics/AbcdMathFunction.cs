// SPDX-FileCopyrightText: Copyright (C) 2006, 2007, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/abcdmathfunction.hpp ql/math/abcdmathfunction.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Utilities;

namespace NanoQL.Numerics;

/// <summary>
/// The abcd functional form, f(t) = (a + b·t)·e<sup>−c·t</sup> + d, in Rebonato's notation; 0 at a negative time.
/// </summary>
/// <remarks>
/// <para>
/// Immutable; validated at construction (<see cref="Validate"/>). An extension point, as in QuantLib:
/// <see cref="TermStructures.AbcdFunction"/> derives from it.
/// </para>
/// <para>
/// Two of QuantLib's defects are fixed. <see cref="MaximumLocation"/> is QuantLib's master's (#2841, after the pin),
/// where 1.44 returns the stationary point unconditionally, a minimum when b &lt; 0 (the S-g1d plan's G1R5 (iii)); and
/// <see cref="MaximumValue"/> is the function at that location, where QuantLib returns d whenever b = 0 or a ≤ 0, so
/// 0.17 for <c>AbcdFunction()</c>'s defaults whose maximum is 0.2657 (G1R6 (ix)).
/// </para>
/// </remarks>
public class AbcdMathFunction
{
    private readonly double da;
    private readonly double db;
    private readonly double pa;
    private readonly double pb;
    private readonly double k;
    private readonly double dibc;
    private readonly double diacplusbcc;

    /// <summary>Initializes a new instance of the <see cref="AbcdMathFunction"/> class.</summary>
    /// <param name="a">a.</param>
    /// <param name="b">b.</param>
    /// <param name="c">c, positive.</param>
    /// <param name="d">d, non-negative.</param>
    /// <exception cref="ArgumentException">The coefficients fail <see cref="Validate"/>.</exception>
    public AbcdMathFunction(double a = 0.002, double b = 0.001, double c = 0.16, double d = 0.0005)
        : this([a, b, c, d])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AbcdMathFunction"/> class from its coefficients.</summary>
    /// <param name="abcd">a, b, c and d, the first four values; <see cref="Coefficients"/> keeps them all, as
    /// QuantLib's.</param>
    /// <exception cref="ArgumentNullException"><paramref name="abcd"/> is null.</exception>
    /// <exception cref="ArgumentException">There are fewer than four values, which QuantLib reads unchecked, or the
    /// coefficients fail <see cref="Validate"/>.</exception>
    public AbcdMathFunction(IEnumerable<double> abcd)
    {
        ArgumentNullException.ThrowIfNull(abcd);
        ImmutableArray<double> values = [.. abcd];
        if (!(values.Length >= 4))
        {
            throw new ArgumentException($"four coefficients needed, {values.Length} given", nameof(abcd));
        }

        Coefficients = values;
        A = values[0];
        B = values[1];
        C = values[2];
        D = values[3];

        // initialize_()
        Validate(A, B, C, D);
        da = B - (C * A);
        db = -C * B;
        DerivativeCoefficients = [da, db, C, 0.0];

        pa = -(A + (B / C)) / C;
        pb = -B / C;
        k = 0.0;

        dibc = B / C;
        diacplusbcc = (A / C) + (dibc / C);
    }

    /// <summary>Gets a.</summary>
    public double A { get; }

    /// <summary>Gets b.</summary>
    public double B { get; }

    /// <summary>Gets c.</summary>
    public double C { get; }

    /// <summary>Gets d.</summary>
    public double D { get; }

    /// <summary>Gets the coefficients as given.</summary>
    public ImmutableArray<double> Coefficients { get; }

    /// <summary>Gets the derivative's coefficients, an abcd form's: (b − c·a, −c·b, c, 0).</summary>
    public ImmutableArray<double> DerivativeCoefficients { get; }

    /// <summary>Validates abcd coefficients: c positive, d and a + d non-negative, and for b &lt; 0 a non-negative
    /// value at the stationary point.</summary>
    /// <param name="a">a.</param>
    /// <param name="b">b.</param>
    /// <param name="c">c.</param>
    /// <param name="d">d.</param>
    /// <exception cref="ArgumentException">A check fails, with QuantLib's text, naming the coefficient.</exception>
    public static void Validate(double a, double b, double c, double d)
    {
        if (Check(a, b, c, d) is (string message, string paramName))
        {
            throw new ArgumentException(message, paramName);
        }
    }

    // Validate's checks: QuantLib's text and the coefficient it names, or null for valid coefficients.
    internal static (string Message, string ParamName)? Check(double a, double b, double c, double d)
    {
        if (!(c > 0))
        {
            return ($"c ({DataFormattersFunctions.General(c)}) must be positive", nameof(c));
        }

        if (!(d >= 0))
        {
            return ($"d ({DataFormattersFunctions.General(d)}) must be non negative", nameof(d));
        }

        if (!(a + d >= 0))
        {
            return ($"a+d ({DataFormattersFunctions.General(a)}+{DataFormattersFunctions.General(d)}) must be non negative", nameof(a));
        }

        if (b >= 0.0)
        {
            return null;
        }

        // the one and only stationary point...
        double zeroFirstDerivative = (1.0 / c) - (a / b);
        if (zeroFirstDerivative >= 0.0)
        {
            // ... is a minimum
            // must be abcd(zeroFirstDerivative)>=0
            if (!(b >= -(d * c) / Math.Exp((c * a / b) - 1.0)))
            {
                return (
                    $"b ({DataFormattersFunctions.General(b)}) less than {DataFormattersFunctions.General(-(d * c) / Math.Exp((c * a / b) - 1.0))}: "
                        + $"negative function value at stationary point {DataFormattersFunctions.General(zeroFirstDerivative)}",
                    nameof(b));
            }
        }

        return null;
    }

    /// <summary>The function's value at a time, QuantLib's <c>operator()</c>: 0 at a negative time.</summary>
    /// <param name="t">The time.</param>
    /// <returns>f(t).</returns>
    public double Evaluate(double t) => t < 0 ? 0.0 : ((A + (B * t)) * Math.Exp(-C * t)) + D;

    /// <summary>The time at which the function reaches its maximum, <see cref="double.MaxValue"/> where it rises toward
    /// d: QuantLib's master's (#2841).</summary>
    /// <returns>The location.</returns>
    public double MaximumLocation()
    {
        if (B == 0.0)
        {
            // f(t) = a*exp(-c*t) + d: monotonically decreasing (a>0) or increasing (a<0)
            if (A >= 0.0)
            {
                return 0.0;
            }
            else
            {
                return double.MaxValue;
            }
        }

        // Stationary point: f'(t*) = 0  =>  t* = 1/c - a/b
        double tStar = (1.0 / C) - (A / B);

        if (B > 0.0)
        {
            // f''(t*) = -c*b*exp(-c*t*) < 0  =>  t* is a maximum.
            // If t* <= 0 the function is decreasing on [0, inf), maximum is at t=0.
            return tStar > 0.0 ? tStar : 0.0;
        }
        else
        {
            // b < 0: f''(t*) > 0  =>  t* is a minimum, not a maximum.
            if (tStar <= 0.0)
            {
                // No interior minimum on [0, inf): function is monotonically
                // increasing toward d; maximum is at t -> +inf.
                return double.MaxValue;
            }
            else
            {
                // Interior minimum at t* > 0: function decreases then increases.
                // Maximum is at one of the two boundaries: t=0 (value a+d) or
                // t -> +inf (value d).  Validate() guarantees a+d >= 0 and d >= 0.
                return A >= 0.0 ? 0.0 : double.MaxValue;
            }
        }
    }

    /// <summary>The function's maximum: its value at <see cref="MaximumLocation"/>, d at +∞ (G1R6 (ix)).</summary>
    /// <returns>The maximum.</returns>
    public double MaximumValue()
    {
        // QuantLib returns d whenever b == 0 or a <= 0; the limit at +inf is d, where (a + b t) e^(-ct) can't be
        // evaluated
        double location = MaximumLocation();
        return location == double.MaxValue ? D : Evaluate(location);
    }

    /// <summary>The function's value at +∞, d.</summary>
    /// <returns>d.</returns>
    public double LongTermValue() => D;

    /// <summary>The first derivative at a time, f'(t) = ((b − c·a) − c·b·t)·e<sup>−c·t</sup>: 0 at a negative
    /// time.</summary>
    /// <param name="t">The time.</param>
    /// <returns>f'(t).</returns>
    public double Derivative(double t) => t < 0 ? 0.0 : (da + (db * t)) * Math.Exp(-C * t);

    /// <summary>The indefinite integral at a time, ((−a/c − b/c²) − (b/c)·t)·e<sup>−c·t</sup> + d·t: 0 at a negative
    /// time.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The primitive.</returns>
    public double Primitive(double t) => t < 0 ? 0.0 : ((pa + (pb * t)) * Math.Exp(-C * t)) + (D * t) + k;

    /// <summary>The definite integral between two times.</summary>
    /// <param name="t1">The lower bound.</param>
    /// <param name="t2">The upper bound.</param>
    /// <returns>The integral.</returns>
    public double DefiniteIntegral(double t1, double t2) => Primitive(t2) - Primitive(t1);

    /// <summary>The coefficients of the abcd function defined as the definite integral on a rolling window of length
    /// τ = <paramref name="t2"/> − <paramref name="t"/>.</summary>
    /// <param name="t">The window's start.</param>
    /// <param name="t2">The window's end.</param>
    /// <returns>The four coefficients.</returns>
    public ImmutableArray<double> DefiniteIntegralCoefficients(double t, double t2)
    {
        double dt = t2 - t;
        double expcdt = Math.Exp(-C * dt);
        var result = new double[4];
        result[0] = diacplusbcc - ((diacplusbcc + (dibc * dt)) * expcdt);
        result[1] = dibc * (1.0 - expcdt);
        result[2] = C;
        result[3] = D * dt;
        return ImmutableArray.Create(result);
    }

    /// <summary>The coefficients of the abcd function defined as the definite derivative on a rolling window of length
    /// τ = <paramref name="t2"/> − <paramref name="t"/>.</summary>
    /// <param name="t">The window's start.</param>
    /// <param name="t2">The window's end.</param>
    /// <returns>The four coefficients.</returns>
    public ImmutableArray<double> DefiniteDerivativeCoefficients(double t, double t2)
    {
        double dt = t2 - t;
        double expcdt = Math.Exp(-C * dt);
        var result = new double[4];
        result[1] = B * C / (1.0 - expcdt);
        result[0] = (A * C) - B + (result[1] * dt * expcdt);
        result[0] /= 1.0 - expcdt;
        result[2] = C;
        result[3] = D / dt;
        return ImmutableArray.Create(result);
    }
}
