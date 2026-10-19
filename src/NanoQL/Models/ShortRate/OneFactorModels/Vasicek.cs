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
        double a = A();
        if (Math.Abs(maturity) < NumericLimits.QL_EPSILON)
        {
            v = 0.0;
        }
        else if (a < Math.Sqrt(NumericLimits.QL_EPSILON))
        {
            v = Sigma * B(maturity, bondMaturity) * Math.Sqrt(maturity);
        }
        else
        {
            v = Sigma * B(maturity, bondMaturity) * Math.Sqrt(0.5 * (1.0 - Math.Exp(-2.0 * a * maturity)) / a);
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
        double a = A();
        if (a < Math.Sqrt(NumericLimits.QL_EPSILON))
        {
            double sigma2 = Sigma * Sigma;
            double tau = T - t;
            return Math.Exp((-0.5 * Lambda * Sigma * tau * tau) + (sigma2 * tau * tau * tau / 6.0));
        }
        else
        {
            double sigma2 = Sigma * Sigma;
            double bt = B(t, T);
            return Math.Exp(((B() + (Lambda * Sigma / a) - (0.5 * sigma2 / (a * a))) * (bt - (T - t)))
                            - (0.25 * sigma2 * bt * bt / a));
        }
    }

    /// <summary>The affine discount bond's B(t, T): (1 − exp(−a(T − t)))/a, or T − t for a near 0.</summary>
    /// <param name="t">The time.</param>
    /// <param name="T">The bond's maturity.</param>
    /// <returns>B(t, T).</returns>
    protected override double B(double t, double T)
    {
        double a = A();
        if (a < Math.Sqrt(NumericLimits.QL_EPSILON))
        {
            return T - t;
        }
        else
        {
            return (1.0 - Math.Exp(-a * (T - t))) / a;
        }
    }

    // The short-rate dynamics: r = x + b, the state variable an Ornstein-Uhlenbeck process.
    private sealed class VasicekDynamics(double a, double b, double sigma, double r0)
        : OneFactorModelShortRateDynamics(new OrnsteinUhlenbeckProcess(a, sigma, r0 - b))
    {
        public override double Variable(double t, double r) => r - b;

        public override double ShortRate(double t, double variable) => variable + b;
    }
}
