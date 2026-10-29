// SPDX-FileCopyrightText: Copyright (C) 2010 SunTrust Bank
// SPDX-FileCopyrightText: Copyright (C) 2010, 2014 Cavit Hafizoglu
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/shortrate/generalizedhullwhite.hpp ql/experimental/shortrate/generalizedhullwhite.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The generalized Hull-White model: d f(rₜ) = (θ(t) − α(t) f(rₜ))dt + σ(t) dWₜ, with α and σ interpolated over dates
/// and f a function of the short rate, fitted to a yield curve: QuantLib's <c>GeneralizedHullWhite</c>.
/// </summary>
/// <remarks>
/// <para>
/// Its short rate is f⁻¹(xₜ + g(t)), xₜ a <see cref="GeneralizedOrnsteinUhlenbeckProcess"/> and g the deterministic
/// fitting parameter, which can't be determined analytically: a tree fits it to the curve at each step as it is built,
/// by <see cref="Brent"/> (2,000 evaluations, accuracy 1e-8, in [−50, 50] from the previous step's value), each tree
/// its own fitting, as QuantLib's. Its parameters are α's values at its speed dates and σ's at its volatility dates, in
/// that order, interpolated over their times from the curve's reference date (<see cref="InterpolationParameter"/>).
/// </para>
/// <para>
/// A, B and the bond options are Hull-White's with time-dependent parameters (Gurrieri, Nakabayashi and Wong, 2009), on
/// QuantLib's numerical integrals of α and σ, and valid only where f is the identity: at a constant α and σ they are
/// within those integrals' error of <see cref="HullWhite"/>'s closed forms (4.6e-4 relative on a bond option at α 0.1),
/// QuantLib's design, kept.
/// </para>
/// <para>
/// <see cref="Dynamics"/> refuses, as QuantLib's does: the general model has no process, and <see cref="HwDynamics"/>
/// gives Hull-White's at α(0) and σ(0), with the analytical fitting parameter φ(t) = f(t) + ½[σ(1 − e^(−αt))/α]², f the
/// instantaneous forward. Below |α| 1e-2 φ is in its cancellation-free form through expm1, σt at α = 0: QuantLib cuts
/// to σt below α √ε, which jumps there and cancels just above, and takes that cut for every negative α (φ(5) 3.6e-4 low
/// at −0.05) (the S-xrt plan's XRR5 (xix); <c>Divergences/GeneralizedHullWhiteSmallReversionTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class GeneralizedHullWhite : OneFactorAffineModel, ITermStructureConsistentModel
{
    private readonly Func<double, double> f;
    private readonly Func<double, double> fInverse;

    // Written by GenerateArguments, on a model under construction or a fresh copy only.
    private Interpolation speed;
    private Interpolation vol;
    private Parameter phi;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneralizedHullWhite"/> class, α and σ interpolated linearly
    /// between their dates and flat outside them (<see cref="LinearFlat"/>).
    /// </summary>
    /// <param name="termStructure">The curve the model fits.</param>
    /// <param name="speedStructure">The dates of the mean reversion's values.</param>
    /// <param name="volStructure">The dates of the volatility's values.</param>
    /// <param name="speed">The mean reversion's values.</param>
    /// <param name="vol">The volatility's values.</param>
    /// <param name="f">The function of the short rate the factor follows, or <see langword="null"/> for the identity.</param>
    /// <param name="fInverse">Its inverse, or <see langword="null"/> for the identity.</param>
    /// <exception cref="ArgumentNullException">A curve or a sequence is null.</exception>
    /// <exception cref="NanoQLException">The dates and the values differ in number, with QuantLib's texts.</exception>
    /// <exception cref="ArgumentException">There are no dates.</exception>
    public GeneralizedHullWhite(
        YieldTermStructure termStructure,
        IEnumerable<Date> speedStructure,
        IEnumerable<Date> volStructure,
        IEnumerable<double> speed,
        IEnumerable<double> vol,
        Func<double, double>? f = null,
        Func<double, double>? fInverse = null)
        : this(
            termStructure,
            Parameters(termStructure, speedStructure, volStructure, speed, vol, default(LinearFlat), default(LinearFlat)),
            f,
            fInverse)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneralizedHullWhite"/> class as Hull-White's model: a constant α
    /// and σ, each a single value at the curve's reference date (<see cref="BackwardFlat"/>), and f the identity.
    /// </summary>
    /// <param name="termStructure">The curve the model fits.</param>
    /// <param name="a">The mean reversion α.</param>
    /// <param name="sigma">The volatility σ.</param>
    /// <exception cref="ArgumentNullException"><paramref name="termStructure"/> is null.</exception>
    public GeneralizedHullWhite(YieldTermStructure termStructure, double a = 0.1, double sigma = 0.01)
        : this(termStructure, ConstantParameters(termStructure, a, sigma), null, null)
    {
    }

    private GeneralizedHullWhite(
        YieldTermStructure termStructure,
        (InterpolationParameter Speed, InterpolationParameter Vol) parameters,
        Func<double, double>? f,
        Func<double, double>? fInverse)
        : base(2)
    {
        TermStructure = termStructure;
        this.f = f ?? Identity;
        this.fInverse = fInverse ?? Identity;
        SetArgument(0, parameters.Speed);
        SetArgument(1, parameters.Vol);
        GenerateArguments();
    }

    /// <summary>Gets the curve the model fits.</summary>
    public YieldTermStructure TermStructure { get; }

    /// <summary>Gets the volatility at time 0: QuantLib's <c>sigma()</c>.</summary>
    public double Sigma => Arguments[1].Evaluate(0.0);

    /// <summary>
    /// A model with α and σ interpolated over their dates by the given interpolations, extrapolated past them: QuantLib's
    /// templated constructor.
    /// </summary>
    /// <typeparam name="TSpeedInterpolator">The mean reversion's interpolation factory's type.</typeparam>
    /// <typeparam name="TVolInterpolator">The volatility's interpolation factory's type.</typeparam>
    /// <param name="termStructure">The curve the model fits.</param>
    /// <param name="speedStructure">The dates of the mean reversion's values.</param>
    /// <param name="volStructure">The dates of the volatility's values.</param>
    /// <param name="speed">The mean reversion's values.</param>
    /// <param name="vol">The volatility's values.</param>
    /// <param name="speedInterpolator">The mean reversion's interpolation factory.</param>
    /// <param name="volInterpolator">The volatility's interpolation factory.</param>
    /// <param name="f">The function of the short rate the factor follows, or <see langword="null"/> for the identity.</param>
    /// <param name="fInverse">Its inverse, or <see langword="null"/> for the identity.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentNullException">A curve or a sequence is null.</exception>
    /// <exception cref="NanoQLException">The dates and the values differ in number, with QuantLib's texts.</exception>
    /// <exception cref="ArgumentException">An interpolation can't be built over its dates.</exception>
    public static GeneralizedHullWhite Create<TSpeedInterpolator, TVolInterpolator>(
        YieldTermStructure termStructure,
        IEnumerable<Date> speedStructure,
        IEnumerable<Date> volStructure,
        IEnumerable<double> speed,
        IEnumerable<double> vol,
        TSpeedInterpolator speedInterpolator,
        TVolInterpolator volInterpolator,
        Func<double, double>? f = null,
        Func<double, double>? fInverse = null)
        where TSpeedInterpolator : struct, IInterpolator
        where TVolInterpolator : struct, IInterpolator =>
        new(
            termStructure,
            Parameters(termStructure, speedStructure, volStructure, speed, vol, speedInterpolator, volInterpolator),
            f,
            fInverse);

    /// <summary>The mean reversion at time 0: QuantLib's <c>a()</c>.</summary>
    /// <returns>α(0).</returns>
    public double A() => Arguments[0].Evaluate(0.0);

    /// <summary>The mean reversion as a function of time, extrapolated past its dates.</summary>
    /// <returns>The function.</returns>
    public Func<double, double> Speed()
    {
        Interpolation s = speed;
        return t => s.Evaluate(t);
    }

    /// <summary>The volatility as a function of time, extrapolated past its dates.</summary>
    /// <returns>The function.</returns>
    public Func<double, double> Vol()
    {
        Interpolation v = vol;
        return t => v.Evaluate(t);
    }

    /// <summary>The flags to pass to calibration to fit the volatility only: the mean reversion's values fixed.</summary>
    /// <returns>One flag per parameter, true for each of α's.</returns>
    public ImmutableArray<bool> FixedReversion()
    {
        int na = Arguments[0].Size;
        int nsigma = Arguments[1].Size;
        var fixr = new bool[na + nsigma];
        Array.Fill(fixr, true, 0, na);
        return [.. fixr];
    }

    /// <summary>Refuses, as QuantLib's does: the general model has no process.</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="NanoQLException">Always, with QuantLib's text.</exception>
    public override OneFactorModelShortRateDynamics Dynamics() =>
        throw new NanoQLException("no defined process for generalized Hull-White model, use HWdynamics()");

    /// <summary>Hull-White's short-rate dynamics at α(0) and σ(0), with the analytical fitting parameter.</summary>
    /// <returns>A new dynamics object.</returns>
    /// <exception cref="ArgumentException">σ(0) is negative.</exception>
    public OneFactorModelShortRateDynamics HwDynamics() => new HullWhiteDynamics(phi, A(), Sigma);

    /// <summary>Builds the model's trinomial tree on a time grid, its own fitting parameter fitted to the curve.</summary>
    /// <param name="grid">The time grid.</param>
    /// <returns>The fitted <see cref="OneFactorModelShortRateTree"/>, immutable once returned.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
    /// <exception cref="NanoQLException">A step's fitting value can't be solved.</exception>
    public override Lattice Tree(TimeGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var fitting = new TermStructureFittingParameter(TermStructure);
        var numericDynamics = new GeneralizedDynamics(fitting, Speed(), Vol(), f, fInverse);
        var trinomial = new TrinomialTree(numericDynamics.Process, grid);
        var numericTree = new OneFactorModelShortRateTree(trinomial, numericDynamics, grid);

        var impl = (TermStructureFittingParameter.NumericalImpl)fitting.Implementation();
        impl.Reset();
        double value = 1.0;
        double vMin = -50.0;
        double vMax = 50.0;
        for (int i = 0; i < grid.Size - 1; i++)
        {
            double discountBond = TermStructure.Discount(grid[i + 1]);
            double xMin = trinomial.Underlying(i, 0);
            double dx = trinomial.Dx(i);
            var finder = new Helper(i, xMin, dx, discountBond, numericTree, fInverse);
            Brent s1d = new Brent().SetMaxEvaluations(2000);
            value = s1d.Solve(finder, 1e-8, value, vMin, vMax);
            impl.Set(grid[i], value);
        }

        return numericTree;
    }

    /// <summary>
    /// The price of an option on a discount bond, by Black's formula on the curve's discount factors, with the
    /// time-dependent parameters' numerical variance: valid only where f is the identity, as QuantLib's.
    /// </summary>
    /// <param name="type">The option's type.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="maturity">The option's maturity.</param>
    /// <param name="bondMaturity">The bond's maturity.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public override double DiscountBondOption(OptionType type, double strike, double maturity, double bondMaturity)
    {
        // Hull-White bond option pricing with time varying sigma and mean reversion. Based on Gurrieri, Nakabayashi &
        // Wong (2009) "Calibration Methods of Hull-White Model", https://ssrn.com/abstract=1514192
        double btT = B(maturity, bondMaturity);
        double vr = V(0, maturity);
        double vp = vr * btT * btT;
        double v = Math.Sqrt(vp);
        double fwd = TermStructure.Discount(bondMaturity);
        double k = TermStructure.Discount(maturity) * strike;
        return BlackFormulaFunctions.BlackFormula(type, k, fwd, v);
    }

    /// <summary>The interpolations of α and σ from the parameters, and the analytical fitting parameter.</summary>
    [MemberNotNull(nameof(speed), nameof(vol), nameof(phi))]
    protected override void GenerateArguments()
    {
        speed = ((InterpolationParameter)Arguments[0]).Interpolate();
        vol = ((InterpolationParameter)Arguments[1]).Interpolate();
        phi = new FittingParameter(TermStructure, A(), Sigma);
    }

    /// <summary>The affine discount bond's A(t, T), fitted to the curve (Gurrieri et al., equation (43)).</summary>
    /// <param name="t">The time.</param>
    /// <param name="T">The bond's maturity.</param>
    /// <returns>A(t, T).</returns>
    protected override double A(double t, double T)
    {
        double discount1 = TermStructure.Discount(t);
        double discount2 = TermStructure.Discount(T);
        // ForwardRate(t, t, Continuous)'s rate without its InterestRate
        double forward = TermStructure.ContinuousForwardRate(t, t);
        double btT = B(t, T);
        double vr = V(0, t);
        double atT = Math.Log(discount2 / discount1) + (btT * forward) - (0.5 * btT * btT * vr);
        return Math.Exp(atT);
    }

    /// <summary>The affine discount bond's B(t, T), by Simpson's rule on α (Gurrieri et al., equations (30), (31)).</summary>
    /// <param name="t">The time.</param>
    /// <param name="T">The bond's maturity.</param>
    /// <returns>B(t, T).</returns>
    protected override double B(double t, double T)
    {
        // Gurrieri et al, equations (30) and (31)
        double lnEt = IntegrateMeanReversion(t);
        double et = Math.Exp(lnEt);
        double b = 0;
        int n = Steps(t, T);
        double dt = 0.5 * (T - t) / n;
        double a, bb, c, time, total = 0;
        time = t;
        c = speed.Evaluate(time);
        time += dt;
        for (int i = 0; i < n; i++)
        {
            a = c;
            bb = speed.Evaluate(time);
            c = speed.Evaluate(time + dt);
            total += dt * (2.0 / 6.0) * (a + (4 * bb) + c);
            b += 2 * dt / Math.Exp(lnEt + total);
            time += 2 * dt;
        }

        b *= et;
        return b;
    }

    // Size((T - t)*365) capped at 2,000 and at least 1, as QuantLib's B and V take it: the conversion truncates, and on
    // MSVC a span of a day or more negative, or NaN, converts to a size past the cap.
    private static int Steps(double t, double T)
    {
        double days = (T - t) * 365;
        int n = !(days > -1.0) || days >= 2000.0 ? 2000 : (int)days;
        return n == 0 ? 1 : n;
    }

    private static double Identity(double x) => x;

    private static (InterpolationParameter Speed, InterpolationParameter Vol) ConstantParameters(
        YieldTermStructure termStructure, double a, double sigma)
    {
        ArgumentNullException.ThrowIfNull(termStructure);
        Date reference = termStructure.ReferenceDate;
        return Parameters(
            termStructure, [reference], [reference], [a], [sigma], default(BackwardFlat), default(BackwardFlat));
    }

    // QuantLib's initialize: the sizes checked, then each interpolation over its dates' times from the reference date.
    private static (InterpolationParameter Speed, InterpolationParameter Vol)
        Parameters<TSpeedInterpolator, TVolInterpolator>(
        YieldTermStructure termStructure,
        IEnumerable<Date> speedStructure,
        IEnumerable<Date> volStructure,
        IEnumerable<double> speed,
        IEnumerable<double> vol,
        TSpeedInterpolator speedInterpolator,
        TVolInterpolator volInterpolator)
        where TSpeedInterpolator : struct, IInterpolator
        where TVolInterpolator : struct, IInterpolator
    {
        ArgumentNullException.ThrowIfNull(termStructure);
        ArgumentNullException.ThrowIfNull(speedStructure);
        ArgumentNullException.ThrowIfNull(volStructure);
        ArgumentNullException.ThrowIfNull(speed);
        ArgumentNullException.ThrowIfNull(vol);
        Date[] speedDates = [.. speedStructure];
        Date[] volDates = [.. volStructure];
        double[] speeds = [.. speed];
        double[] vols = [.. vol];
        if (speedDates.Length != speeds.Length)
        {
            throw new NanoQLException("mean reversion inputs inconsistent");
        }

        if (volDates.Length != vols.Length)
        {
            throw new NanoQLException("volatility inputs inconsistent");
        }

        DayCounter dc = termStructure.DayCounter;
        Date reference = termStructure.ReferenceDate;
        double[] speedPeriods = [.. speedDates.Select(d => dc.YearFraction(reference, d))];
        double[] volPeriods = [.. volDates.Select(d => dc.YearFraction(reference, d))];
        var a = InterpolationParameter.Create(speedPeriods, speeds, new NoConstraint(), speedInterpolator);
        var sigma = InterpolationParameter.Create(volPeriods, vols, new PositiveConstraint(), volInterpolator);
        return (a, sigma);
    }

    // The integral of the mean reversion from 0 to t, by Simpson's rule at QuantLib's accuracy.
    private double IntegrateMeanReversion(double t)
    {
        if (t < NumericLimits.QL_EPSILON)
        {
            return 0.0;
        }

        var integrator = new SimpsonIntegral(1e-5, 1000);
        Interpolation s = speed;
        return integrator.Integrate(x => s.Evaluate(x), 0, t);
    }

    // V(t, T), the short rate's variance, by Simpson's rule on σ and α (Gurrieri et al., equation (37)).
    private double V(double t, double T)
    {
        // Gurrieri et al, equation (37)
        double lnEt = IntegrateMeanReversion(t);
        double v = 0, eu;
        int n = Steps(t, T);
        double dt = 0.5 * (T - t) / n;
        double a, b, c, time, lnE = lnEt;
        time = t;
        double volatility = vol.Evaluate(time);
        eu = Math.Exp(lnE);
        c = eu * eu * volatility * volatility;
        time += dt;
        for (int i = 0; i < n; i++)
        {
            a = c;
            volatility = vol.Evaluate(time);
            lnE += speed.Evaluate(time) * dt;
            eu = Math.Exp(lnE);
            b = eu * eu * volatility * volatility;
            volatility = vol.Evaluate(time + dt);
            lnE += speed.Evaluate(time + dt) * dt;
            eu = Math.Exp(lnE);
            c = eu * eu * volatility * volatility;
            v += dt * (2.0 / 6.0) * (a + (4 * b) + c);
            time += 2 * dt;
        }

        return v / (eu * eu);
    }

    // The short-rate dynamics: f(r) = x + g(t), x a generalized Ornstein-Uhlenbeck process.
    private sealed class GeneralizedDynamics(
        Parameter fitting,
        Func<double, double> alpha,
        Func<double, double> sigma,
        Func<double, double> f,
        Func<double, double> fInverse)
        : OneFactorModelShortRateDynamics(new GeneralizedOrnsteinUhlenbeckProcess(alpha, sigma))
    {
        public override double Variable(double t, double r) => f(r) - fitting.Evaluate(t);

        public override double ShortRate(double t, double variable) => fInverse(variable + fitting.Evaluate(t));
    }

    // Hull-White's dynamics: r = x + φ(t), x an Ornstein-Uhlenbeck process.
    private sealed class HullWhiteDynamics(Parameter fitting, double a, double sigma)
        : OneFactorModelShortRateDynamics(new OrnsteinUhlenbeckProcess(a, sigma))
    {
        public override double Variable(double t, double r) => r - fitting.Evaluate(t);

        public override double ShortRate(double t, double variable) => variable + fitting.Evaluate(t);
    }

    // The analytical term-structure fitting parameter: φ(t) = f(t) + ½[σ(1 − e^(−at))/a]², f the instantaneous forward.
    private sealed class FittingParameter(YieldTermStructure termStructure, double a, double sigma)
        : TermStructureFittingParameter(new FittingImpl(termStructure, a, sigma))
    {
        private sealed class FittingImpl(YieldTermStructure termStructure, double a, double sigma) : Impl
        {
            public override double Value(ImmutableArray<double> parameters, double t)
            {
                // ForwardRate(t, t, Continuous)'s rate without its InterestRate
                double forwardRate = termStructure.ContinuousForwardRate(t, t);

                // the expm1 form below |a| 1e-2, sigma*t at 0, QuantLib's from 1e-2 and past -1e-2 (XRR5 (xix))
                double temp;
                if (a == 0.0)
                {
                    temp = sigma * t;
                }
                else if (Math.Abs(a) < 1e-2)
                {
                    temp = sigma * -Expm1Functions.StdExpm1(-a * t) / a;
                }
                else
                {
                    temp = sigma * (1.0 - Math.Exp(-a * t)) / a;
                }

                return forwardRate + (0.5 * temp * temp);
            }
        }
    }

    // The function the fitting solves at a step: the curve's discount less the step's state prices, each discounted
    // at the node's short rate f⁻¹(theta + x). Its construction stores the step's state prices.
    private readonly struct Helper : ISolver1DFunction
    {
        private readonly int size;
        private readonly double dt;
        private readonly double xMin, dx;
        private readonly ImmutableArray<double> statePrices;
        private readonly double discountBondPrice;
        private readonly Func<double, double> fInverse;

        public Helper(
            int i,
            double xMin,
            double dx,
            double discountBondPrice,
            OneFactorModelShortRateTree tree,
            Func<double, double> fInv)
        {
            size = tree.Size(i);
            dt = tree.TimeGrid.Dt(i);
            this.xMin = xMin;
            this.dx = dx;
            tree.ComputeStatePrices(i);
            statePrices = tree.StatePrices(i);
            this.discountBondPrice = discountBondPrice;
            fInverse = fInv;
        }

        public double Evaluate(double theta)
        {
            double value = discountBondPrice;
            double x = xMin;
            for (int j = 0; j < size; j++)
            {
                double discount = Math.Exp(-fInverse(theta + x) * dt);
                value -= statePrices[j] * discount;
                x += dx;
            }

            return value;
        }
    }
}
