// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/costfunction.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The cost function of an optimization problem: a vector of values, and the cost they give.</summary>
/// <remarks>
/// <para>
/// A cost function implements <see cref="Values"/>, and overrides the other members where it knows better than their
/// defaults: <see cref="Value"/>, the root mean square of the values; <see cref="Gradient"/> and
/// <see cref="Jacobian"/>, by central differences with step <see cref="FiniteDifferenceEpsilon"/>, written into their
/// first argument; and <see cref="ValueAndGradient"/> and <see cref="ValuesAndJacobian"/>, which call those.
/// </para>
/// <para>
/// A vector a member returns is the caller's own: a new one, which the caller may change. An extension point, as in
/// QuantLib: each problem's cost function derives from it.
/// </para>
/// </remarks>
public abstract class CostFunction
{
    /// <summary>Initializes a new instance of the <see cref="CostFunction"/> class.</summary>
    protected CostFunction()
    {
    }

    /// <summary>Gets the step of the finite differences in <see cref="Gradient"/> and <see cref="Jacobian"/>: by default 1e-8.</summary>
    public virtual double FiniteDifferenceEpsilon => 1e-8;

    /// <summary>The cost at a point: by default the root mean square of <see cref="Values"/>.</summary>
    /// <param name="x">The point.</param>
    /// <returns>The cost.</returns>
    public virtual double Value(Vector x)
    {
        // QuantLib squares the values in place, then sums them in order; the same sum, leaving the values unchanged.
        Vector v = Values(x);
        double sum = 0.0;
        foreach (double e in v)
        {
            double square = e * e;
            sum = sum + square;
        }

        return Math.Sqrt(sum / v.Size);
    }

    /// <summary>The cost function's values at a point.</summary>
    /// <param name="x">The point.</param>
    /// <returns>A new vector of the values.</returns>
    public abstract Vector Values(Vector x);

    /// <summary>
    /// The gradient of <see cref="Value"/>, the first derivative of the cost with respect to x, written into
    /// <paramref name="grad"/>: by default by central differences.
    /// </summary>
    /// <param name="grad">The gradient, at least of x's size, whose first elements are written.</param>
    /// <param name="x">The point.</param>
    public virtual void Gradient(Vector grad, Vector x)
    {
        ArgumentNullException.ThrowIfNull(grad);
        ArgumentNullException.ThrowIfNull(x);
        double eps = FiniteDifferenceEpsilon, fp, fm;
        Vector xx = x.Clone();
        for (int i = 0; i < x.Size; i++)
        {
            xx[i] += eps;
            fp = Value(xx);
            xx[i] -= 2.0 * eps;
            fm = Value(xx);
            grad[i] = 0.5 * (fp - fm) / eps;
            xx[i] = x[i];
        }
    }

    /// <summary>
    /// The gradient written into <paramref name="grad"/>, and the cost: by default <see cref="Gradient"/>, then
    /// <see cref="Value"/>.
    /// </summary>
    /// <param name="grad">The gradient, at least of x's size, whose first elements are written.</param>
    /// <param name="x">The point.</param>
    /// <returns>The cost.</returns>
    public virtual double ValueAndGradient(Vector grad, Vector x)
    {
        Gradient(grad, x);
        return Value(x);
    }

    /// <summary>
    /// The Jacobian of <see cref="Values"/> with respect to x, written into <paramref name="jac"/>, one row per value:
    /// by default by central differences.
    /// </summary>
    /// <param name="jac">The Jacobian, with a row per value and a column per element of x.</param>
    /// <param name="x">The point.</param>
    public virtual void Jacobian(Matrix jac, Vector x)
    {
        ArgumentNullException.ThrowIfNull(jac);
        ArgumentNullException.ThrowIfNull(x);
        double eps = FiniteDifferenceEpsilon;
        Vector xx = x.Clone(), fp, fm;
        for (int i = 0; i < x.Size; ++i)
        {
            xx[i] += eps;

            // QuantLib's values returns an Array by value, so fp is always its own. A C# Values may return its argument
            // (QuantLib's "return x;") or a buffer it reuses, which the next two lines would change: copy it (S2R1).
            fp = Values(xx).Clone();
            xx[i] -= 2.0 * eps;
            fm = Values(xx);
            for (int j = 0; j < fp.Size; ++j)
            {
                jac[j, i] = 0.5 * (fp[j] - fm[j]) / eps;
            }

            xx[i] = x[i];
        }
    }

    /// <summary>
    /// The Jacobian written into <paramref name="jac"/>, and the values: by default <see cref="Jacobian"/>, then
    /// <see cref="Values"/>.
    /// </summary>
    /// <param name="jac">The Jacobian, with a row per value and a column per element of x.</param>
    /// <param name="x">The point.</param>
    /// <returns>A new vector of the values.</returns>
    public virtual Vector ValuesAndJacobian(Matrix jac, Vector x)
    {
        Jacobian(jac, x);
        return Values(x);
    }
}
