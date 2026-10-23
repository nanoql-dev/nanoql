// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004 Mike Parker
// SPDX-FileCopyrightText: Copyright (C) 2021 Magnus Mencke
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/twofactormodels/g2.hpp ql/models/shortrate/twofactormodels/g2.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>
/// The two-additive-factor Gaussian model: rₜ = φ(t) + xₜ + yₜ, with dxₜ = −a xₜ dt + σ dW¹ₜ, x₀ = 0,
/// dyₜ = −b yₜ dt + η dW²ₜ, y₀ = 0, and dW¹ₜdW²ₜ = ρ dt, fitted to a yield curve.
/// </summary>
/// <remarks>
/// <para>
/// Its arguments are a, σ, b and η (positive) and ρ (in [−1, 1]), in that order, each a constant parameter;
/// φ is the analytical fitting parameter, φ(t) = f(t) + ½(σ(1 − e^(−at))/a)² + ½(η(1 − e^(−bt))/b)²
/// + ρ(σ(1 − e^(−at))/a)(η(1 − e^(−bt))/b), f the curve's instantaneous forward. As in <see cref="Vasicek"/>,
/// QuantLib's <c>a()</c> and <c>b()</c> share their names with B(x, t), so they are methods. QuantLib's header warns
/// that the class "was not tested enough to guarantee its functionality".
/// </para>
/// <para>
/// V(t) and B(x, t) are QuantLib's master's forms after the pin (#2885, the S-sr plan's SSR3), free of cancellation at
/// small mean reversion; the members QuantLib leaves, the bond option's σ_P, the fitting parameter and the swaption
/// function's means, standard deviations and correlation, are written in the same forms (SSR6 (iii)), exact in the limit
/// a, b → 0, so their last bits differ from 1.44's at every reversion.
/// </para>
/// </remarks>
public sealed class G2 : TwoFactorModel, IAffineModel, ITermStructureConsistentModel
{
    // Written by GenerateArguments, on a model under construction or a fresh copy only.
    private Parameter phi;

    /// <summary>Initializes a new instance of the <see cref="G2"/> class.</summary>
    /// <param name="termStructure">The curve the model fits.</param>
    /// <param name="a">The first factor's mean reversion, positive.</param>
    /// <param name="sigma">The first factor's volatility, positive.</param>
    /// <param name="b">The second factor's mean reversion, positive.</param>
    /// <param name="eta">The second factor's volatility, positive.</param>
    /// <param name="rho">The factors' correlation, in [−1, 1].</param>
    /// <exception cref="ArgumentNullException"><paramref name="termStructure"/> is null.</exception>
    /// <exception cref="ArgumentException">A parameter is outside its constraint.</exception>
    public G2(
        YieldTermStructure termStructure, double a = 0.1, double sigma = 0.01, double b = 0.1, double eta = 0.01, double rho = -0.75)
        : base(5)
    {
        ArgumentNullException.ThrowIfNull(termStructure);
        TermStructure = termStructure;
        SetArgument(0, new ConstantParameter(a, new PositiveConstraint(), nameof(a)));
        SetArgument(1, new ConstantParameter(sigma, new PositiveConstraint(), nameof(sigma)));
        SetArgument(2, new ConstantParameter(b, new PositiveConstraint(), nameof(b)));
        SetArgument(3, new ConstantParameter(eta, new PositiveConstraint(), nameof(eta)));
        SetArgument(4, new ConstantParameter(rho, new BoundaryConstraint(-1.0, 1.0), nameof(rho)));
        GenerateArguments();
    }

    /// <summary>Gets the curve the model fits.</summary>
    public YieldTermStructure TermStructure { get; }

    /// <summary>Gets the first factor's volatility σ.</summary>
    public double Sigma => Arguments[1].Evaluate(0.0);

    /// <summary>Gets the second factor's volatility η.</summary>
    public double Eta => Arguments[3].Evaluate(0.0);

    /// <summary>Gets the factors' correlation ρ.</summary>
    public double Rho => Arguments[4].Evaluate(0.0);

    /// <summary>The first factor's mean reversion a.</summary>
    /// <returns>a.</returns>
    public double A() => Arguments[0].Evaluate(0.0);

    /// <summary>The second factor's mean reversion b.</summary>
    /// <returns>b.</returns>
    public double B() => Arguments[2].Evaluate(0.0);

    /// <summary>The model's short-rate dynamics: r = φ(t) + x + y, x and y Ornstein-Uhlenbeck processes.</summary>
    /// <returns>A new dynamics object.</returns>
    public override TwoFactorModelShortRateDynamics Dynamics() => new G2Dynamics(phi, A(), Sigma, B(), Eta, Rho);

    /// <summary>The price at a time of a unit discount bond maturing later, given the two factors then.</summary>
    /// <param name="now">The time.</param>
    /// <param name="maturity">The bond's maturity.</param>
    /// <param name="factors">The factors' values at <paramref name="now"/>, at least two.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factors"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="factors"/> holds fewer than two values.</exception>
    public double DiscountBond(double now, double maturity, Vector factors)
    {
        ArgumentNullException.ThrowIfNull(factors);
        if (!(factors.Size > 1))
        {
            throw new ArgumentException("g2 model needs two factors to compute discount bond", nameof(factors));
        }

        return DiscountBond(now, maturity, factors[0], factors[1]);
    }

    /// <summary>The price at a time of a unit discount bond maturing later: A(t, T)·exp(−B(a, T − t)x − B(b, T − t)y).</summary>
    /// <param name="t">The time.</param>
    /// <param name="T">The bond's maturity.</param>
    /// <param name="x">The first factor at <paramref name="t"/>.</param>
    /// <param name="y">The second factor at <paramref name="t"/>.</param>
    /// <returns>The price.</returns>
    public double DiscountBond(double t, double T, double x, double y) =>
        A(t, T) * Math.Exp((-B(A(), T - t) * x) - (B(B(), T - t) * y));

    /// <summary>The price of an option on a discount bond, by Black's formula on the curve's discount factors.</summary>
    /// <param name="type">The option's type.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="maturity">The option's maturity.</param>
    /// <param name="bondMaturity">The bond's maturity.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public double DiscountBondOption(OptionType type, double strike, double maturity, double bondMaturity)
    {
        double v = SigmaP(maturity, bondMaturity);
        double f = TermStructure.Discount(bondMaturity);
        double k = TermStructure.Discount(maturity) * strike;

        return BlackFormulaFunctions.BlackFormula(type, k, f, v);
    }

    /// <summary>
    /// The price of an option on a discount bond that starts later than the option's maturity: as QuantLib's
    /// <c>AffineModel</c> default, the option on the bond from the option's maturity, <paramref name="bondStart"/> ignored.
    /// </summary>
    /// <param name="type">The option's type.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="maturity">The option's maturity.</param>
    /// <param name="bondStart">The bond's start, unused.</param>
    /// <param name="bondMaturity">The bond's maturity.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public double DiscountBondOption(OptionType type, double strike, double maturity, double bondStart, double bondMaturity) =>
        DiscountBondOption(type, strike, maturity, bondMaturity);

    /// <summary>The implied discount curve: the curve's own discount factor.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The discount factor.</returns>
    public double Discount(double t) => TermStructure.Discount(t);

    /// <summary>
    /// The price of a European swaption, exercised at its swap's start: QuantLib's <c>swaption</c>, the integral over the
    /// first factor's distribution at the start, between <paramref name="range"/> standard deviations either side of its
    /// mean, on <paramref name="intervals"/> segments, of the swap's payoff conditional on it, the second factor's
    /// break-even level solved by <see cref="Brent"/>.
    /// </summary>
    /// <param name="arguments">The swaption's swap's arguments.</param>
    /// <param name="fixedRate">The fixed rate, corrected for the floating leg's spread.</param>
    /// <param name="range">The number of standard deviations either side.</param>
    /// <param name="intervals">The number of segments.</param>
    /// <returns>The price.</returns>
    /// <exception cref="NanoQLException">The swap's nominals aren't constant, or the break-even level can't be solved.</exception>
    /// <remarks>
    /// The fixed coupons are the swap's: each the corrected rate times the coupon's own accrual period, the last with
    /// the notional, where QuantLib's are the rate times the interval between pay times on the curve's day counter
    /// (<c>g2.cpp:162-163</c>), which the swap's fixed day counter and accruals never enter (the S-sr plan's
    /// SSR6 (xiii)). QuantLib's member takes the engine's arguments, internal here: <see cref="G2SwaptionEngine"/> is
    /// its public route.
    /// </remarks>
    internal double Swaption(FixedVsFloatingSwapArguments arguments, double fixedRate, double range, int intervals)
    {
        if (arguments.Nominal is not double nominal)
        {
            throw new NanoQLException("non-constant nominals are not supported yet");
        }

        Date settlement = TermStructure.ReferenceDate;
        DayCounter dayCounter = TermStructure.DayCounter;
        double start = dayCounter.YearFraction(settlement, arguments.FloatingResetDates[0]);
        double w = arguments.Type == SwapType.Payer ? 1 : -1;

        double[] fixedPayTimes = new double[arguments.FixedPayDates.Length];
        for (int i = 0; i < fixedPayTimes.Length; ++i)
        {
            fixedPayTimes[i] = dayCounter.YearFraction(settlement, arguments.FixedPayDates[i]);
        }

        var function = new SwaptionPricingFunction(
            A(), Sigma, B(), Eta, Rho, w, start, fixedPayTimes, arguments.FixedAccrualTimes, fixedRate, this);

        double upper = function.Mux + (range * function.Sigmax);
        double lower = function.Mux - (range * function.Sigmax);
        var integrator = new SegmentIntegral(intervals);
        return nominal * w * TermStructure.Discount(start) * integrator.Integrate(function.Evaluate, lower, upper);
    }

    /// <summary>The analytical fitting parameter, from the curve and the parameters.</summary>
    [MemberNotNull(nameof(phi))]
    protected override void GenerateArguments() =>
        phi = new FittingParameter(TermStructure, A(), Sigma, B(), Eta, Rho);

    /// <summary>A(t, T) = P(T)/P(t)·exp(½(V(T − t) − V(T) + V(t))), P the curve's discount.</summary>
    /// <param name="t">The time.</param>
    /// <param name="T">The bond's maturity.</param>
    /// <returns>A(t, T).</returns>
    private double A(double t, double T) =>
        TermStructure.Discount(T) / TermStructure.Discount(t) * Math.Exp(0.5 * (V(T - t) - V(T) + V(t)));

    /// <summary>B(x, t) = (1 − e^(−xt))/x, written with expm1 (QuantLib's master).</summary>
    /// <param name="x">The mean reversion.</param>
    /// <param name="t">The time.</param>
    /// <returns>B(x, t).</returns>
    private static double B(double x, double t) => -Expm1Functions.StdExpm1(-x * t) / x;

    // The bond option's volatility, QuantLib's sigmaP: sigma^2*B_a^2*t*f(2at) + eta^2*B_b^2*t*f(2bt)
    // + 2*rho*sigma*eta*B_a*B_b*t*f((a+b)t), with B_x = (s-t)*f(x(s-t)) and f(x) = (1-exp(-x))/x, which is QuantLib's
    // 0.5*sigma^2*(1-exp(-a(s-t)))^2*(1-exp(-2at))/a^3 + ... rewritten without its O(1/a^3) cancellation.
    private double SigmaP(double t, double s)
    {
        double a = A(), b = B();
        double tau = s - t;
        double ba = tau * G2Functions.OneMinusExpOverX(a * tau);
        double bb = tau * G2Functions.OneMinusExpOverX(b * tau);
        double value = (Sigma * Sigma * ba * ba * t * G2Functions.OneMinusExpOverX(2.0 * a * t))
            + (Eta * Eta * bb * bb * t * G2Functions.OneMinusExpOverX(2.0 * b * t))
            + (2.0 * Rho * Sigma * Eta * ba * bb * t * G2Functions.OneMinusExpOverX((a + b) * t));
        return Math.Sqrt(value);
    }

    // the variance of the integral of the two factors between 0 and t, that is, the
    // integral of sigma^2*B(a,s)^2 + eta^2*B(b,s)^2 + 2*rho*sigma*eta*B(a,s)*B(b,s)
    private double V(double t)
    {
        double u = A() * t;
        double v = B() * t;
        double valuex = Sigma * Sigma * G2Functions.VarianceKernel(u, u);
        double valuey = Eta * Eta * G2Functions.VarianceKernel(v, v);
        double value = 2.0 * Rho * Sigma * Eta * G2Functions.VarianceKernel(u, v);
        return t * t * t * (valuex + valuey + value);
    }

    // The short-rate dynamics: r = phi(t) + x + y.
    private sealed class G2Dynamics(Parameter fitting, double a, double sigma, double b, double eta, double rho)
        : TwoFactorModelShortRateDynamics(new OrnsteinUhlenbeckProcess(a, sigma), new OrnsteinUhlenbeckProcess(b, eta), rho)
    {
        public override double ShortRate(double t, double x, double y) => fitting.Evaluate(t) + x + y;
    }

    // The analytical term-structure fitting parameter phi(t), its terms sigma*(1-exp(-at))/a as sigma*t*f(at).
    private sealed class FittingParameter(YieldTermStructure termStructure, double a, double sigma, double b, double eta, double rho)
        : TermStructureFittingParameter(new FittingImpl(termStructure, a, sigma, b, eta, rho))
    {
        private sealed class FittingImpl(YieldTermStructure termStructure, double a, double sigma, double b, double eta, double rho)
            : Impl
        {
            public override double Value(ImmutableArray<double> parameters, double t)
            {
                // ForwardRate(t, t, Continuous)'s rate without its InterestRate, so a finite-difference step allocates
                // nothing (the S-sr plan's SSD10)
                double forward = termStructure.ContinuousForwardRate(t, t);
                double temp1 = sigma * t * G2Functions.OneMinusExpOverX(a * t);
                double temp2 = eta * t * G2Functions.OneMinusExpOverX(b * t);
                double value = (0.5 * temp1 * temp1) + (0.5 * temp2 * temp2) + (rho * temp1 * temp2) + forward;
                return value;
            }
        }
    }

    // QuantLib's SwaptionPricingFunction: the payoff's density at the first factor's level x, the second's
    // break-even level solved for each x.
    private sealed class SwaptionPricingFunction
    {
        private readonly double w;
        private readonly double rate;
        private readonly int size;
        private readonly double[] t;
        private readonly double[] accruals;
        private readonly double[] aT, ba, bb;
        private readonly double muy, sigmay, rhoxy;
        private readonly CumulativeNormalDistribution normal = new();

        public SwaptionPricingFunction(
            double a,
            double sigma,
            double b,
            double eta,
            double rho,
            double w,
            double start,
            double[] payTimes,
            ImmutableArray<double> accruals,
            double fixedRate,
            G2 model)
        {
            this.w = w;
            double T = start;
            t = payTimes;
            this.accruals = [.. accruals];
            rate = fixedRate;
            size = t.Length;
            aT = new double[size];
            ba = new double[size];
            bb = new double[size];

            // QuantLib's sigma*sqrt(0.5*(1-exp(-2aT))/a), eta*sqrt(0.5*(1-exp(-2bT))/b) and
            // rho*eta*sigma*(1-exp(-(a+b)T))/((a+b)*sigmax*sigmay), each (1-exp(-x))/x as f(x)
            Sigmax = sigma * Math.Sqrt(T * G2Functions.OneMinusExpOverX(2.0 * a * T));
            sigmay = eta * Math.Sqrt(T * G2Functions.OneMinusExpOverX(2.0 * b * T));
            rhoxy = rho * eta * sigma * T * G2Functions.OneMinusExpOverX((a + b) * T) / (Sigmax * sigmay);

            // QuantLib's -((sigma^2/a^2 + rho*sigma*eta/(a*b))*(1 - exp(-aT)) - 0.5*sigma^2/a^2*(1 - exp(-2aT))
            // - rho*sigma*eta/(b*(a+b))*(1 - exp(-(b+a)T))), whose sigma^2 terms are -0.5*sigma^2*(T*f(aT))^2 and whose
            // rho terms are -rho*sigma*eta*T^2*(f(aT) - f((a+b)T))/(bT), each O(1/a^2) or O(1/b) apart; muy alike
            double fa = T * G2Functions.OneMinusExpOverX(a * T);
            double fb = T * G2Functions.OneMinusExpOverX(b * T);
            Mux = -((0.5 * sigma * sigma * fa * fa) + (rho * sigma * eta * T * T * G2Functions.DecayRatioSlope(a * T, b * T)));
            muy = -((0.5 * eta * eta * fb * fb) + (rho * sigma * eta * T * T * G2Functions.DecayRatioSlope(b * T, a * T)));

            for (int i = 0; i < size; i++)
            {
                aT[i] = model.A(T, t[i]);
                ba[i] = G2.B(a, t[i] - T);
                bb[i] = G2.B(b, t[i] - T);
            }
        }

        public double Mux { get; }

        public double Sigmax { get; }

        public double Evaluate(double x)
        {
            double temp = (x - Mux) / Sigmax;
            double txy = Math.Sqrt(1.0 - (rhoxy * rhoxy));

            double[] lambda = new double[size];
            int i;
            for (i = 0; i < size; i++)
            {
                // the swap's own coupon: its accrual period times the rate, the last with the notional
                double c = i == size - 1 ? 1.0 + (rate * accruals[i]) : rate * accruals[i];
                lambda[i] = c * aT[i] * Math.Exp(-ba[i] * x);
            }

            var function = new SolvingFunction(lambda, bb);
            Brent s1d = new Brent().SetMaxEvaluations(1000);
            double searchBound = Math.Max(10.0 * sigmay, 1.0);
            double yb = s1d.Solve(function, 1e-6, 0.00, -searchBound, searchBound);

            double h1 = ((yb - muy) / (sigmay * txy)) - (rhoxy * (x - Mux) / (Sigmax * txy));
            double value = normal.Evaluate(-w * h1);

            for (i = 0; i < size; i++)
            {
                double h2 = h1 + (bb[i] * sigmay * Math.Sqrt(1.0 - (rhoxy * rhoxy)));
                double kappa = -bb[i] * (muy - (0.5 * txy * txy * sigmay * sigmay * bb[i]) + (rhoxy * sigmay * (x - Mux) / Sigmax));
                value -= lambda[i] * Math.Exp(kappa) * normal.Evaluate(-w * h2);
            }

            return Math.Exp(-0.5 * temp * temp) * value / (Sigmax * Math.Sqrt(2.0 * Math.PI));
        }

        // 1 less the coupons' discounted values at the second factor's level y
        private readonly struct SolvingFunction(double[] lambda, double[] bb) : ISolver1DFunction
        {
            public double Evaluate(double y)
            {
                double value = 1.0;
                for (int i = 0; i < lambda.Length; i++)
                {
                    value -= lambda[i] * Math.Exp(-bb[i] * y);
                }

                return value;
            }
        }
    }
}
