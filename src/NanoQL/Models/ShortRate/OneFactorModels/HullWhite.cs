// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodels/hullwhite.hpp ql/models/shortrate/onefactormodels/hullwhite.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>
/// The single-factor Hull-White (extended Vasicek) model: drₜ = (θ(t) − αrₜ)dt + σ dWₜ, with constant α and σ, fitted
/// to a yield curve.
/// </summary>
/// <remarks>
/// <para>
/// Its short rate is r = φ(t) + x, with x an Ornstein-Uhlenbeck process and φ the deterministic fitting parameter:
/// analytical in <see cref="Dynamics"/>, φ(t) = f(t) + ½[σ(1 − e^(−αt))/α]², and numerical in <see cref="Tree"/>, whose
/// tree fits φ to the curve at each step as it is built. Its parameters are α and σ; b and λ are null parameters.
/// </para>
/// <para>
/// Its constructor computes the curve's instantaneous forward rate at 0 and then its zero rate at 0, as QuantLib's does,
/// so the curve's refusals come in QuantLib's order; the second is the initial short rate.
/// </para>
/// <para>
/// B(t, T) is Vasicek's, in QuantLib's master's form after the pin (#2882); the members QuantLib 1.44 cuts at small
/// α, the bond options, the analytical fitting parameter and the convexity bias, are written in the same
/// cancellation-free forms, exact in the limit α → 0, so their last bits differ from 1.44's at every α
/// (<c>Divergences/SmallReversionTests.cs</c>).
/// </para>
/// </remarks>
public sealed class HullWhite : Vasicek, ITermStructureConsistentModel
{
    // Written by GenerateArguments, on a model under construction or a fresh copy only.
    private Parameter phi;

    /// <summary>Initializes a new instance of the <see cref="HullWhite"/> class.</summary>
    /// <param name="termStructure">The curve the model fits.</param>
    /// <param name="a">The mean-reversion speed α, positive.</param>
    /// <param name="sigma">The volatility σ, positive.</param>
    /// <exception cref="ArgumentNullException"><paramref name="termStructure"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="a"/> or <paramref name="sigma"/> isn't positive.</exception>
    public HullWhite(YieldTermStructure termStructure, double a = 0.1, double sigma = 0.01)
        : base(ForwardRateAtZero(termStructure), a, 0.0, sigma, 0.0)
    {
        TermStructure = termStructure;
        SetArgument(1, new NullParameter());
        SetArgument(3, new NullParameter());
        GenerateArguments();
    }

    /// <summary>Gets the curve the model fits.</summary>
    public YieldTermStructure TermStructure { get; }

    /// <summary>
    /// The convexity bias of a futures rate: the difference between the futures-implied rate and the forward rate, as
    /// in G. Kirikos and D. Novak, "Convexity Conundrums", Risk Magazine, March 1997.
    /// </summary>
    /// <param name="futurePrice">The futures' market price.</param>
    /// <param name="t">The futures' maturity, as a year fraction of the deposit's day counter.</param>
    /// <param name="T">The underlying deposit's maturity, likewise.</param>
    /// <param name="sigma">The volatility.</param>
    /// <param name="a">The mean-reversion speed.</param>
    /// <returns>The bias.</returns>
    /// <exception cref="ArgumentException">A price, time, volatility or speed is negative, or <paramref name="T"/> is before <paramref name="t"/>.</exception>
    public static double ConvexityBias(double futurePrice, double t, double T, double sigma, double a)
    {
        if (!(futurePrice >= 0.0))
        {
            throw new ArgumentException($"negative futures price ({Show(futurePrice)}) not allowed", nameof(futurePrice));
        }

        if (!(t >= 0.0))
        {
            throw new ArgumentException($"negative t ({Show(t)}) not allowed", nameof(t));
        }

        if (!(T >= t))
        {
            throw new ArgumentException($"T ({Show(T)}) must not be less than t ({Show(t)})", nameof(T));
        }

        if (!(sigma >= 0.0))
        {
            throw new ArgumentException($"negative sigma ({Show(sigma)}) not allowed", nameof(sigma));
        }

        if (!(a >= 0.0))
        {
            throw new ArgumentException($"negative a ({Show(a)}) not allowed", nameof(a));
        }

        // (1 - exp(-a*x))/a, in Vasicek's cancellation-free form
        double Temp(double x) => x * OneMinusExpOverX(a * x);

        double deltaT = T - t;
        double tempDeltaT = Temp(deltaT);
        double halfSigmaSquare = sigma * sigma / 2.0;

        // lambda adjusts for the fact that the underlying is an interest rate
        double lambda = Temp(2.0 * t) * tempDeltaT;

        double tempT = Temp(t);

        // phi is the MtM adjustment
        double phi = tempT * tempT;

        // the adjustment
        double z = halfSigmaSquare * (lambda + phi);

        double futureRate = (100.0 - futurePrice) / 100.0;
        return deltaT < NumericLimits.QL_EPSILON ? z : (1.0 - Math.Exp(-z * tempDeltaT)) * (futureRate + (1.0 / deltaT));
    }

    /// <summary>The fixed-parameter specification that fixes the reversion α and frees σ, for a calibration.</summary>
    /// <returns><c>[true, false]</c>.</returns>
    public static ImmutableArray<bool> FixedReversion() => [true, false];

    /// <summary>Builds the model's trinomial tree on a time grid, fitted to the curve at each step as it is built.</summary>
    /// <param name="grid">The time grid.</param>
    /// <returns>The fitted <see cref="OneFactorModelShortRateTree"/>, immutable once returned.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
    public override Lattice Tree(TimeGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var fitting = new TermStructureFittingParameter(TermStructure);
        var numericDynamics = new HullWhiteDynamics(fitting, A(), Sigma);
        var trinomial = new TrinomialTree(numericDynamics.Process, grid);
        var numericTree = new OneFactorModelShortRateTree(trinomial, numericDynamics, grid);

        var impl = (TermStructureFittingParameter.NumericalImpl)fitting.Implementation();
        impl.Reset();
        for (int i = 0; i < grid.Size - 1; i++)
        {
            double discountBond = TermStructure.Discount(grid[i + 1]);

            // the tree stores each step's state prices as the fit reaches it
            numericTree.ComputeStatePrices(i);
            ImmutableArray<double> statePrices = numericTree.StatePrices(i);
            int size = numericTree.Size(i);
            double dt = numericTree.TimeGrid.Dt(i);
            double dx = trinomial.Dx(i);
            double x = trinomial.Underlying(i, 0);
            double value = 0.0;
            for (int j = 0; j < size; j++)
            {
                value += statePrices[j] * Math.Exp(-x * dt);
                x += dx;
            }

            value = Math.Log(value / discountBond) / dt;
            impl.Set(grid[i], value);
        }

        return numericTree;
    }

    /// <summary>The model's short-rate dynamics, with the analytical fitting parameter.</summary>
    /// <returns>A new dynamics object.</returns>
    public override OneFactorModelShortRateDynamics Dynamics() => new HullWhiteDynamics(phi, A(), Sigma);

    /// <summary>The price of an option on a discount bond, by Black's formula on the curve's discount factors.</summary>
    /// <param name="type">The option's type.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="maturity">The option's maturity.</param>
    /// <param name="bondMaturity">The bond's maturity.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public override double DiscountBondOption(OptionType type, double strike, double maturity, double bondMaturity)
    {
        // 0.5*(1 - exp(-2*a*maturity))/a
        double variance = maturity * OneMinusExpOverX(2.0 * A() * maturity);
        double v = Sigma * B(maturity, bondMaturity) * Math.Sqrt(variance);

        double f = TermStructure.Discount(bondMaturity);
        double k = TermStructure.Discount(maturity) * strike;

        return BlackFormulaFunctions.BlackFormula(type, k, f, v);
    }

    /// <summary>The price of an option on a discount bond that starts later than the option's maturity.</summary>
    /// <param name="type">The option's type.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="maturity">The option's maturity.</param>
    /// <param name="bondStart">The bond's start.</param>
    /// <param name="bondMaturity">The bond's maturity.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public override double DiscountBondOption(
        OptionType type, double strike, double maturity, double bondStart, double bondMaturity)
    {
        // QuantLib's c/(2a^3), c = exp(-2a(U-T)) - exp(-2aU) - 2[exp(-a(U+S-2T)) - exp(-a(U+S))] + exp(-2a(S-T))
        // - exp(-2aS) with T the maturity, U the bond's start and S its maturity, is the product
        // [(exp(-aU) - exp(-aS))/a]^2 (exp(2aT) - 1)/(2a) = B(U,S)^2 exp(-2a(U-T)) T (1 - exp(-2aT))/(2aT), free of
        // its cancellation at small a and exact in the limit; QuantLib floors c at 0 (std::max(c, 0.0), which keeps
        // a NaN), so the last factor is floored likewise.
        double a = A();
        double variance = maturity * OneMinusExpOverX(2.0 * a * maturity);
        double v = Sigma * B(bondStart, bondMaturity) * Math.Exp(-a * (bondStart - maturity))
            * Math.Sqrt(variance < 0.0 ? 0.0 : variance);

        double f = TermStructure.Discount(bondMaturity);
        double k = TermStructure.Discount(bondStart) * strike;

        return BlackFormulaFunctions.BlackFormula(type, k, f, v);
    }

    /// <summary>The initial short rate and the analytical fitting parameter, from the curve and the parameters.</summary>
    [MemberNotNull(nameof(phi))]
    protected override void GenerateArguments()
    {
        R0 = TermStructure.ZeroRate(0.0, Compounding.Continuous, Frequency.NoFrequency).Rate;
        phi = new FittingParameter(TermStructure, A(), Sigma);
    }

    /// <summary>The affine discount bond's A(t, T), fitted to the curve.</summary>
    /// <param name="t">The time.</param>
    /// <param name="T">The bond's maturity.</param>
    /// <returns>A(t, T).</returns>
    protected override double A(double t, double T)
    {
        double discount1 = TermStructure.Discount(t);
        double discount2 = TermStructure.Discount(T);
        // ForwardRate(t, t, Continuous)'s rate without its InterestRate, so a Monte Carlo path's bonds allocate nothing
        double forward = TermStructure.ContinuousForwardRate(t, t);
        double temp = Sigma * B(t, T);
        double value = (B(t, T) * forward) - (0.25 * temp * temp * B(0.0, 2.0 * t));
        return Math.Exp(value) * discount2 / discount1;
    }

    private static double ForwardRateAtZero(YieldTermStructure termStructure)
    {
        ArgumentNullException.ThrowIfNull(termStructure);
        return termStructure.ForwardRate(0.0, 0.0, Compounding.Continuous, Frequency.NoFrequency).Rate;
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    // The short-rate dynamics: r = φ(t) + x, x an Ornstein-Uhlenbeck process.
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
                // ForwardRate(t, t, Continuous)'s rate without its InterestRate, so a finite-difference step allocates
                // nothing
                double forwardRate = termStructure.ContinuousForwardRate(t, t);
                double temp = sigma * t * OneMinusExpOverX(a * t);
                return forwardRate + (0.5 * temp * temp);
            }
        }
    }
}
