// SPDX-FileCopyrightText: Copyright (C) 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/polynomialmathfunction.hpp ql/math/polynomialmathfunction.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>A polynomial f(t) = Σᵢ cᵢ·tⁱ, i from 0 to n, with its derivative and primitive.</summary>
/// <remarks>
/// Immutable: QuantLib keeps a mutable matrix for the definite coefficients' equations, rebuilt at each call; here
/// each call builds its own. Those equations read <see cref="PascalTriangle"/> to the polynomial's order, so the
/// definite coefficients refuse orders above <see cref="PascalTriangle.MaxOrder"/>.
/// </remarks>
public sealed class PolynomialFunction
{
    private readonly int order;
    private readonly ImmutableArray<double> c;
    private readonly ImmutableArray<double> derC;
    private readonly ImmutableArray<double> prC;
    private readonly double k;

    /// <summary>Initializes a new instance of the <see cref="PolynomialFunction"/> class.</summary>
    /// <param name="coeff">The coefficients c₀ to cₙ, at least one.</param>
    /// <exception cref="ArgumentException"><paramref name="coeff"/> is empty.</exception>
    public PolynomialFunction(ReadOnlySpan<double> coeff)
    {
        if (coeff.IsEmpty)
        {
            throw new ArgumentException("empty coefficient vector", nameof(coeff));
        }

        order = coeff.Length;
        c = [.. coeff];
        var der = new double[order - 1];
        var pr = new double[order];
        k = 0.0;

        int i;
        for (i = 0; i < order - 1; ++i)
        {
            pr[i] = c[i] / (i + 1);
            der[i] = c[i + 1] * (i + 1);
        }

        pr[i] = c[i] / (i + 1);
        derC = [.. der];
        prC = [.. pr];
    }

    /// <summary>Gets the order, the number of coefficients.</summary>
    public int Order => order;

    /// <summary>The coefficients c₀ to cₙ.</summary>
    /// <returns>The coefficients.</returns>
    public ImmutableArray<double> Coefficients() => c;

    /// <summary>The derivative's coefficients, (i + 1)·cᵢ₊₁.</summary>
    /// <returns>The coefficients.</returns>
    public ImmutableArray<double> DerivativeCoefficients() => derC;

    /// <summary>The primitive's coefficients, cᵢ/(i + 1), of tⁱ⁺¹.</summary>
    /// <returns>The coefficients.</returns>
    public ImmutableArray<double> PrimitiveCoefficients() => prC;

    /// <summary>The function's value at <paramref name="t"/>.</summary>
    /// <param name="t">The time.</param>
    /// <returns>Σᵢ cᵢ·tⁱ.</returns>
    public double Evaluate(double t)
    {
        double result = 0.0, tPower = 1.0;
        for (int i = 0; i < order; ++i)
        {
            result += c[i] * tPower;
            tPower *= t;
        }

        return result;
    }

    /// <summary>The first derivative at <paramref name="t"/>.</summary>
    /// <param name="t">The time.</param>
    /// <returns>Σᵢ (i + 1)·cᵢ₊₁·tⁱ.</returns>
    public double Derivative(double t)
    {
        double result = 0.0, tPower = 1.0;
        for (int i = 0; i < order - 1; ++i)
        {
            result += derC[i] * tPower;
            tPower *= t;
        }

        return result;
    }

    /// <summary>The indefinite integral at <paramref name="t"/>, its constant 0.</summary>
    /// <param name="t">The time.</param>
    /// <returns>Σᵢ cᵢ·tⁱ⁺¹/(i + 1).</returns>
    public double Primitive(double t)
    {
        double result = k, tPower = t;
        for (int i = 0; i < order; ++i)
        {
            result += prC[i] * tPower;
            tPower *= t;
        }

        return result;
    }

    /// <summary>The definite integral between <paramref name="t1"/> and <paramref name="t2"/>.</summary>
    /// <param name="t1">The lower time.</param>
    /// <param name="t2">The upper time.</param>
    /// <returns>The integral.</returns>
    public double DefiniteIntegral(double t1, double t2) => Primitive(t2) - Primitive(t1);

    /// <summary>
    /// The coefficients of the polynomial defined as the definite integral on a rolling window of length τ = t2 − t.
    /// </summary>
    /// <param name="t">The window's start.</param>
    /// <param name="t2">The window's end.</param>
    /// <returns>The coefficients.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The order is above <see cref="PascalTriangle.MaxOrder"/>.</exception>
    public ImmutableArray<double> DefiniteIntegralCoefficients(double t, double t2)
    {
        var kv = new Vector(c.AsSpan());
        Matrix eqs = InitializeEqs(t, t2);
        Vector coeff = eqs * kv;
        return [.. coeff.AsSpan()];
    }

    /// <summary>
    /// The coefficients of the polynomial defined as the definite derivative on a rolling window of length τ = t2 − t.
    /// </summary>
    /// <param name="t">The window's start.</param>
    /// <param name="t2">The window's end.</param>
    /// <returns>The coefficients.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The order is above <see cref="PascalTriangle.MaxOrder"/>.</exception>
    /// <exception cref="NanoQLException">The equations' matrix is singular.</exception>
    public ImmutableArray<double> DefiniteDerivativeCoefficients(double t, double t2)
    {
        var kv = new Vector(c.AsSpan());
        Matrix eqs = InitializeEqs(t, t2);
        Vector coeff = MatrixFunctions.Inverse(eqs) * kv;
        return [.. coeff.AsSpan()];
    }

    private Matrix InitializeEqs(double t, double t2)
    {
        var eqs = new Matrix(order, order, 0.0);
        double dt = t2 - t;
        double tau;
        for (int i = 0; i < order; ++i)
        {
            tau = 1.0;
            for (int j = i; j < order; ++j)
            {
                tau *= dt;
                eqs[i, j] = (tau * PascalTriangle.Get(j + 1)[i]) / (j + 1);
            }
        }

        return eqs;
    }
}
