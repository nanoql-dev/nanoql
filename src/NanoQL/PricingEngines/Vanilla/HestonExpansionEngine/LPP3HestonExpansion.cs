// SPDX-FileCopyrightText: Copyright (C) 2014 Fabien Le Floc'h
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/hestonexpansionengine.hpp ql/pricingengines/vanilla/hestonexpansionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>Lorig, Pagliarani and Pascucci's expansion of order 3 for the Heston model's implied volatility.</summary>
/// <remarks>
/// <para>
/// Its four coefficients are QuantLib's expressions, evaluated where κt calls for it in another arithmetic (the
/// S-hes plan's HR6 and HR11): QuantLib's double, and so its bits, for κt from the coefficient's cut to its upper
/// cut; double-double below the cut, where the double cancels; quad-double below its floor, where double-double
/// cancels too, and below quad-double's floor the coefficient's cubic in κ through four quad-double values; and past
/// the upper cut, where e^{κt}'s powers overflow, an extended-range double, each cut measured at the run against
/// QuantLib's expressions in 50 to 250 digits. At a term of 0 each coefficient is its limit, the first three
/// <see cref="FordeHestonExpansion"/>'s leading order (HR7 (xxxii)), where QuantLib's divide 0 by 0.
/// </para>
/// <para>
/// Reference: M. Lorig, S. Pagliarani, A. Pascucci, Explicit implied vols for multifactor local-stochastic vol models,
/// arXiv:1306.5447v3, 2014; the formula's Mathematica notebook at http://explicitsolutions.wordpress.com/.
/// </para>
/// </remarks>
public sealed class LPP3HestonExpansion : HestonExpansion
{
    // each coefficient's cut, double-double and quad-double floors and upper cut in kappa t, measured at the run (HR6,
    // HR11; the quad-double floors the user's ruling of R2-1)
    private const double Z0Cut = 2.0, Z0Floor = 1e-3, Z0QuadFloor = 5e-10, Z0Upper = 100.0;
    private const double Z1Cut = 1.0, Z1Floor = 5e-4, Z1QuadFloor = 2e-10, Z1Upper = 100.0;
    private const double Z2Cut = 1.0, Z2Floor = 5e-5, Z2QuadFloor = 1e-12, Z2Upper = 100.0;
    private const double Z3Cut = 1.0, Z3Floor = 5e-3, Z3QuadFloor = 5e-8, Z3Upper = 100.0;

    private readonly double c0, c1, c2, c3;

    /// <summary>Initializes a new instance of the <see cref="LPP3HestonExpansion"/> class: the coefficients for one
    /// term.</summary>
    /// <param name="kappa">The variance's mean-reversion speed.</param>
    /// <param name="theta">The variance's long-run mean.</param>
    /// <param name="sigma">The volatility of the variance.</param>
    /// <param name="v0">The initial variance.</param>
    /// <param name="rho">The correlation.</param>
    /// <param name="term">The term.</param>
    public LPP3HestonExpansion(double kappa, double theta, double sigma, double v0, double rho, double term)
    {
        if (term == 0.0)
        {
            // the coefficients' limits as the term falls to 0 (HR7 (xxxii))
            (double Sigma00, double Sigma01, double Sigma02) leading = FordeHestonExpansion.LeadingOrder(sigma, v0, rho);
            c0 = leading.Sigma00;
            c1 = leading.Sigma01;
            c2 = leading.Sigma02;
            c3 = Z3Limit(sigma, v0, rho);
        }
        else
        {
            c0 = IKappaTExpression.Evaluate(
                new Z0Expression(term, theta, sigma, v0, rho), kappa, term, Z0Cut, Z0Floor, Z0QuadFloor, Z0Upper);
            c1 = IKappaTExpression.Evaluate(
                new Z1Expression(term, theta, sigma, v0, rho), kappa, term, Z1Cut, Z1Floor, Z1QuadFloor, Z1Upper);
            c2 = IKappaTExpression.Evaluate(
                new Z2Expression(term, theta, sigma, v0, rho), kappa, term, Z2Cut, Z2Floor, Z2QuadFloor, Z2Upper);
            c3 = IKappaTExpression.Evaluate(
                new Z3Expression(term, theta, sigma, v0, rho), kappa, term, Z3Cut, Z3Floor, Z3QuadFloor, Z3Upper);
        }
    }

    /// <inheritdoc/>
    public override double ImpliedVolatility(double strike, double forward)
    {
        double x = Math.Log(strike / forward);
        double vol = c0 + (x * (c1 + (x * (c2 + (x * c3)))));
        return Max(1e-8, vol);
    }

    // the coefficients, for the tests
    internal double Coefficient(int i) => i switch
    {
        0 => c0,
        1 => c1,
        2 => c2,
        3 => c3,
        _ => throw new ArgumentOutOfRangeException(nameof(i), i, "no such coefficient"),
    };

    // QuantLib's LPP3HestonExpansion::z0, its powers of e^{kappa t} computed as its constructor computes them
    internal static T Z0<T>(T t, T kappa, T theta, T delta, T y, T rho)
        where T : struct, IRealArithmetic<T>
    {
        T ekt = T.Exp(kappa * t);
        T e2kt = ekt * ekt;
        T e3kt = e2kt * ekt;
        T e4kt = e2kt * e2kt;
        return (96*T.Pow(delta,2)*ekt*T.Pow(kappa,3)*
            (-theta - 4*ekt*(theta + kappa*t*(theta - y)) +
            e2kt*((5 - 2*kappa*t)*theta - 2*y) + 2*y)*
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y) +
            3072*e2kt*T.Pow(kappa,5)*
            T.Pow((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y,2) +
            96*T.Pow(delta,3)*ekt*T.Pow(kappa,2)*rho*
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y)*
            (-2*theta - kappa*t*theta - 2*ekt*(2 + kappa*t)*
            (2*theta + kappa*t*(theta - y)) + e2kt*((10 - 3*kappa*t)*theta - 3*y) +
            3*y + 2*kappa*t*y) + 768*delta*e2kt*T.Pow(kappa,4)*rho*
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y)*
            ((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y) +
            6*T.Pow(delta,3)*kappa*rho*(-theta - 4*ekt*(theta + kappa*t*(theta - y)) +
            e2kt*((5 - 2*kappa*t)*theta - 2*y) + 2*y)*
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y)*
            ((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y) +
            24*T.Pow(delta,2)*e2kt*T.Pow(kappa,2)*T.Pow(rho,2)*
            (-theta + kappa*t*theta + (theta - y)/ekt + y)*
            T.Pow((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y,2) +
            (1152*T.Pow(delta,2)*e3kt*T.Pow(kappa,4)*T.Pow(rho,2)*
            T.Pow((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y,2))/
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y) -
            24*T.Pow(delta,2)*ekt*T.Pow(kappa,2)*T.Pow(rho,2)*
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y)*
            T.Pow((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y,2) +
            80*T.Pow(delta,3)*ekt*kappa*T.Pow(rho,3)*
            T.Pow((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y,3) +
            T.Pow(delta,3)*ekt*T.Pow(rho,3)*
            (-theta + kappa*t*theta + (theta - y)/ekt + y)*
            T.Pow((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y,3) -
            (1440*T.Pow(delta,3)*e3kt*T.Pow(kappa,3)*T.Pow(rho,3)*
            T.Pow((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y,3))/
            T.Pow((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y,2) -
            (528*T.Pow(delta,3)*e2kt*T.Pow(kappa,2)*T.Pow(rho,3)*
            T.Pow((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y,3))/
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y) -
            3*T.Pow(delta,3)*T.Pow(rho,3)*((1 + ekt*(-1 + kappa*t))*theta +
            (-1 + ekt)*y)*T.Pow((2 + kappa*t + ekt*(-2 + kappa*t))*
            theta + (-1 + ekt - kappa*t)*y,3) +
            384*T.Pow(delta,3)*e2kt*T.Pow(kappa,3)*rho*
            ((2 + kappa*t + 2*ekt*T.Pow(2 + kappa*t,2) +
            e2kt*(-10 + 3*kappa*t))*theta +
            (-3 + 3*e2kt - 2*kappa*t - 2*ekt*kappa*t*(2 + kappa*t))*y) -
            (576*T.Pow(delta,3)*e2kt*T.Pow(kappa,3)*rho*
            ((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y)*
            ((1 + e2kt*(-5 + 2*kappa*t + 4*T.Pow(rho,2)*(-3 + kappa*t)) +
            2*ekt*(2 + 2*kappa*t +
            T.Pow(rho,2)*(6 + 4*kappa*t + T.Pow(kappa,2)*T.Pow(t,2))))*theta +
            2*(-1 + e2kt*(1 + 2*T.Pow(rho,2)) -
            ekt*(2*kappa*t +
            T.Pow(rho,2)*(2 + 2*kappa*t + T.Pow(kappa,2)*T.Pow(t,2))))*y))/
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y) +
            T.Pow(delta,3)*rho*((1 + ekt*(-1 + kappa*t))*theta +
            (-1 + ekt)*y)*((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y)*
            (theta*(12*ekt*T.Pow(kappa,3)*T.Pow(rho,2)*T.Pow(t,2) +
            8*T.Pow(-1 + ekt,2)*T.Pow(rho,2)*theta -
            (-1 + ekt)*kappa*
            (3 + 8*T.Pow(rho,2)*t*theta + ekt*(15 + 8*T.Pow(rho,2)*(9 + t*theta)))
            + 2*T.Pow(kappa,2)*t*(T.Pow(rho,2)*t*theta +
            2*ekt*(3 + T.Pow(rho,2)*(12 + t*theta)) +
            e2kt*(3 + T.Pow(rho,2)*(12 + t*theta)))) -
            2*(6*ekt*T.Pow(kappa,3)*T.Pow(rho,2)*T.Pow(t,2) +
            4*T.Pow(-1 + ekt,2)*T.Pow(rho,2)*theta +
            2*T.Pow(kappa,2)*t*(T.Pow(rho,2)*t*theta +
            ekt*(3 + T.Pow(rho,2)*(6 + t*theta))) -
            (-1 + ekt)*kappa*
            (3 + 6*T.Pow(rho,2)*t*theta + ekt*(3 + 2*T.Pow(rho,2)*(6 + t*theta))))*
            y + 2*T.Pow(rho,2)*T.Pow(1 - ekt + kappa*t,2)*T.Pow(y,2)) -
            (40*T.Pow(delta,3)*kappa*rho*((1 + ekt*(-1 + kappa*t))*theta +
            (-1 + ekt)*y)*((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y)*
            (theta*(12*ekt*T.Pow(kappa,3)*T.Pow(rho,2)*T.Pow(t,2) +
            8*T.Pow(-1 + ekt,2)*T.Pow(rho,2)*theta -
            (-1 + ekt)*kappa*
            (3 + 8*T.Pow(rho,2)*t*theta +
            ekt*(15 + 8*T.Pow(rho,2)*(9 + t*theta))) +
            2*T.Pow(kappa,2)*t*(T.Pow(rho,2)*t*theta +
            2*ekt*(3 + T.Pow(rho,2)*(12 + t*theta)) +
            e2kt*(3 + T.Pow(rho,2)*(12 + t*theta)))) -
            2*(6*ekt*T.Pow(kappa,3)*T.Pow(rho,2)*T.Pow(t,2) +
            4*T.Pow(-1 + ekt,2)*T.Pow(rho,2)*theta +
            2*T.Pow(kappa,2)*t*(T.Pow(rho,2)*t*theta +
            ekt*(3 + T.Pow(rho,2)*(6 + t*theta))) -
            (-1 + ekt)*kappa*
            (3 + 6*T.Pow(rho,2)*t*theta + ekt*(3 + 2*T.Pow(rho,2)*(6 + t*theta)))
            )*y + 2*T.Pow(rho,2)*T.Pow(1 - ekt + kappa*t,2)*T.Pow(y,2)))/
            (-theta + kappa*t*theta + (theta - y)/ekt + y) -
            12*T.Pow(delta,3)*kappa*rho*((1 + ekt*(-1 + kappa*t))*theta +
            (-1 + ekt)*y)*(2*theta + kappa*t*theta - y - kappa*t*y +
            ekt*((-2 + kappa*t)*theta + y))*
            (theta - 2*y + e2kt*
            (-5*theta + 2*kappa*t*theta + 2*y + 4*T.Pow(rho,2)*((-3 + kappa*t)*theta + y)) +
            2*ekt*(2*(theta + kappa*t*(theta - y)) +
            T.Pow(rho,2)*((6 + kappa*t*(4 + kappa*t))*theta - (2 + kappa*t*(2 + kappa*t))*y))) +
            (288*T.Pow(delta,3)*T.Pow(kappa,2)*rho*
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y)*
            (2*theta + kappa*t*theta - y - kappa*t*y + ekt*((-2 + kappa*t)*theta + y))*
            (theta - 2*y + e2kt*
            (-5*theta + 2*kappa*t*theta + 2*y + 4*T.Pow(rho,2)*((-3 + kappa*t)*theta + y)) +
            2*ekt*(2*(theta + kappa*t*(theta - y)) +
            T.Pow(rho,2)*((6 + kappa*t*(4 + kappa*t))*theta - (2 + kappa*t*(2 + kappa*t))*y))))
            /(-theta + kappa*t*theta + (theta - y)/ekt + y) +
            48*T.Pow(delta,2)*ekt*T.Pow(kappa,3)*
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y)*
            (theta - 2*y + e2kt*
            (-5*theta + 2*kappa*t*theta + 2*y + 8*T.Pow(rho,2)*((-3 + kappa*t)*theta + y)) +
            4*ekt*(theta + kappa*t*theta - kappa*t*y +
            T.Pow(rho,2)*((6 + kappa*t*(4 + kappa*t))*theta - (2 + kappa*t*(2 + kappa*t))*y))) -
            (192*T.Pow(delta,2)*ekt*T.Pow(kappa,4)*
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y)*
            (theta - 2*y + e2kt*
            (-5*theta + 2*kappa*t*theta + 2*y + 8*T.Pow(rho,2)*((-3 + kappa*t)*theta + y)) +
            4*ekt*(theta + kappa*t*theta - kappa*t*y +
            T.Pow(rho,2)*((6 + kappa*t*(4 + kappa*t))*theta - (2 + kappa*t*(2 + kappa*t))*y))))
            /(-theta + kappa*t*theta + (theta - y)/ekt + y) +
            3*T.Pow(delta,3)*kappa*rho*((1 + ekt*(-1 + kappa*t))*theta +
            (-1 + ekt)*y)*((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y)*
            (theta - 2*y + e2kt*
            (-5*theta + 2*kappa*t*theta + 2*y + 8*T.Pow(rho,2)*((-3 + kappa*t)*theta + y)) +
            4*ekt*(theta + kappa*t*theta - kappa*t*y +
            T.Pow(rho,2)*((6 + kappa*t*(4 + kappa*t))*theta - (2 + kappa*t*(2 + kappa*t))*y))) -
            (12*T.Pow(delta,3)*T.Pow(kappa,2)*rho*
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y)*
            ((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y)*
            (theta - 2*y + e2kt*
            (-5*theta + 2*kappa*t*theta + 2*y + 8*T.Pow(rho,2)*((-3 + kappa*t)*theta + y)) +
            4*ekt*(theta + kappa*t*theta - kappa*t*y +
            T.Pow(rho,2)*((6 + kappa*t*(4 + kappa*t))*theta - (2 + kappa*t*(2 + kappa*t))*y))))
            /(-theta + kappa*t*theta + (theta - y)/ekt + y) +
            4*T.Pow(delta,3)*kappa*rho*((1 + ekt*(-1 + kappa*t))*theta +
            (-1 + ekt)*y)*(3*(theta - 2*y)*((2 + kappa*t)*theta - (1 + kappa*t)*y) +
            3*ekt*(6*T.Pow(theta,2) + theta*y - 2*T.Pow(y,2) +
            kappa*(13*t*T.Pow(theta,2) + theta*(8 - 18*t*y) + 4*y*(-3 + t*y)) +
            4*T.Pow(kappa,2)*t*(theta + t*T.Pow(theta,2) - 2*t*theta*y + y*(-2 + t*y))) +
            3*e3kt*(10*T.Pow(theta,2) +
            2*T.Pow(kappa,2)*t*theta*(6 + 8*T.Pow(rho,2) + t*theta) - 9*theta*y + 2*T.Pow(y,2) +
            kappa*(-9*t*T.Pow(theta,2) + 4*(3 + 4*T.Pow(rho,2))*y +
            theta*(-40 - 64*T.Pow(rho,2) + 4*t*y))) +
            e2kt*(-54*T.Pow(theta,2) +
            8*T.Pow(kappa,4)*T.Pow(rho,2)*T.Pow(t,3)*(theta - y) + 39*theta*y - 6*T.Pow(y,2) +
            24*T.Pow(kappa,3)*T.Pow(t,2)*(theta + 2*T.Pow(rho,2)*theta - (1 + T.Pow(rho,2))*y) +
            6*T.Pow(kappa,2)*t*(3*t*T.Pow(theta,2) - 8*(1 + T.Pow(rho,2))*y +
            theta*(16 + 24*T.Pow(rho,2) - 3*t*y)) -
            3*kappa*(5*t*T.Pow(theta,2) + 2*y*(8*T.Pow(rho,2) + 3*t*y) -
            theta*(32 + 64*T.Pow(rho,2) + 17*t*y)))) -
            (48*T.Pow(delta,3)*T.Pow(kappa,2)*rho*
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y)*
            (3*(theta - 2*y)*((2 + kappa*t)*theta - (1 + kappa*t)*y) +
            3*ekt*(6*T.Pow(theta,2) + theta*y - 2*T.Pow(y,2) +
            kappa*(13*t*T.Pow(theta,2) + theta*(8 - 18*t*y) + 4*y*(-3 + t*y)) +
            4*T.Pow(kappa,2)*t*(theta + t*T.Pow(theta,2) - 2*t*theta*y + y*(-2 + t*y))) +
            3*e3kt*(10*T.Pow(theta,2) +
            2*T.Pow(kappa,2)*t*theta*(6 + 8*T.Pow(rho,2) + t*theta) - 9*theta*y +
            2*T.Pow(y,2) + kappa*(-9*t*T.Pow(theta,2) + 4*(3 + 4*T.Pow(rho,2))*y +
            theta*(-40 - 64*T.Pow(rho,2) + 4*t*y))) +
            e2kt*(-54*T.Pow(theta,2) +
            8*T.Pow(kappa,4)*T.Pow(rho,2)*T.Pow(t,3)*(theta - y) + 39*theta*y - 6*T.Pow(y,2) +
            24*T.Pow(kappa,3)*T.Pow(t,2)*
            (theta + 2*T.Pow(rho,2)*theta - (1 + T.Pow(rho,2))*y) +
            6*T.Pow(kappa,2)*t*(3*t*T.Pow(theta,2) - 8*(1 + T.Pow(rho,2))*y +
            theta*(16 + 24*T.Pow(rho,2) - 3*t*y)) -
            3*kappa*(5*t*T.Pow(theta,2) + 2*y*(8*T.Pow(rho,2) + 3*t*y) -
            theta*(32 + 64*T.Pow(rho,2) + 17*t*y)))))/
            (-theta + kappa*t*theta + (theta - y)/ekt + y) +
            (240*T.Pow(delta,3)*e2kt*T.Pow(kappa,2)*rho*
            ((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y)*
            (12*ekt*T.Pow(kappa,3)*T.Pow(rho,2)*T.Pow(t,2)*(theta - y) +
            2*T.Pow(-1 + ekt,2)*T.Pow(rho,2)*T.Pow(-2*theta + y,2) -
            (-1 + ekt)*kappa*
            (8*(1 + ekt)*T.Pow(rho,2)*t*T.Pow(theta,2) +
            2*y*(-3 - 3*ekt*(1 + 4*T.Pow(rho,2)) + 2*T.Pow(rho,2)*t*y) +
            theta*(3 - 12*T.Pow(rho,2)*t*y + ekt*(15 + T.Pow(rho,2)*(72 - 4*t*y)))
            ) + 2*T.Pow(kappa,2)*t*(e2kt*theta*
            (3 + T.Pow(rho,2)*(12 + t*theta)) + T.Pow(rho,2)*t*T.Pow(theta - y,2) +
            2*ekt*(T.Pow(rho,2)*t*T.Pow(theta,2) - 3*(y + 2*T.Pow(rho,2)*y) +
            theta*(3 + T.Pow(rho,2)*(12 - t*y))))))/
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y))/
            (3072.0*e4kt*T.Pow(kappa,7)*T.Pow(t,2)*
            T.Pow((-theta + kappa*t*theta + (theta - y)/ekt + y)/(kappa*t),1.5));
    }

    // QuantLib's LPP3HestonExpansion::z1, its powers of e^{kappa t} computed as its constructor computes them
    internal static T Z1<T>(T t, T kappa, T theta, T delta, T y, T rho)
        where T : struct, IRealArithmetic<T>
    {
        T ekt = T.Exp(kappa * t);
        T e2kt = ekt * ekt;
        T e3kt = e2kt * ekt;
        return (delta*(768*e2kt*T.Pow(kappa,4)*rho*
            ((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y) -
            (576*delta*e2kt*T.Pow(kappa,3)*T.Pow(rho,2)*
            T.Pow((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y,2))/
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y) -
            10*T.Pow(delta,2)*T.Pow(rho,3)*T.Pow((2 + kappa*t + ekt*(-2 + kappa*t))*
            theta + (-1 + ekt - kappa*t)*y,3) +
            (6*T.Pow(delta,2)*kappa*T.Pow(rho,3)*
            T.Pow((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y,3))/
            (-theta + kappa*t*theta + (theta - y)/ekt + y) -
            (3360*T.Pow(delta,2)*e3kt*T.Pow(kappa,3)*T.Pow(rho,3)*
            T.Pow((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y,3))/
            T.Pow((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y,3) -
            (288*T.Pow(delta,2)*e2kt*T.Pow(kappa,2)*T.Pow(rho,3)*
            T.Pow((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y,3))/
            T.Pow((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y,2) +
            (234*T.Pow(delta,2)*ekt*kappa*T.Pow(rho,3)*
            T.Pow((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y,3))/
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y) -
            96*delta*ekt*T.Pow(kappa,3)*
            ((1 + 4*ekt*(1 + kappa*t) + e2kt*(-5 + 2*kappa*t))*theta +
            2*(-1 + e2kt - 2*ekt*kappa*t)*y) -
            12*T.Pow(delta,2)*kappa*rho*((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y)*
            ((1 + 4*ekt*(1 + kappa*t) + e2kt*(-5 + 2*kappa*t))*theta +
            2*(-1 + e2kt - 2*ekt*kappa*t)*y) -
            192*T.Pow(delta,2)*ekt*T.Pow(kappa,2)*rho*
            ((2 + kappa*t + 2*ekt*T.Pow(2 + kappa*t,2) +
            e2kt*(-10 + 3*kappa*t))*theta +
            (-3 + 3*e2kt - 2*kappa*t - 2*ekt*kappa*t*(2 + kappa*t))*y)
            - (12*T.Pow(delta,2)*ekt*T.Pow(kappa,2)*rho*
            ((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y)*
            ((1 + e2kt*(-5 + 2*kappa*t + 8*T.Pow(rho,2)*(-3 + kappa*t)) +
            4*ekt*(1 + kappa*t +
            T.Pow(rho,2)*(6 + 4*kappa*t + T.Pow(kappa,2)*T.Pow(t,2))))*theta +
            2*(-1 + e2kt*(1 + 4*T.Pow(rho,2)) -
            2*ekt*(kappa*t +
            T.Pow(rho,2)*(2 + 2*kappa*t + T.Pow(kappa,2)*T.Pow(t,2))))*y))/
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y) +
            (576*T.Pow(delta,2)*ekt*T.Pow(kappa,2)*rho*
            ((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y)*
            ((1 + e2kt*(-5 + 2*kappa*t + 4*T.Pow(rho,2)*(-3 + kappa*t)) +
            2*ekt*(2 + 2*kappa*t +
            T.Pow(rho,2)*(6 + 4*kappa*t + T.Pow(kappa,2)*T.Pow(t,2))))*theta +
            2*(-1 + e2kt*(1 + 2*T.Pow(rho,2)) -
            ekt*(2*kappa*t +
            T.Pow(rho,2)*(2 + 2*kappa*t + T.Pow(kappa,2)*T.Pow(t,2))))*y))/
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y) +
            (5*T.Pow(delta,2)*rho*((1 + ekt*(-1 + kappa*t))*theta +
            (-1 + ekt)*y)*
            ((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y)*
            (theta*(12*ekt*T.Pow(kappa,3)*T.Pow(rho,2)*T.Pow(t,2) +
            8*T.Pow(-1 + ekt,2)*T.Pow(rho,2)*theta -
            (-1 + ekt)*kappa*
            (3 + 8*T.Pow(rho,2)*t*theta +
            ekt*(15 + 8*T.Pow(rho,2)*(9 + t*theta))) +
            2*T.Pow(kappa,2)*t*(T.Pow(rho,2)*t*theta +
            2*ekt*(3 + T.Pow(rho,2)*(12 + t*theta)) +
            e2kt*(3 + T.Pow(rho,2)*(12 + t*theta)))) -
            2*(6*ekt*T.Pow(kappa,3)*T.Pow(rho,2)*T.Pow(t,2) +
            4*T.Pow(-1 + ekt,2)*T.Pow(rho,2)*theta +
            2*T.Pow(kappa,2)*t*(T.Pow(rho,2)*t*theta +
            ekt*(3 + T.Pow(rho,2)*(6 + t*theta))) -
            (-1 + ekt)*kappa*
            (3 + 6*T.Pow(rho,2)*t*theta +
            ekt*(3 + 2*T.Pow(rho,2)*(6 + t*theta))))*y +
            2*T.Pow(rho,2)*T.Pow(1 - ekt + kappa*t,2)*T.Pow(y,2)))/
            (ekt*(-theta + kappa*t*theta + (theta - y)/ekt + y)) -
            (48*T.Pow(delta,2)*kappa*rho*((1 + ekt*(-1 + kappa*t))*theta +
            (-1 + ekt)*y)*
            (2*theta + kappa*t*theta - y - kappa*t*y +
            ekt*((-2 + kappa*t)*theta + y))*
            (theta - 2*y + e2kt*
            (-5*theta + 2*kappa*t*theta + 2*y + 4*T.Pow(rho,2)*((-3 + kappa*t)*theta + y)) +
            2*ekt*(2*(theta + kappa*t*(theta - y)) +
            T.Pow(rho,2)*((6 + kappa*t*(4 + kappa*t))*theta - (2 + kappa*t*(2 + kappa*t))*y))
            ))/(ekt*(-theta + kappa*t*theta + (theta - y)/ekt + y)) +
            (96*delta*T.Pow(kappa,3)*((1 + ekt*(-1 + kappa*t))*theta +
            (-1 + ekt)*y)*
            (theta - 2*y + e2kt*
            (-5*theta + 2*kappa*t*theta + 2*y + 8*T.Pow(rho,2)*((-3 + kappa*t)*theta + y)) +
            4*ekt*(theta + kappa*t*theta - kappa*t*y +
            T.Pow(rho,2)*((6 + kappa*t*(4 + kappa*t))*theta - (2 + kappa*t*(2 + kappa*t))*y))
            ))/(-theta + kappa*t*theta + (theta - y)/ekt + y) +
            (9*T.Pow(delta,2)*kappa*rho*((1 + ekt*(-1 + kappa*t))*theta +
            (-1 + ekt)*y)*
            ((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y)*
            (theta - 2*y + e2kt*
            (-5*theta + 2*kappa*t*theta + 2*y + 8*T.Pow(rho,2)*((-3 + kappa*t)*theta + y)) +
            4*ekt*(theta + kappa*t*theta - kappa*t*y +
            T.Pow(rho,2)*((6 + kappa*t*(4 + kappa*t))*theta - (2 + kappa*t*(2 + kappa*t))*y))
            ))/(ekt*(-theta + kappa*t*theta + (theta - y)/ekt + y)) -
            (48*T.Pow(delta,2)*ekt*T.Pow(kappa,2)*rho*
            (3*(theta - 2*y)*((2 + kappa*t)*theta - (1 + kappa*t)*y) +
            3*ekt*(6*T.Pow(theta,2) + theta*y - 2*T.Pow(y,2) +
            kappa*(13*t*T.Pow(theta,2) + theta*(8 - 18*t*y) + 4*y*(-3 + t*y)) +
            4*T.Pow(kappa,2)*t*(theta + t*T.Pow(theta,2) - 2*t*theta*y + y*(-2 + t*y))) +
            3*e3kt*(10*T.Pow(theta,2) +
            2*T.Pow(kappa,2)*t*theta*(6 + 8*T.Pow(rho,2) + t*theta) - 9*theta*y +
            2*T.Pow(y,2) + kappa*(-9*t*T.Pow(theta,2) + 4*(3 + 4*T.Pow(rho,2))*y +
            theta*(-40 - 64*T.Pow(rho,2) + 4*t*y))) +
            e2kt*(-54*T.Pow(theta,2) +
            8*T.Pow(kappa,4)*T.Pow(rho,2)*T.Pow(t,3)*(theta - y) + 39*theta*y -
            6*T.Pow(y,2) + 24*T.Pow(kappa,3)*T.Pow(t,2)*
            (theta + 2*T.Pow(rho,2)*theta - (1 + T.Pow(rho,2))*y) +
            6*T.Pow(kappa,2)*t*(3*t*T.Pow(theta,2) - 8*(1 + T.Pow(rho,2))*y +
            theta*(16 + 24*T.Pow(rho,2) - 3*t*y)) -
            3*kappa*(5*t*T.Pow(theta,2) + 2*y*(8*T.Pow(rho,2) + 3*t*y) -
            theta*(32 + 64*T.Pow(rho,2) + 17*t*y)))))/
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y) +
            (12*T.Pow(delta,2)*kappa*rho*((1 + ekt*(-1 + kappa*t))*theta +
            (-1 + ekt)*y)*
            (3*(theta - 2*y)*((2 + kappa*t)*theta - (1 + kappa*t)*y) +
            3*ekt*(6*T.Pow(theta,2) + theta*y - 2*T.Pow(y,2) +
            kappa*(13*t*T.Pow(theta,2) + theta*(8 - 18*t*y) + 4*y*(-3 + t*y)) +
            4*T.Pow(kappa,2)*t*(theta + t*T.Pow(theta,2) - 2*t*theta*y + y*(-2 + t*y))) +
            3*e3kt*(10*T.Pow(theta,2) +
            2*T.Pow(kappa,2)*t*theta*(6 + 8*T.Pow(rho,2) + t*theta) - 9*theta*y +
            2*T.Pow(y,2) + kappa*(-9*t*T.Pow(theta,2) + 4*(3 + 4*T.Pow(rho,2))*y +
            theta*(-40 - 64*T.Pow(rho,2) + 4*t*y))) +
            e2kt*(-54*T.Pow(theta,2) +
            8*T.Pow(kappa,4)*T.Pow(rho,2)*T.Pow(t,3)*(theta - y) + 39*theta*y -
            6*T.Pow(y,2) + 24*T.Pow(kappa,3)*T.Pow(t,2)*
            (theta + 2*T.Pow(rho,2)*theta - (1 + T.Pow(rho,2))*y) +
            6*T.Pow(kappa,2)*t*(3*t*T.Pow(theta,2) - 8*(1 + T.Pow(rho,2))*y +
            theta*(16 + 24*T.Pow(rho,2) - 3*t*y)) -
            3*kappa*(5*t*T.Pow(theta,2) + 2*y*(8*T.Pow(rho,2) + 3*t*y) -
            theta*(32 + 64*T.Pow(rho,2) + 17*t*y)))))/
            (ekt*(-theta + kappa*t*theta + (theta - y)/ekt + y)) +
            (240*T.Pow(delta,2)*e2kt*T.Pow(kappa,2)*rho*
            ((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y)*
            (12*ekt*T.Pow(kappa,3)*T.Pow(rho,2)*T.Pow(t,2)*(theta - y) +
            2*T.Pow(-1 + ekt,2)*T.Pow(rho,2)*T.Pow(-2*theta + y,2) -
            (-1 + ekt)*kappa*
            (8*(1 + ekt)*T.Pow(rho,2)*t*T.Pow(theta,2) +
            2*y*(-3 - 3*ekt*(1 + 4*T.Pow(rho,2)) + 2*T.Pow(rho,2)*t*y) +
            theta*(3 - 12*T.Pow(rho,2)*t*y +
            ekt*(15 + T.Pow(rho,2)*(72 - 4*t*y)))) +
            2*T.Pow(kappa,2)*t*(e2kt*theta*(3 + T.Pow(rho,2)*(12 + t*theta)) +
            T.Pow(rho,2)*t*T.Pow(theta - y,2) +
            2*ekt*(T.Pow(rho,2)*t*T.Pow(theta,2) - 3*(y + 2*T.Pow(rho,2)*y) +
            theta*(3 + T.Pow(rho,2)*(12 - t*y))))))/
            T.Pow((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y,2) -
            (120*T.Pow(delta,2)*ekt*kappa*rho*
            ((2 + kappa*t + ekt*(-2 + kappa*t))*theta +
            (-1 + ekt - kappa*t)*y)*
            (12*ekt*T.Pow(kappa,3)*T.Pow(rho,2)*T.Pow(t,2)*(theta - y) +
            2*T.Pow(-1 + ekt,2)*T.Pow(rho,2)*T.Pow(-2*theta + y,2) -
            (-1 + ekt)*kappa*
            (8*(1 + ekt)*T.Pow(rho,2)*t*T.Pow(theta,2) +
            2*y*(-3 - 3*ekt*(1 + 4*T.Pow(rho,2)) + 2*T.Pow(rho,2)*t*y) +
            theta*(3 - 12*T.Pow(rho,2)*t*y +
            ekt*(15 + T.Pow(rho,2)*(72 - 4*t*y)))) +
            2*T.Pow(kappa,2)*t*(e2kt*theta*(3 + T.Pow(rho,2)*(12 + t*theta)) +
            T.Pow(rho,2)*t*T.Pow(theta - y,2) +
            2*ekt*(T.Pow(rho,2)*t*T.Pow(theta,2) - 3*(y + 2*T.Pow(rho,2)*y) +
            theta*(3 + T.Pow(rho,2)*(12 - t*y))))))/
            ((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y)))/
            (1536.0*e3kt*T.Pow(kappa,6)*T.Pow(t,2)*
            T.Pow((-theta + kappa*t*theta + (theta - y)/ekt + y)/(kappa*t),1.5));
    }

    // QuantLib's LPP3HestonExpansion::z2, its powers of e^{kappa t} computed as its constructor computes them
    internal static T Z2<T>(T t, T kappa, T theta, T delta, T y, T rho)
        where T : struct, IRealArithmetic<T>
    {
        T ekt = T.Exp(kappa * t);
        T e2kt = ekt * ekt;
        T e3kt = e2kt * ekt;
        return (T.Pow(delta,2)*(8*e3kt*T.Pow(kappa,5)*T.Pow(rho,2)*T.Pow(t,4)*(2 + delta*rho*t)*
            T.Pow(theta,2)*(theta - y) - delta*T.Pow(-1 + ekt,3)*rho*
            (2*(-1 + ekt*(-5 + 24*T.Pow(rho,2)))*T.Pow(theta,3) +
            (7 + ekt*(3 + 56*T.Pow(rho,2)))*T.Pow(theta,2)*y -
            3*(1 + ekt*(-3 + 8*T.Pow(rho,2)))*theta*T.Pow(y,2) +
            2*(-1 + ekt*(-1 + 2*T.Pow(rho,2)))*T.Pow(y,3)) -
            T.Pow(-1 + ekt,2)*kappa*
            ((-4 + delta*rho*t - 8*ekt*
            (2 - 12*T.Pow(rho,2) - 4*delta*rho*t + 25*delta*T.Pow(rho,3)*t) +
            e2kt*(20 - 96*T.Pow(rho,2) + 3*delta*rho*t + 56*delta*T.Pow(rho,3)*t)
            )*T.Pow(theta,3) - 2*(-8 + 2*delta*rho*t +
            e2kt*(24 - 80*T.Pow(rho,2) - 9*delta*rho*t +
            24*delta*T.Pow(rho,3)*t) -
            4*ekt*(4 - 20*T.Pow(rho,2) - 10*delta*rho*t + 39*delta*T.Pow(rho,3)*t)
            )*T.Pow(theta,2)*y + (5*(-4 + delta*rho*t) +
            ekt*(-16 + 80*T.Pow(rho,2) + 57*delta*rho*t -
            140*delta*T.Pow(rho,3)*t) +
            2*e2kt*(18 - 40*T.Pow(rho,2) - 3*delta*rho*t +
            6*delta*T.Pow(rho,3)*t))*theta*T.Pow(y,2) +
            2*(4 + e2kt*(-4 + 8*T.Pow(rho,2)) - delta*rho*t +
            ekt*rho*(-8*rho - 7*delta*t + 14*delta*T.Pow(rho,2)*t))*T.Pow(y,3)) +
            ekt*(-1 + ekt)*T.Pow(kappa,2)*t*
            ((-24 + 128*T.Pow(rho,2) + 9*delta*rho*t - 144*delta*T.Pow(rho,3)*t -
            4*ekt*(6 - 8*T.Pow(rho,2) - 9*delta*rho*t + 6*delta*T.Pow(rho,3)*t) +
            e2kt*(48 - 160*T.Pow(rho,2) - 9*delta*rho*t +
            24*delta*T.Pow(rho,3)*t))*T.Pow(theta,3) -
            (-72 + 320*T.Pow(rho,2) + 27*delta*rho*t - 360*delta*T.Pow(rho,3)*t -
            ekt*rho*(160*rho - 81*delta*t + 348*delta*T.Pow(rho,2)*t) +
            2*e2kt*(36 - 80*T.Pow(rho,2) - 3*delta*rho*t +
            6*delta*T.Pow(rho,3)*t))*T.Pow(theta,2)*y -
            2*(32 - 128*T.Pow(rho,2) + 12*e2kt*(-1 + 2*T.Pow(rho,2)) -
            15*delta*rho*t + 144*delta*T.Pow(rho,3)*t +
            2*ekt*(-10 + 52*T.Pow(rho,2) - 13*delta*rho*t +
            58*delta*T.Pow(rho,3)*t))*theta*T.Pow(y,2) +
            4*(4 - 16*T.Pow(rho,2) - 3*delta*rho*t + 18*delta*T.Pow(rho,3)*t +
            ekt*(-4 + 16*T.Pow(rho,2) - 2*delta*rho*t + 11*delta*T.Pow(rho,3)*t))*
            T.Pow(y,3)) - 4*e2kt*T.Pow(kappa,4)*T.Pow(t,3)*theta*
            (2*e2kt*(-1 + 2*T.Pow(rho,2))*T.Pow(theta,2) +
            T.Pow(rho,2)*(4 + 13*delta*rho*t)*T.Pow(theta - y,2) +
            ekt*((-4 + 16*T.Pow(rho,2) - 2*delta*rho*t + 9*delta*T.Pow(rho,3)*t)*
            T.Pow(theta,2) + (4 - 32*T.Pow(rho,2) + 2*delta*rho*t - 19*delta*T.Pow(rho,3)*t)*
            theta*y + 4*T.Pow(rho,2)*(2 + delta*rho*t)*T.Pow(y,2))) -
            2*ekt*T.Pow(kappa,3)*T.Pow(t,2)*
            (-4*T.Pow(rho,2)*(-4 + 3*delta*rho*t)*T.Pow(theta - y,3) +
            e3kt*T.Pow(theta,2)*
            ((18 - 40*T.Pow(rho,2) - delta*rho*t + 2*delta*T.Pow(rho,3)*t)*theta +
            12*(-1 + 2*T.Pow(rho,2))*y) +
            2*ekt*((-9 + 36*T.Pow(rho,2) + 19*delta*T.Pow(rho,3)*t)*T.Pow(theta,3) +
            2*(9 - 30*T.Pow(rho,2) + 7*delta*T.Pow(rho,3)*t)*T.Pow(theta,2)*y +
            (-8 + 20*T.Pow(rho,2) + delta*rho*t - 46*delta*T.Pow(rho,3)*t)*theta*T.Pow(y,2) +
            T.Pow(rho,2)*(4 + 13*delta*rho*t)*T.Pow(y,3)) +
            e2kt*(8*theta*y*(-3*theta + 2*y) +
            delta*rho*t*theta*(7*T.Pow(theta,2) - 23*theta*y + 8*T.Pow(y,2)) -
            8*T.Pow(rho,2)*(6*T.Pow(theta,3) - 18*T.Pow(theta,2)*y + 11*theta*T.Pow(y,2) -
            T.Pow(y,3)) + 4*delta*T.Pow(rho,3)*t*
            (-13*T.Pow(theta,3) + 31*T.Pow(theta,2)*y - 14*theta*T.Pow(y,2) + T.Pow(y,3))))))/
            (64.0*T.Pow(kappa,2)*t*T.Sqrt((-theta + kappa*t*theta + (theta - y)/ekt + y)/
            (kappa*t))*T.Pow((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y,
            4));
    }

    // QuantLib's LPP3HestonExpansion::z3, its powers of e^{kappa t} computed as its constructor computes them
    internal static T Z3<T>(T t, T kappa, T theta, T delta, T y, T rho)
        where T : struct, IRealArithmetic<T>
    {
        T ekt = T.Exp(kappa * t);
        T e2kt = ekt * ekt;
        T e3kt = e2kt * ekt;
        T e4kt = e2kt * e2kt;
        return (T.Pow(delta,3)*ekt*rho*((-15*(2 + kappa*t) +
            3*e4kt*(50 - 79*kappa*t + 35*T.Pow(kappa,2)*T.Pow(t,2) -
            6*T.Pow(kappa,3)*T.Pow(t,3) +
            8*T.Pow(rho,2)*(-18 + 15*kappa*t - 6*T.Pow(kappa,2)*T.Pow(t,2) +
            T.Pow(kappa,3)*T.Pow(t,3))) +
            ekt*(-3*(20 + 86*kappa*t + 29*T.Pow(kappa,2)*T.Pow(t,2)) +
            T.Pow(rho,2)*(432 + 936*kappa*t + 552*T.Pow(kappa,2)*T.Pow(t,2) +
            92*T.Pow(kappa,3)*T.Pow(t,3))) +
            e2kt*(360 + 324*kappa*t - 261*T.Pow(kappa,2)*T.Pow(t,2) -
            48*T.Pow(kappa,3)*T.Pow(t,3) -
            4*T.Pow(rho,2)*(324 + 378*kappa*t - 12*T.Pow(kappa,2)*T.Pow(t,2) -
            2*T.Pow(kappa,3)*T.Pow(t,3) + 23*T.Pow(kappa,4)*T.Pow(t,4))) +
            e3kt*(3*(-140 + 62*kappa*t + 81*T.Pow(kappa,2)*T.Pow(t,2) -
            38*T.Pow(kappa,3)*T.Pow(t,3) + 8*T.Pow(kappa,4)*T.Pow(t,4)) +
            4*T.Pow(rho,2)*(324 + 54*kappa*t - 114*T.Pow(kappa,2)*T.Pow(t,2) +
            77*T.Pow(kappa,3)*T.Pow(t,3) - 19*T.Pow(kappa,4)*T.Pow(t,4) +
            2*T.Pow(kappa,5)*T.Pow(t,5))))*T.Pow(theta,3) +
            (15*(7 + 4*kappa*t) + 3*e4kt*
            (-79 + 70*kappa*t - 18*T.Pow(kappa,2)*T.Pow(t,2) +
            24*T.Pow(rho,2)*(5 - 4*kappa*t + T.Pow(kappa,2)*T.Pow(t,2))) -
            3*ekt*(26 - 200*kappa*t - 87*T.Pow(kappa,2)*T.Pow(t,2) +
            4*T.Pow(rho,2)*(30 + 142*kappa*t + 115*T.Pow(kappa,2)*T.Pow(t,2) +
            23*T.Pow(kappa,3)*T.Pow(t,3))) +
            2*e2kt*(3*(-66 - 195*kappa*t + 63*T.Pow(kappa,2)*T.Pow(t,2) +
            16*T.Pow(kappa,3)*T.Pow(t,3)) +
            4*T.Pow(rho,2)*(135 + 390*kappa*t - 9*T.Pow(kappa,2)*T.Pow(t,2) -
            48*T.Pow(kappa,3)*T.Pow(t,3) + 23*T.Pow(kappa,4)*T.Pow(t,4))) +
            e3kt*(606 + 300*kappa*t - 585*T.Pow(kappa,2)*T.Pow(t,2) +
            210*T.Pow(kappa,3)*T.Pow(t,3) - 24*T.Pow(kappa,4)*T.Pow(t,4) -
            4*T.Pow(rho,2)*(270 + 282*kappa*t - 345*T.Pow(kappa,2)*T.Pow(t,2) +
            153*T.Pow(kappa,3)*T.Pow(t,3) - 29*T.Pow(kappa,4)*T.Pow(t,4) +
            2*T.Pow(kappa,5)*T.Pow(t,5))))*T.Pow(theta,2)*y +
            (-93 - 75*kappa*t + 3*e4kt*
            (35 - 18*kappa*t + 24*T.Pow(rho,2)*(-2 + kappa*t)) +
            3*ekt*(58 - 123*kappa*t - 86*T.Pow(kappa,2)*T.Pow(t,2) +
            4*T.Pow(rho,2)*(12 + 80*kappa*t + 92*T.Pow(kappa,2)*T.Pow(t,2) +
            23*T.Pow(kappa,3)*T.Pow(t,3))) +
            e3kt*(-3*(74 + 137*kappa*t - 100*T.Pow(kappa,2)*T.Pow(t,2) +
            16*T.Pow(kappa,3)*T.Pow(t,3)) -
            16*T.Pow(rho,2)*(-27 - 51*kappa*t + 45*T.Pow(kappa,2)*T.Pow(t,2) -
            12*T.Pow(kappa,3)*T.Pow(t,3) + T.Pow(kappa,4)*T.Pow(t,4))) +
            e2kt*(36 + 909*kappa*t - 42*T.Pow(kappa,2)*T.Pow(t,2) -
            60*T.Pow(kappa,3)*T.Pow(t,3) -
            4*T.Pow(rho,2)*(108 + 462*kappa*t + 96*T.Pow(kappa,2)*T.Pow(t,2) -
            117*T.Pow(kappa,3)*T.Pow(t,3) + 23*T.Pow(kappa,4)*T.Pow(t,4))))*theta*T.Pow(y,2)
            + 2*(9 + 3*e4kt*(-3 + 4*T.Pow(rho,2)) + 15*kappa*t +
            e2kt*(-3*kappa*t*(33 + 10*kappa*t) +
            T.Pow(rho,2)*(36 + 192*kappa*t + 96*T.Pow(kappa,2)*T.Pow(t,2) -
            46*T.Pow(kappa,3)*T.Pow(t,3))) +
            e3kt*(18 + 57*kappa*t - 12*T.Pow(kappa,2)*T.Pow(t,2) -
            2*T.Pow(rho,2)*(18 + 48*kappa*t - 21*T.Pow(kappa,2)*T.Pow(t,2) +
            2*T.Pow(kappa,3)*T.Pow(t,3))) +
            ekt*(3*(-6 + 9*kappa*t + 14*T.Pow(kappa,2)*T.Pow(t,2)) -
            2*T.Pow(rho,2)*(6 + 48*kappa*t + 69*T.Pow(kappa,2)*T.Pow(t,2) +
            23*T.Pow(kappa,3)*T.Pow(t,3))))*T.Pow(y,3)))/
            (96.0*kappa*t*T.Sqrt((-theta + kappa*t*theta + (theta - y)/ekt + y)/(kappa*t))*
            T.Pow((1 + ekt*(-1 + kappa*t))*theta + (-1 + ekt)*y,5));
    }

    // z3's limit as the term falls to 0: rho (8 rho^2 - 5) sigma^3 / (96 v0^{5/2}) (HR7 (xxxii))
    private static double Z3Limit(double sigma, double v0, double rho) =>
        rho * ((8.0 * rho * rho) - 5.0) * sigma * sigma * sigma / (96.0 * v0 * v0 * Math.Sqrt(v0));

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

    // z3 as a function of kappa, its other inputs fixed
    private readonly struct Z3Expression(double t, double theta, double delta, double y, double rho) : IKappaTExpression
    {
        public T Evaluate<T>(T kappa)
            where T : struct, IRealArithmetic<T> =>
            Z3(T.Of(t), kappa, T.Of(theta), T.Of(delta), T.Of(y), T.Of(rho));
    }
}
