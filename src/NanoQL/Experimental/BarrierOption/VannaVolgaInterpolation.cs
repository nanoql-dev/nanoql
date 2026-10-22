// SPDX-FileCopyrightText: Copyright (C) 2013 Yue Tian
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/barrieroption/vannavolgainterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.PricingEngines;

namespace NanoQL.Experimental;

/// <summary>
/// The Vanna-Volga interpolation of a smile between three volatilities in strike space: the at-the-money call's Black
/// price corrected by the three quoted calls' market premia over their at-the-money ones, weighted by each strike's
/// vega, and turned back into a volatility.
/// </summary>
/// <remarks>
/// The second point is the at-the-money volatility. QuantLib's <c>update()</c>, which computes the premia and vegas and
/// appends them to its vectors on every call, runs once at construction: nanoql's interpolations have no update.
/// Immutable once built; it copies its inputs. It has no primitive, derivative or second derivative, as QuantLib's.
/// </remarks>
[Experimental("NQL0001")]
public sealed class VannaVolgaInterpolation : Interpolation
{
    private readonly double[] premiaBS = new double[3];
    private readonly double[] premiaMKT = new double[3];
    private readonly double[] vegas = new double[3];
    private readonly double atmVol;
    private readonly double spot;
    private readonly double fwd;
    private readonly double dDiscount;
    private readonly double t;

    /// <summary>Initializes a new instance of the <see cref="VannaVolgaInterpolation"/> class.</summary>
    /// <param name="x">The three strikes, sorted.</param>
    /// <param name="y">The three volatilities, the second at the money.</param>
    /// <param name="spot">The spot.</param>
    /// <param name="dDiscount">The domestic discount factor.</param>
    /// <param name="fDiscount">The foreign discount factor.</param>
    /// <param name="t">The time to maturity.</param>
    /// <exception cref="ArgumentException">The sizes differ, or there aren't exactly three points.</exception>
    public VannaVolgaInterpolation(
        ReadOnlySpan<double> x, ReadOnlySpan<double> y, double spot, double dDiscount, double fDiscount, double t)
        : base(x, y, VannaVolga.RequiredPoints)
    {
        this.spot = spot;
        this.dDiscount = dDiscount;
        this.t = t;
        if (!(X.Length == 3))
        {
            throw new ArgumentException(
                "Vanna Volga Interpolator only interpolates 3 volatilities in strike space", nameof(x));
        }

        // QuantLib's update(): atmVol should be the second vol
        atmVol = Y[1];
        fwd = spot * fDiscount / dDiscount;
        for (int i = 0; i < 3; i++)
        {
            premiaBS[i] = BlackFormulaFunctions.BlackFormula(
                OptionType.Call, X[i], fwd, atmVol * Math.Sqrt(t), dDiscount);
            premiaMKT[i] = BlackFormulaFunctions.BlackFormula(
                OptionType.Call, X[i], fwd, Y[i] * Math.Sqrt(t), dDiscount);
            vegas[i] = Vega(X[i]);
        }
    }

    /// <inheritdoc/>
    protected override double ValueCore(double k)
    {
        ReadOnlySpan<double> xs = X;
        double x1 = Vega(k) / vegas[0]
            * (Math.Log(xs[1] / k) * Math.Log(xs[2] / k))
            / (Math.Log(xs[1] / xs[0]) * Math.Log(xs[2] / xs[0]));
        double x2 = Vega(k) / vegas[1]
            * (Math.Log(k / xs[0]) * Math.Log(xs[2] / k))
            / (Math.Log(xs[1] / xs[0]) * Math.Log(xs[2] / xs[1]));
        double x3 = Vega(k) / vegas[2]
            * (Math.Log(k / xs[0]) * Math.Log(k / xs[1]))
            / (Math.Log(xs[2] / xs[0]) * Math.Log(xs[2] / xs[1]));
        double cBS = BlackFormulaFunctions.BlackFormula(OptionType.Call, k, fwd, atmVol * Math.Sqrt(t), dDiscount);
        double c = cBS + (x1 * (premiaMKT[0] - premiaBS[0])) + (x2 * (premiaMKT[1] - premiaBS[1]))
            + (x3 * (premiaMKT[2] - premiaBS[2]));
        double std = BlackFormulaFunctions.BlackFormulaImpliedStdDev(OptionType.Call, k, fwd, c, dDiscount);
        return std / Math.Sqrt(t);
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always, with QuantLib's "Vanna Volga primitive not implemented".</exception>
    protected override double PrimitiveCore(double x) =>
        throw new NanoQLException("Vanna Volga primitive not implemented");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always, with QuantLib's "Vanna Volga derivative not implemented".</exception>
    protected override double DerivativeCore(double x) =>
        throw new NanoQLException("Vanna Volga derivative not implemented");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always, with QuantLib's "Vanna Volga secondDerivative not
    /// implemented".</exception>
    protected override double SecondDerivativeCore(double x) =>
        throw new NanoQLException("Vanna Volga secondDerivative not implemented");

    private double Vega(double k)
    {
        double d1 = (Math.Log(fwd / k) + (0.5 * Math.Pow(atmVol, 2.0) * t)) / (atmVol * Math.Sqrt(t));
        var norm = new NormalDistribution();
        return spot * dDiscount * Math.Sqrt(t) * norm.Evaluate(d1);
    }
}
