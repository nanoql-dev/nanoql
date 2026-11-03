// SPDX-FileCopyrightText: Copyright (C) 2014 Fabien Le Floc'h
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/hestonexpansionengine.hpp ql/pricingengines/vanilla/hestonexpansionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>Lorig, Pagliarani and Pascucci's expansion of order 2 for the Heston model's implied volatility.</summary>
/// <remarks>
/// <para>
/// Its three coefficients are QuantLib's expressions, evaluated where κt calls for it in another arithmetic: QuantLib's
/// double, and so its bits, for κt from the coefficient's cut to its upper cut; double-double below the cut, where the
/// double cancels; quad-double below its floor, where double-double cancels too, and below quad-double's floor the
/// coefficient's cubic in κ through four quad-double values; and past the upper cut, where e^{κt}'s powers overflow, an
/// extended-range double, each cut measured at the run against QuantLib's expressions in 50 to 250 digits. At a term of
/// 0 each coefficient is its limit, the first three <see cref="FordeHestonExpansion"/>'s leading order, where
/// QuantLib's divide 0 by 0.
/// </para>
/// <para>
/// Reference: M. Lorig, S. Pagliarani, A. Pascucci, Explicit implied vols for multifactor local-stochastic vol models,
/// arXiv:1306.5447v3, 2014; the formula's Mathematica notebook at http://explicitsolutions.wordpress.com/.
/// </para>
/// </remarks>
public sealed class LPP2HestonExpansion : HestonExpansion
{
    // each coefficient's cut, double-double and quad-double floors and upper cut in kappa t, measured at the run
    private const double Z0Cut = 0.5, Z0Floor = 2e-6, Z0QuadFloor = 1e-12, Z0Upper = 100.0;
    private const double Z1Cut = 0.2, Z1Floor = 5e-7, Z1QuadFloor = 1e-12, Z1Upper = 200.0;
    private const double Z2Cut = 0.5, Z2Floor = 2e-6, Z2QuadFloor = 1e-12, Z2Upper = 200.0;

    private readonly double c0, c1, c2;

    /// <summary>Initializes a new instance of the <see cref="LPP2HestonExpansion"/> class: the coefficients for one
    /// term.</summary>
    /// <param name="kappa">The variance's mean-reversion speed.</param>
    /// <param name="theta">The variance's long-run mean.</param>
    /// <param name="sigma">The volatility of the variance.</param>
    /// <param name="v0">The initial variance.</param>
    /// <param name="rho">The correlation.</param>
    /// <param name="term">The term.</param>
    public LPP2HestonExpansion(double kappa, double theta, double sigma, double v0, double rho, double term)
    {
        if (term == 0.0)
        {
            // the coefficients' limits as the term falls to 0
            (double Sigma00, double Sigma01, double Sigma02) leading = FordeHestonExpansion.LeadingOrder(sigma, v0, rho);
            c0 = leading.Sigma00;
            c1 = leading.Sigma01;
            c2 = leading.Sigma02;
        }
        else
        {
            c0 = IKappaTExpression.Evaluate(
                new Z0Expression(term, theta, sigma, v0, rho), kappa, term, Z0Cut, Z0Floor, Z0QuadFloor, Z0Upper);
            c1 = IKappaTExpression.Evaluate(
                new Z1Expression(term, theta, sigma, v0, rho), kappa, term, Z1Cut, Z1Floor, Z1QuadFloor, Z1Upper);
            c2 = IKappaTExpression.Evaluate(
                new Z2Expression(term, theta, sigma, v0, rho), kappa, term, Z2Cut, Z2Floor, Z2QuadFloor, Z2Upper);
        }
    }

    /// <inheritdoc/>
    public override double ImpliedVolatility(double strike, double forward)
    {
        double x = Math.Log(strike / forward);
        double vol = c0 + (x * (c1 + (x * c2)));
        return Max(1e-8, vol);
    }

    // the coefficients, for the tests
    internal double Coefficient(int i) => i switch
    {
        0 => c0,
        1 => c1,
        2 => c2,
        _ => throw new ArgumentOutOfRangeException(nameof(i), i, "no such coefficient"),
    };

    // QuantLib's LPP2HestonExpansion::z0, its powers of e^{kappa t} computed as its constructor computes them
    internal static T Z0<T>(T t, T kappa, T theta, T delta, T y, T rho)
        where T : struct, IRealArithmetic<T>
    {
        T ekt = T.Exp(kappa * t);
        T e2kt = ekt * ekt;
        T e3kt = e2kt * ekt;
        return (4*T.Pow(delta,2)*kappa*(-theta - 4*ekt*(theta + kappa*t*(theta - y)) +
            e2kt*((5 - 2*kappa*t)*theta - 2*y) + 2*y)*
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y) +
            128*ekt*T.Pow(kappa,3)*
            T.Pow((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y,2) +
            32*delta*ekt*T.Pow(kappa,2)*rho*
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y)*
            ((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y) +
            T.Pow(delta,2)*ekt*T.Pow(rho,2)*
            (-theta + kappa*t*theta + (theta - y)/ekt + y)*
            T.Pow((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y,2) +
            (48*T.Pow(delta,2)*e2kt*T.Pow(kappa,2)*T.Pow(rho,2)*
            T.Pow((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y,2))/
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y) -
            T.Pow(delta,2)*T.Pow(rho,2)*((1 + ekt*(-1 + kappa*t))*theta +
            (-1 + ekt)*y)*T.Pow((2 + kappa*t + ekt*(-2 + kappa*t))*
            theta + (-1 + ekt - kappa*t)*y,2) +
            2*T.Pow(delta,2)*kappa*((1 + ekt*(-1 + kappa*t))*theta +
            (-1 + ekt)*y)*(theta - 2*y +
            e2kt*(-5*theta + 2*kappa*t*theta + 2*y +
            8*T.Pow(rho,2)*((-3 + kappa*t)*theta + y)) +
            4*ekt*(theta + kappa*t*theta - kappa*t*y +
            T.Pow(rho,2)*((6 + kappa*t*(4 + kappa*t))*theta - (2 + kappa*t*(2 + kappa*t))*y))) -
            (8*T.Pow(delta,2)*T.Pow(kappa,2)*((1 + ekt*(-1 + kappa*t))*theta +
            (-1 + ekt)*y)*(theta - 2*y +
            e2kt*(-5*theta + 2*kappa*t*theta + 2*y +
            8*T.Pow(rho,2)*((-3 + kappa*t)*theta + y)) +
            4*ekt*(theta + kappa*t*theta - kappa*t*y +
            T.Pow(rho,2)*((6 + kappa*t*(4 + kappa*t))*theta - (2 + kappa*t*(2 + kappa*t))*y))))
            /(-theta + kappa*t*theta + (theta - y)/ekt + y))/
            (128.0*e3kt*T.Pow(kappa,5)*T.Pow(t,2)*
            T.Pow((-theta + kappa*t*theta + (theta - y)/ekt + y)/(kappa*t),1.5));
    }

    // QuantLib's LPP2HestonExpansion::z1, its powers of e^{kappa t} computed as its constructor computes them
    internal static T Z1<T>(T t, T kappa, T theta, T delta, T y, T rho)
        where T : struct, IRealArithmetic<T>
    {
        T ekt = T.Exp(kappa * t);
        T e2kt = ekt * ekt;
        return (delta*rho*(-(delta*T.Pow(-1 + ekt,2)*rho*(4*theta - y)*y) +
            2*ekt*T.Pow(kappa,3)*T.Pow(t,2)*theta*
            ((2 + 2*ekt + delta*rho*t)*theta - (2 + delta*rho*t)*y) -
            2*(-1 + ekt)*kappa*(2*theta - y)*
            ((-1 + ekt)*(-2 + delta*rho*t)*theta +
            (-2 + 2*ekt + delta*rho*t)*y) +
            T.Pow(kappa,2)*t*((-1 + ekt)*
            (-4 + delta*rho*t + ekt*(-12 + delta*rho*t))*T.Pow(theta,2) +
            2*(-4 + 4*e2kt + delta*rho*t + 3*delta*ekt*rho*t)*theta*
            y - (-4 + delta*rho*t + 2*ekt*(2 + delta*rho*t))*T.Pow(y,2))))/
            (8.0*T.Pow(kappa,2)*t*T.Sqrt((-theta + kappa*t*theta + (theta - y)/ekt + y)/
            (kappa*t))*T.Pow((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y,
            2));
    }

    // QuantLib's LPP2HestonExpansion::z2, its powers of e^{kappa t} computed as its constructor computes them
    internal static T Z2<T>(T t, T kappa, T theta, T delta, T y, T rho)
        where T : struct, IRealArithmetic<T>
    {
        T ekt = T.Exp(kappa * t);
        T e2kt = ekt * ekt;
        return (T.Pow(delta,2)*T.Sqrt((-theta + kappa*t*theta + (theta - y)/ekt + y)/(kappa*t))*
            (-12*T.Pow(rho,2)*T.Pow((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y,2) +
            (-theta + kappa*t*theta + (theta - y)/ekt + y)*
            (theta - 2*y + e2kt*
            (-5*theta + 2*kappa*t*theta + 2*y + 8*T.Pow(rho,2)*((-3 + kappa*t)*theta + y)) +
            4*ekt*(theta + kappa*t*theta - kappa*t*y +
            T.Pow(rho,2)*((6 + kappa*t*(4 + kappa*t))*theta - (2 + kappa*t*(2 + kappa*t))*y))))
            )/(16.0*e2kt*T.Pow(-theta + kappa*t*theta + (theta - y)/ekt + y,
            4));
    }

    // std::max, which returns its first argument where neither is less than the other
    private static double Max(double a, double b) => a < b ? b : a;

    // z0 as a function of kappa, its other inputs fixed
    private readonly struct Z0Expression(double t, double theta, double delta, double y, double rho) : IKappaTExpression
    {
        public T Evaluate<T>(T kappa)
            where T : struct, IRealArithmetic<T> =>
            Z0(T.Of(t), kappa, T.Of(theta), T.Of(delta), T.Of(y), T.Of(rho));
    }

    // z1 as a function of kappa, its other inputs fixed
    private readonly struct Z1Expression(double t, double theta, double delta, double y, double rho) : IKappaTExpression
    {
        public T Evaluate<T>(T kappa)
            where T : struct, IRealArithmetic<T> =>
            Z1(T.Of(t), kappa, T.Of(theta), T.Of(delta), T.Of(y), T.Of(rho));
    }

    // z2 as a function of kappa, its other inputs fixed
    private readonly struct Z2Expression(double t, double theta, double delta, double y, double rho) : IKappaTExpression
    {
        public T Evaluate<T>(T kappa)
            where T : struct, IRealArithmetic<T> =>
            Z2(T.Of(t), kappa, T.Of(theta), T.Of(delta), T.Of(y), T.Of(rho));
    }
}
