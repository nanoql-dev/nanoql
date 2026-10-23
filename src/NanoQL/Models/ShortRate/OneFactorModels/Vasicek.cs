// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodels/vasicek.hpp ql/models/shortrate/onefactormodels/vasicek.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;

namespace NanoQL.Models;

/// <summary>The Vasicek model: drₜ = a(b − rₜ)dt + σ dWₜ, with constant a, b and σ, and a risk premium λ.</summary>
/// <remarks>
/// <para>
/// Its arguments are a (positive), b, σ (positive) and λ, in that order, each a constant parameter. QuantLib's
/// <c>a()</c> and <c>b()</c> share their names with the affine <c>A(t, T)</c> and <c>B(t, T)</c>, so all four are
/// methods.
/// </para>
/// <para>
/// A(t, T), B(t, T) and the bond option's variance are QuantLib's master's forms after the pin (#2882, the S-sr plan's
/// SSR3), free of cancellation at small a and exact in the limit a → 0, where QuantLib 1.44 cuts to the a = 0 forms
/// below a = √ε and cancels above it. They read <c>std::expm1</c> (nanoql's own), so their last bits differ from 1.44's
/// at every a.
/// </para>
/// <para>An extension point, as in QuantLib: Hull-White derives from it.</para>
/// </remarks>
public class Vasicek : OneFactorAffineModel
{
    /// <summary>Initializes a new instance of the <see cref="Vasicek"/> class.</summary>
    /// <param name="r0">The initial short rate.</param>
    /// <param name="a">The mean-reversion speed, positive.</param>
    /// <param name="b">The mean-reversion level.</param>
    /// <param name="sigma">The volatility, positive.</param>
    /// <param name="lambda">The risk premium.</param>
    /// <exception cref="ArgumentException"><paramref name="a"/> or <paramref name="sigma"/> isn't positive.</exception>
    public Vasicek(double r0 = 0.05, double a = 0.1, double b = 0.05, double sigma = 0.01, double lambda = 0.0)
        : base(4)
    {
        R0 = r0;
        SetArgument(0, new ConstantParameter(a, new PositiveConstraint(), nameof(a)));
        SetArgument(1, new ConstantParameter(b, new NoConstraint(), nameof(b)));
        SetArgument(2, new ConstantParameter(sigma, new PositiveConstraint(), nameof(sigma)));
        SetArgument(3, new ConstantParameter(lambda, new NoConstraint(), nameof(lambda)));
    }

    /// <summary>Gets the volatility σ.</summary>
    public double Sigma => Arguments[2].Evaluate(0.0);

    /// <summary>Gets the risk premium λ.</summary>
    public double Lambda => Arguments[3].Evaluate(0.0);

    /// <summary>
    /// Gets the initial short rate, which a derived model may compute in its <see cref="CalibratedModel.GenerateArguments"/>:
    /// its setter is for a model under construction or a fresh copy only, as <see cref="CalibratedModel"/>'s writes are.
    /// </summary>
    public double R0 { get; protected set; }

    /// <summary>The mean-reversion speed a.</summary>
    /// <returns>a.</returns>
    public double A() => Arguments[0].Evaluate(0.0);

    /// <summary>The mean-reversion level b.</summary>
    /// <returns>b.</returns>
    public double B() => Arguments[1].Evaluate(0.0);

    /// <summary>The model's short-rate dynamics: an Ornstein-Uhlenbeck process of r − b.</summary>
    /// <returns>A new dynamics object.</returns>
    public override OneFactorModelShortRateDynamics Dynamics() => new VasicekDynamics(A(), B(), Sigma, R0);

    /// <summary>The price of an option on a discount bond, by Black's formula on the bond's forward price.</summary>
    /// <param name="type">The option's type.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="maturity">The option's maturity.</param>
    /// <param name="bondMaturity">The bond's maturity.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public override double DiscountBondOption(OptionType type, double strike, double maturity, double bondMaturity)
    {
        double v;
        if (Math.Abs(maturity) < NumericLimits.QL_EPSILON)
        {
            v = 0.0;
        }
        else
        {
            // 0.5*(1 - exp(-2*a*maturity))/a
            double variance = maturity * OneMinusExpOverX(2.0 * A() * maturity);
            v = Sigma * B(maturity, bondMaturity) * Math.Sqrt(variance);
        }

        double f = DiscountBond(0.0, bondMaturity, R0);
        double k = DiscountBond(0.0, maturity, R0) * strike;

        return BlackFormulaFunctions.BlackFormula(type, k, f, v);
    }

    /// <summary>The affine discount bond's A(t, T).</summary>
    /// <param name="t">The time.</param>
    /// <param name="T">The bond's maturity.</param>
    /// <returns>A(t, T).</returns>
    protected override double A(double t, double T)
    {
        // A(t,T) = exp((b + lambda*sigma/a - sigma^2/(2a^2))*(B - tau)
        //               - sigma^2*B^2/(4a))
        // rewritten as exp(-(a*b + lambda*sigma)*tau^2*phi(a*tau)
        //                  + sigma^2*tau^3*h(a*tau)/4),
        // which is free of cancellation and has the correct limit
        // for a -> 0.
        double tau = T - t;
        double sigma2 = Sigma * Sigma;
        (double phi, double h) = PhiAndH(A() * tau);
        return Math.Exp((-((A() * B()) + (Lambda * Sigma)) * tau * tau * phi) + (0.25 * sigma2 * tau * tau * tau * h));
    }

    /// <summary>The affine discount bond's B(t, T): (1 − exp(−a(T − t)))/a, written as τ·(1 − e^(−aτ))/(aτ), τ = T − t.</summary>
    /// <param name="t">The time.</param>
    /// <param name="T">The bond's maturity.</param>
    /// <returns>B(t, T).</returns>
    protected override double B(double t, double T)
    {
        double tau = T - t;
        return tau * OneMinusExpOverX(A() * tau);
    }

    /// <summary>(1 − exp(−x))/x, accurate for small x as well, and 1 at 0: QuantLib's master's <c>oneMinusExpOverX</c>.</summary>
    /// <param name="x">The argument.</param>
    /// <returns>(1 − exp(−x))/x.</returns>
    private protected static double OneMinusExpOverX(double x) => x == 0.0 ? 1.0 : -Expm1Functions.StdExpm1(-x) / x;

    // The two functions
    //   phi(x) = (exp(-x) - 1 + x)/x^2
    //   h(x)   = (4 exp(-x) - exp(-2x) - 3 + 2x)/x^3
    // appear in the exponent of A. Their closed forms cancel
    // catastrophically for small x, so we use the Taylor series
    //   phi(x) = sum_k (k+3) p_k
    //   h(x)   = sum_k (2^(k+3) - 4) p_k
    // with p_k = (-x)^k/(k+3)!, which converges quickly and
    // without significant cancellation for |x| < 1.
    private static (double Phi, double H) PhiAndH(double x)
    {
        double phi, h;
        if (Math.Abs(x) < 1.0)
        {
            double p = 1.0 / 6.0;
            double twoPower = 8.0;
            phi = h = 0.0;
            for (int k = 0; k < 40; ++k)
            {
                double hTerm = (twoPower - 4.0) * p;
                phi += (k + 3.0) * p;
                h += hTerm;
                if (Math.Abs(hTerm) <= NumericLimits.QL_EPSILON * Math.Abs(h))
                {
                    break;
                }

                p *= -x / (k + 4.0);
                twoPower *= 2.0;
            }
        }
        else
        {
            double e1 = Expm1Functions.StdExpm1(-x);
            double e2 = Expm1Functions.StdExpm1(-2.0 * x);
            phi = (e1 + x) / (x * x);
            h = ((4.0 * e1) - e2 + (2.0 * x)) / (x * x * x);
        }

        return (phi, h);
    }

    // The short-rate dynamics: r = x + b, the state variable an Ornstein-Uhlenbeck process.
    private sealed class VasicekDynamics(double a, double b, double sigma, double r0)
        : OneFactorModelShortRateDynamics(new OrnsteinUhlenbeckProcess(a, sigma, r0 - b))
    {
        public override double Variable(double t, double r) => r - b;

        public override double ShortRate(double t, double variable) => variable + b;
    }
}
