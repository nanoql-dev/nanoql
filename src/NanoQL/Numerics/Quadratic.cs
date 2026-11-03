// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/quadratic.hpp ql/math/quadratic.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The quadratic a x² + b x + c: its value, turning point, discriminant and real roots.</summary>
/// <remarks>Immutable. QuantLib's <c>quadratic</c>.</remarks>
public sealed class Quadratic
{
    private readonly double a;
    private readonly double b;
    private readonly double c;

    /// <summary>Initializes a new instance of the <see cref="Quadratic"/> class.</summary>
    /// <param name="a">The coefficient of x².</param>
    /// <param name="b">The coefficient of x.</param>
    /// <param name="c">The constant.</param>
    public Quadratic(double a, double b, double c)
    {
        this.a = a;
        this.b = b;
        this.c = c;
    }

    /// <summary>The turning point, −b/(2a).</summary>
    /// <returns>The turning point.</returns>
    public double TurningPoint() => -b / (2.0 * a);

    /// <summary>The value at the turning point.</summary>
    /// <returns>The value.</returns>
    public double ValueAtTurningPoint() => Evaluate(TurningPoint());

    /// <summary>The value at <paramref name="x"/>.</summary>
    /// <param name="x">The point.</param>
    /// <returns>x(xa + b) + c.</returns>
    public double Evaluate(double x) => (x * ((x * a) + b)) + c;

    /// <summary>The discriminant, b² − 4ac.</summary>
    /// <returns>The discriminant.</returns>
    public double Discriminant() => (b * b) - (4 * a * c);

    /// <summary>The roots, or where they aren't real the turning point.</summary>
    /// <param name="x">(−b − √d)/(2a), the lower root where a &gt; 0, or the turning point.</param>
    /// <param name="y">(−b + √d)/(2a), the upper root where a &gt; 0, or the turning point.</param>
    /// <returns>Whether the roots are real.</returns>
    public bool Roots(out double x, out double y)
    {
        double d = Discriminant();
        if (d < 0)
        {
            x = y = TurningPoint();
            return false;
        }

        d = Math.Sqrt(d);
        x = (-b - d) / (2 * a);
        y = (-b + d) / (2 * a);
        return true;
    }
}
