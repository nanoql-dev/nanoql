// SPDX-FileCopyrightText: Copyright (C) 2022 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/qdfpamericanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>
/// The fixed-point equation of the QD engine for the exercise boundary b(τ) = K e^(−(r−q)τ) N(τ, b)/D(τ, b), its
/// integrals read through the boundary B: QuantLib's <c>DqFpEquation</c>. A Gauss-Legendre integrator's nodes and
/// weights are summed directly, in QuantLib's order.
/// </summary>
internal abstract class DqFpEquation
{
    /// <summary>Initializes a new instance of the <see cref="DqFpEquation"/> class.</summary>
    /// <param name="r">The risk-free rate.</param>
    /// <param name="q">The dividend yield.</param>
    /// <param name="vol">The volatility.</param>
    /// <param name="b">The exercise boundary at a time to expiry.</param>
    /// <param name="integrator">The integrator of each step.</param>
    protected DqFpEquation(double r, double q, double vol, Func<double, double> b, Integrator integrator)
    {
        R = r;
        Q = q;
        Vol = vol;
        B = b;
        Integrator = integrator;

        if (integrator is GaussLegendreIntegrator legendreIntegrator)
        {
            XI = legendreIntegrator.GetIntegration().X();
            WI = legendreIntegrator.GetIntegration().Weights();
        }
    }

    /// <summary>Gets the Gauss-Legendre nodes, or <see langword="null"/> for another integrator.</summary>
    protected Vector? XI { get; }

    /// <summary>Gets the Gauss-Legendre weights, or <see langword="null"/> for another integrator.</summary>
    protected Vector? WI { get; }

    /// <summary>Gets the risk-free rate.</summary>
    protected double R { get; }

    /// <summary>Gets the dividend yield.</summary>
    protected double Q { get; }

    /// <summary>Gets the volatility.</summary>
    protected double Vol { get; }

    /// <summary>Gets the exercise boundary at a time to expiry.</summary>
    protected Func<double, double> B { get; }

    /// <summary>Gets the integrator of each step.</summary>
    protected Integrator Integrator { get; }

    /// <summary>Gets the normal density, QuantLib's <c>phi</c>.</summary>
    protected NormalDistribution Density { get; } = new();

    /// <summary>Gets the cumulative normal distribution, QuantLib's <c>Phi</c>.</summary>
    protected CumulativeNormalDistribution Cumulative { get; } = new();

    /// <summary>The derivatives of N and D by the boundary.</summary>
    /// <param name="tau">The time to expiry.</param>
    /// <param name="b">The boundary.</param>
    /// <returns>N′ and D′.</returns>
    internal abstract (double Nd, double Dd) NDd(double tau, double b);

    /// <summary>N, D and the equation's value K e^(−(r−q)τ) N/D.</summary>
    /// <param name="tau">The time to expiry.</param>
    /// <param name="b">The boundary.</param>
    /// <returns>N, D and the value.</returns>
    internal abstract (double N, double D, double Fv) F(double tau, double b);

    /// <summary>d₊ and d₋ at a time and a spot-to-boundary ratio.</summary>
    /// <param name="t">The time.</param>
    /// <param name="z">The ratio.</param>
    /// <returns>d₊ and d₋.</returns>
    protected (double Plus, double Minus) D(double t, double z)
    {
        double v = Vol * Math.Sqrt(t);
        double m = ((Math.Log(z) + ((R - Q) * t)) / v) + (0.5 * v);

        return (m, m - v);
    }
}
