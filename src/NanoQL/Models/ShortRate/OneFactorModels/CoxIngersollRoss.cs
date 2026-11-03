// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2021 Magnus Mencke
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodels/coxingersollross.hpp ql/models/shortrate/onefactormodels/coxingersollross.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Models;

/// <summary>The Cox-Ingersoll-Ross model: dr(t) = k(θ − r(t))dt + σ√r(t) dW(t).</summary>
/// <remarks>
/// <para>
/// Its arguments are θ, k, σ and r₀, in that order, each a constant parameter, positive. QuantLib's header warns that
/// the class "was not tested enough to guarantee its functionality". Its dynamics simulate the short rate directly,
/// on <see cref="CoxIngersollRossProcess"/>'s quadratic-exponential scheme.
/// </para>
/// <para>
/// <b>The Feller constraint</b> (K1): with it, the model's parameters satisfy σ² &lt; 2kθ at each vector's own k and θ,
/// through <see cref="CalibratedModel.ParametersConstraint"/>, and σ's own constraint is positivity: a divergence,
/// QuantLib's testing σ against the constructor's k and θ (<c>coxingersollross.cpp:24-37</c>), so a calibration can
/// leave the Feller region and σ stops at a bound the constructor set (<c>Divergences/CirFellerConstraintTests.cs</c>).
/// The constructor refuses a σ outside it with QuantLib's text.
/// </para>
/// <para>
/// <b>The tree</b> (K2): on QuantLib's dynamics before its 1.23, the square root y = √r of the short rate, of constant
/// volatility σ/2, Euler-discretized, the short rate y², which the trinomial tree's state-independent variance fits:
/// a divergence, QuantLib's tree reading <see cref="CoxIngersollRossProcess"/>, whose variance depends on the state,
/// at its initial value, so that its prices stall away from its own analytic ones
/// (<c>Divergences/CirTreeConvergenceTests.cs</c>).
/// </para>
/// <para>An extension point, as in QuantLib: the extended Cox-Ingersoll-Ross model derives from it.</para>
/// </remarks>
public class CoxIngersollRoss : OneFactorAffineModel
{
    private readonly bool withFellerConstraint;

    /// <summary>Initializes a new instance of the <see cref="CoxIngersollRoss"/> class.</summary>
    /// <param name="r0">The initial short rate, positive.</param>
    /// <param name="theta">The level θ, positive.</param>
    /// <param name="k">The speed k, positive.</param>
    /// <param name="sigma">The volatility σ, positive, and with the Feller constraint below √(2kθ).</param>
    /// <param name="withFellerConstraint">Whether the parameters satisfy the Feller condition σ² &lt; 2kθ.</param>
    /// <exception cref="ArgumentException">A parameter is outside its constraint.</exception>
    public CoxIngersollRoss(
        double r0 = 0.05, double theta = 0.1, double k = 0.1, double sigma = 0.1, bool withFellerConstraint = true)
        : base(4)
    {
        this.withFellerConstraint = withFellerConstraint;
        SetArgument(0, new ConstantParameter(theta, new PositiveConstraint(), nameof(theta)));
        SetArgument(1, new ConstantParameter(k, new PositiveConstraint(), nameof(k)));
        SetArgument(2, new ConstantParameter(sigma, new PositiveConstraint(), nameof(sigma)));

        // QuantLib's VolatilityConstraint(k, theta) on sigma, at construction
        if (withFellerConstraint && !(sigma * sigma < 2.0 * k * theta))
        {
            throw new ArgumentException($"{sigma.ToString(CultureInfo.InvariantCulture)}: invalid value", nameof(sigma));
        }

        SetArgument(3, new ConstantParameter(r0, new PositiveConstraint(), nameof(r0)));
    }

    /// <summary>Gets the level θ.</summary>
    protected double Theta => Arguments[0].Evaluate(0.0);

    /// <summary>Gets the speed k.</summary>
    protected double K => Arguments[1].Evaluate(0.0);

    /// <summary>Gets the volatility σ.</summary>
    protected double Sigma => Arguments[2].Evaluate(0.0);

    /// <summary>Gets the initial short rate.</summary>
    protected double X0 => Arguments[3].Evaluate(0.0);

    /// <summary>The price of an option on a discount bond, by the non-central χ² distribution.</summary>
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

        double discountT = DiscountBond(0.0, t, X0);
        double discountS = DiscountBond(0.0, s, X0);

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
        double b = B(t, s);

        double rho = 2.0 * h / (sigma2 * (Math.Exp(h * t) - 1.0));
        double psi = (K + h) / sigma2;

        double df = 4.0 * K * Theta / sigma2;
        double ncps = 2.0 * rho * rho * X0 * Math.Exp(h * t) / (rho + psi + b);
        double ncpt = 2.0 * rho * rho * X0 * Math.Exp(h * t) / (rho + psi);

        var chis = new NonCentralCumulativeChiSquareDistribution(df, ncps);
        var chit = new NonCentralCumulativeChiSquareDistribution(df, ncpt);

        double z = Math.Log(A(t, s) / strike) / b;
        double call = (discountS * chis.Evaluate(2.0 * z * (rho + psi + b))) - (strike * discountT * chit.Evaluate(2.0 * z * (rho + psi)));

        return type == OptionType.Call ? call : call - discountS + (strike * discountT);
    }

    /// <summary>The model's short-rate dynamics: the short rate itself, a <see cref="CoxIngersollRossProcess"/>.</summary>
    /// <returns>A new dynamics object.</returns>
    public override OneFactorModelShortRateDynamics Dynamics() => new CirDynamics(Theta, K, Sigma, X0);

    /// <summary>Builds the model's trinomial tree on a time grid, on the square root of the short rate.</summary>
    /// <param name="grid">The time grid.</param>
    /// <returns>The tree, an unfitted <see cref="OneFactorModelShortRateTree"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
    public override Lattice Tree(TimeGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        OneFactorModelShortRateDynamics treeDynamics = TreeDynamics();
        var trinomial = new TrinomialTree(treeDynamics.Process, grid, true);
        return new OneFactorModelShortRateTree(trinomial, treeDynamics, grid);
    }

    // std::max<Real>(x, 0.0): x < 0.0 ? 0.0 : x, a NaN kept.
    private static double Floor0(double x) => x < 0.0 ? 0.0 : x;

    /// <summary>The square-root dynamics the tree is built on: y = √r, the short rate y².</summary>
    /// <returns>A new dynamics object.</returns>
    private protected virtual OneFactorModelShortRateDynamics TreeDynamics() => new SquareRootDynamics(Theta, K, Sigma, X0);

    /// <summary>The Feller condition σ² &lt; 2kθ at the vector's own k and θ, when the model was built with it.</summary>
    /// <returns>The constraint, or <see langword="null"/> without the Feller constraint.</returns>
    protected override Constraint? ParametersConstraint() => withFellerConstraint ? new FellerConstraint() : null;

    /// <summary>The affine discount bond's A(t, T).</summary>
    /// <param name="t">The time.</param>
    /// <param name="T">The bond's maturity.</param>
    /// <returns>A(t, T).</returns>
    protected override double A(double t, double T)
    {
        double sigma2 = Sigma * Sigma;
        double h = Math.Sqrt((K * K) + (2.0 * sigma2));
        double numerator = 2.0 * h * Math.Exp(0.5 * (K + h) * (T - t));
        double denominator = (2.0 * h) + ((K + h) * (Math.Exp((T - t) * h) - 1.0));
        double value = Math.Log(numerator / denominator) * 2.0 * K * Theta / sigma2;
        return Math.Exp(value);
    }

    /// <summary>The affine discount bond's B(t, T).</summary>
    /// <param name="t">The time.</param>
    /// <param name="T">The bond's maturity.</param>
    /// <returns>B(t, T).</returns>
    protected override double B(double t, double T)
    {
        double h = Math.Sqrt((K * K) + (2.0 * Sigma * Sigma));
        double temp = Math.Exp((T - t) * h) - 1.0;
        double numerator = 2.0 * temp;
        double denominator = (2.0 * h) + ((K + h) * temp);
        double value = numerator / denominator;
        return value;
    }

    // The dynamics: the short rate simulated directly by the quadratic-exponential scheme.
    private sealed class CirDynamics(double theta, double k, double sigma, double x0)
        : OneFactorModelShortRateDynamics(new CoxIngersollRossProcess(k, sigma, x0, theta))
    {
        public override double Variable(double t, double r) => r;

        public override double ShortRate(double t, double variable) => variable;
    }

    // QuantLib 1.22's dynamics: the state y = sqrt(r), dy = [(k*theta/2 - sigma^2/8)/y - k*y/2]dt + sigma/2 dW.
    private sealed class SquareRootDynamics(double theta, double k, double sigma, double x0)
        : OneFactorModelShortRateDynamics(new HelperProcess(theta, k, sigma, Math.Sqrt(x0)))
    {
        public override double Variable(double t, double r) => Math.Sqrt(r);

        public override double ShortRate(double t, double variable) => variable * variable;
    }

    // The Feller condition on the parameters [theta, k, sigma, r0]: QuantLib's VolatilityConstraint built at the
    // vector's own k and theta, without its sigma > 0, which sigma's own constraint tests.
    private sealed class FellerConstraint : Constraint
    {
        public override bool Test(Vector p)
        {
            ArgumentNullException.ThrowIfNull(p);
            double theta = p[0], k = p[1], sigma = p[2];
            return sigma * sigma < 2.0 * k * theta;
        }
    }

    /// <summary>
    /// QuantLib 1.22's <c>CoxIngersollRoss::HelperProcess</c>: the square root y of the short rate, of drift
    /// (kθ/2 − σ²/8)/y − ky/2 and diffusion σ/2, Euler-discretized. The trees of the Cox-Ingersoll-Ross models read it.
    /// </summary>
    private protected sealed class HelperProcess : StochasticProcess1D
    {
        private readonly double y0, theta, k, sigma;

        /// <summary>Initializes a new instance of the <see cref="HelperProcess"/> class.</summary>
        /// <param name="theta">The level θ.</param>
        /// <param name="k">The speed k.</param>
        /// <param name="sigma">The volatility σ.</param>
        /// <param name="y0">The initial square root of the short rate.</param>
        public HelperProcess(double theta, double k, double sigma, double y0)
            : base(new EulerDiscretization())
        {
            this.y0 = y0;
            this.theta = theta;
            this.k = k;
            this.sigma = sigma;
        }

        /// <inheritdoc/>
        public override double X0 => y0;

        /// <inheritdoc/>
        public override double Drift(double t, double y) => (((0.5 * theta * k) - (0.125 * sigma * sigma)) / y) - (0.5 * k * y);

        /// <inheritdoc/>
        public override double Diffusion(double t, double x) => 0.5 * sigma;
    }
}
