// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2021 Magnus Mencke
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodels/extendedcoxingersollross.hpp ql/models/shortrate/onefactormodels/extendedcoxingersollross.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>
/// The extended Cox-Ingersoll-Ross model: r(t) = φ(t) + y(t), y a standard Cox-Ingersoll-Ross process,
/// dy(t) = k(θ − y(t))dt + σ√y(t) dW(t), and φ the deterministic parameter fitting the curve.
/// </summary>
/// <remarks>
/// <para>
/// φ is analytical in <see cref="Dynamics"/>, φ(t) = f(t) − 2kθ(e^(th) − 1)/(2h + (k + h)(e^(th) − 1))
/// − 4x₀h²e^(th)/(2h + (k + h)(e^(th) − 1))², f the curve's instantaneous forward and h = √(k² + 2σ²), and numerical in
/// <see cref="Tree"/>, whose tree fits it to the curve at each step as it is built. QuantLib's header warns that the
/// class "was not tested enough to guarantee its functionality".
/// </para>
/// <para>
/// As <see cref="CoxIngersollRoss"/>'s, its Feller constraint reads each parameter vector's own k and θ, and its tree
/// is on the square root of the process, y = √(r − φ(t)), the short rate y² + φ(t).
/// </para>
/// </remarks>
public sealed class ExtendedCoxIngersollRoss : CoxIngersollRoss, ITermStructureConsistentModel
{
    // Written by GenerateArguments, on a model under construction or a fresh copy only.
    private Parameter phi;

    /// <summary>Initializes a new instance of the <see cref="ExtendedCoxIngersollRoss"/> class.</summary>
    /// <param name="termStructure">The curve the model fits.</param>
    /// <param name="theta">The level θ, positive.</param>
    /// <param name="k">The speed k, positive.</param>
    /// <param name="sigma">The volatility σ, positive, and with the Feller constraint below √(2kθ).</param>
    /// <param name="x0">The process's initial value, positive.</param>
    /// <param name="withFellerConstraint">Whether the parameters satisfy the Feller condition σ² &lt; 2kθ.</param>
    /// <exception cref="ArgumentException">A parameter is outside its constraint.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="termStructure"/> is null.</exception>
    public ExtendedCoxIngersollRoss(
        YieldTermStructure termStructure,
        double theta = 0.1,
        double k = 0.1,
        double sigma = 0.1,
        double x0 = 0.05,
        bool withFellerConstraint = true)
        : base(x0, theta, k, sigma, withFellerConstraint)
    {
        ArgumentNullException.ThrowIfNull(termStructure);
        TermStructure = termStructure;
        GenerateArguments();
    }

    /// <summary>Gets the curve the model fits.</summary>
    public YieldTermStructure TermStructure { get; }

    /// <summary>Builds the model's trinomial tree on a time grid, fitted to the curve at each step as it is built.</summary>
    /// <param name="grid">The time grid.</param>
    /// <returns>The fitted <see cref="OneFactorModelShortRateTree"/>, immutable once returned.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
    /// <exception cref="NanoQLException">A step's fitting value can't be solved.</exception>
    public override Lattice Tree(TimeGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var fitting = new TermStructureFittingParameter(TermStructure);
        var numericDynamics = new SquareRootExtendedDynamics(fitting, Theta, K, Sigma, X0);

        var trinomial = new TrinomialTree(numericDynamics.Process, grid, true);

        var impl = (TermStructureFittingParameter.NumericalImpl)fitting.Implementation();

        return new OneFactorModelShortRateTree(trinomial, numericDynamics, impl, grid);
    }

    /// <summary>The model's short-rate dynamics, with the analytical fitting parameter: r = y + φ(t).</summary>
    /// <returns>A new dynamics object.</returns>
    public override OneFactorModelShortRateDynamics Dynamics() => new ExtendedDynamics(phi, Theta, K, Sigma, X0);

    /// <summary>The price of an option on a discount bond, by the non-central χ² distribution on the curve.</summary>
    /// <param name="type">The option's type.</param>
    /// <param name="strike">The strike, positive.</param>
    /// <param name="maturity">The option's maturity.</param>
    /// <param name="bondMaturity">The bond's maturity.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentException"><paramref name="strike"/> isn't positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public override double DiscountBondOption(OptionType type, double strike, double maturity, double bondMaturity)
    {
        double t = maturity, s = bondMaturity;
        if (!(strike > 0.0))
        {
            throw new ArgumentException("strike must be positive", nameof(strike));
        }

        double discountT = TermStructure.Discount(t);
        double discountS = TermStructure.Discount(s);
        if (t < NumericLimits.QL_EPSILON)
        {
            return type switch
            {
                OptionType.Call => Floor0(discountS - strike),
                OptionType.Put => Floor0(strike - discountS),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, "unsupported option type"),
            };
        }

        double sigma2 = Sigma * Sigma;
        double h = Math.Sqrt((K * K) + (2.0 * sigma2));
        double r0 = TermStructure.ForwardRate(0.0, 0.0, Compounding.Continuous, Frequency.NoFrequency).Rate;
        double b = B(t, s);

        double rho = 2.0 * h / (sigma2 * (Math.Exp(h * t) - 1.0));
        double psi = (K + h) / sigma2;

        double df = 4.0 * K * Theta / sigma2;
        double ncps = 2.0 * rho * rho * (r0 - phi.Evaluate(0.0)) * Math.Exp(h * t) / (rho + psi + b);
        double ncpt = 2.0 * rho * rho * (r0 - phi.Evaluate(0.0)) * Math.Exp(h * t) / (rho + psi);

        var chis = new NonCentralCumulativeChiSquareDistribution(df, ncps);
        var chit = new NonCentralCumulativeChiSquareDistribution(df, ncpt);

        double discountShift = discountT * base.A(0.0, s) * Math.Exp(-B(0.0, s) * X0)
            / (discountS * base.A(0.0, t) * Math.Exp(-B(0.0, t) * X0));

        double z = (Math.Log(base.A(t, s) / strike) - Math.Log(discountShift)) / b;
        double call = (discountS * chis.Evaluate(2.0 * z * (rho + psi + b))) - (strike * discountT * chit.Evaluate(2.0 * z * (rho + psi)));
        return type == OptionType.Call ? call : call - discountS + (strike * discountT);
    }

    /// <summary>The analytical fitting parameter, from the curve and the parameters.</summary>
    [MemberNotNull(nameof(phi))]
    protected override void GenerateArguments() =>
        phi = new FittingParameter(TermStructure, Theta, K, Sigma, X0);

    /// <summary>The affine discount bond's A(t, T), fitted to the curve.</summary>
    /// <param name="t">The time.</param>
    /// <param name="T">The bond's maturity.</param>
    /// <returns>A(t, T).</returns>
    protected override double A(double t, double T)
    {
        double s = T;
        double pt = TermStructure.Discount(t);
        double ps = TermStructure.Discount(s);
        double value = base.A(t, s) * Math.Exp(B(t, s) * phi.Evaluate(t))
            * (ps * base.A(0.0, t) * Math.Exp(-B(0.0, t) * X0))
            / (pt * base.A(0.0, s) * Math.Exp(-B(0.0, s) * X0));
        return value;
    }

    // std::max<Real>(x, 0.0): x < 0.0 ? 0.0 : x, a NaN kept.
    private static double Floor0(double x) => x < 0.0 ? 0.0 : x;

    // The dynamics: r = y + phi(t), y simulated by the quadratic-exponential scheme.
    private sealed class ExtendedDynamics(Parameter fitting, double theta, double k, double sigma, double x0)
        : OneFactorModelShortRateDynamics(new Processes.CoxIngersollRossProcess(k, sigma, x0, theta))
    {
        public override double Variable(double t, double r) => r - fitting.Evaluate(t);

        public override double ShortRate(double t, double variable) => variable + fitting.Evaluate(t);
    }

    // QuantLib 1.22's dynamics for the tree: y = sqrt(r - phi(t)), r = y^2 + phi(t).
    private sealed class SquareRootExtendedDynamics(Parameter fitting, double theta, double k, double sigma, double x0)
        : OneFactorModelShortRateDynamics(new HelperProcess(theta, k, sigma, Math.Sqrt(x0)))
    {
        public override double Variable(double t, double r) => Math.Sqrt(r - fitting.Evaluate(t));

        public override double ShortRate(double t, double variable) => (variable * variable) + fitting.Evaluate(t);
    }

    // The analytical term-structure fitting parameter phi(t).
    private sealed class FittingParameter(YieldTermStructure termStructure, double theta, double k, double sigma, double x0)
        : TermStructureFittingParameter(new FittingImpl(termStructure, theta, k, sigma, x0))
    {
        private sealed class FittingImpl(YieldTermStructure termStructure, double theta, double k, double sigma, double x0) : Impl
        {
            public override double Value(ImmutableArray<double> parameters, double t)
            {
                double forwardRate = termStructure.ForwardRate(t, t, Compounding.Continuous, Frequency.NoFrequency).Rate;
                double h = Math.Sqrt((k * k) + (2.0 * sigma * sigma));
                double expth = Math.Exp(t * h);
                double temp = (2.0 * h) + ((k + h) * (expth - 1.0));
                double phi = forwardRate - (2.0 * k * theta * (expth - 1.0) / temp) - (x0 * 4.0 * h * h * expth / (temp * temp));
                return phi;
            }
        }
    }
}
