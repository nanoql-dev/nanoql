// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodels/blackkarasinski.hpp ql/models/shortrate/onefactormodels/blackkarasinski.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Models;

/// <summary>
/// The standard Black-Karasinski model: d ln rₜ = (θ(t) − α ln rₜ)dt + σ dWₜ, with constant α and σ, fitted to a
/// yield curve.
/// </summary>
/// <remarks>
/// <para>
/// Its short rate is rₜ = e^(φ(t) + xₜ), xₜ an Ornstein-Uhlenbeck process and φ the deterministic fitting parameter,
/// which can't be determined analytically: a tree fits it to the curve at each step as it is built, by
/// <see cref="Brent"/> (1,000 evaluations, accuracy 1e-7, in [−50, 50] from the previous step's value), and
/// <see cref="Dynamics"/> reads the fitting of a 50-step tree over the curve's maximum time, as QuantLib's does.
/// </para>
/// <para>
/// Each tree owns its fitting: a divergence, QuantLib's writing the model's one fitting parameter, so a second tree on
/// another grid rewrites the first's, which then refuses "fitting parameter not set!" at its own times
/// (<c>Divergences/BlackKarasinskiTreeFittingTests.cs</c>), as the Hull-White tree already does.
/// </para>
/// </remarks>
public sealed class BlackKarasinski : OneFactorModel, ITermStructureConsistentModel
{
    /// <summary>Initializes a new instance of the <see cref="BlackKarasinski"/> class.</summary>
    /// <param name="termStructure">The curve the model fits.</param>
    /// <param name="a">The mean reversion α, positive.</param>
    /// <param name="sigma">The volatility σ, positive.</param>
    /// <exception cref="ArgumentNullException"><paramref name="termStructure"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="a"/> or <paramref name="sigma"/> isn't positive.</exception>
    public BlackKarasinski(YieldTermStructure termStructure, double a = 0.1, double sigma = 0.1)
        : base(2)
    {
        ArgumentNullException.ThrowIfNull(termStructure);
        TermStructure = termStructure;
        SetArgument(0, new ConstantParameter(a, new PositiveConstraint(), nameof(a)));
        SetArgument(1, new ConstantParameter(sigma, new PositiveConstraint(), nameof(sigma)));
    }

    /// <summary>Gets the curve the model fits.</summary>
    public YieldTermStructure TermStructure { get; }

    private double A => Arguments[0].Evaluate(0.0);

    private double Sigma => Arguments[1].Evaluate(0.0);

    /// <summary>The model's short-rate dynamics, on the fitting of a 50-step tree over the curve's maximum time.</summary>
    /// <returns>A new dynamics object, whose fitting parameter is set at that tree's times only.</returns>
    /// <exception cref="NanoQLException">A step's fitting value can't be solved.</exception>
    public override OneFactorModelShortRateDynamics Dynamics()
    {
        // Calibrate fitting parameter to term structure
        int steps = 50;
        TermStructureFittingParameter fitting = FittedTree(new TimeGrid(TermStructure.MaxTime, steps)).Fitting;
        return new BlackKarasinskiDynamics(fitting, A, Sigma);
    }

    /// <summary>Builds the model's trinomial tree on a time grid, its own fitting parameter fitted to the curve.</summary>
    /// <param name="grid">The time grid.</param>
    /// <returns>The fitted <see cref="OneFactorModelShortRateTree"/>, immutable once returned.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
    /// <exception cref="NanoQLException">A step's fitting value can't be solved.</exception>
    public override Lattice Tree(TimeGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return FittedTree(grid).Tree;
    }

    // QuantLib's tree(): the tree and the fitting it fills, a new one for each tree.
    private (OneFactorModelShortRateTree Tree, TermStructureFittingParameter Fitting) FittedTree(TimeGrid grid)
    {
        var fitting = new TermStructureFittingParameter(TermStructure);
        var numericDynamics = new BlackKarasinskiDynamics(fitting, A, Sigma);
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
            var finder = new Helper(i, xMin, dx, discountBond, numericTree);
            Brent s1d = new Brent().SetMaxEvaluations(1000);
            value = s1d.Solve(finder, 1e-7, value, vMin, vMax);
            impl.Set(grid[i], value);
        }

        return (numericTree, fitting);
    }

    // The short-rate dynamics: r = exp(phi(t) + x), x an Ornstein-Uhlenbeck process.
    private sealed class BlackKarasinskiDynamics(Parameter fitting, double alpha, double sigma)
        : OneFactorModelShortRateDynamics(new OrnsteinUhlenbeckProcess(alpha, sigma))
    {
        public override double Variable(double t, double r) => Math.Log(r) - fitting.Evaluate(t);

        public override double ShortRate(double t, double variable) => Math.Exp(variable + fitting.Evaluate(t));
    }

    // The function the fitting solves at a step: the curve's discount less the step's state prices, each discounted
    // at the node's short rate exp(theta + x). Its construction stores the step's state prices.
    private readonly struct Helper : ISolver1DFunction
    {
        private readonly int size;
        private readonly double dt;
        private readonly double xMin, dx;
        private readonly ImmutableArray<double> statePrices;
        private readonly double discountBondPrice;

        public Helper(int i, double xMin, double dx, double discountBondPrice, OneFactorModelShortRateTree tree)
        {
            size = tree.Size(i);
            dt = tree.TimeGrid.Dt(i);
            this.xMin = xMin;
            this.dx = dx;
            tree.ComputeStatePrices(i);
            statePrices = tree.StatePrices(i);
            this.discountBondPrice = discountBondPrice;
        }

        public double Evaluate(double theta)
        {
            double value = discountBondPrice;
            double x = xMin;
            for (int j = 0; j < size; j++)
            {
                double discount = Math.Exp(-Math.Exp(theta + x) * dt);
                value -= statePrices[j] * discount;
                x += dx;
            }

            return value;
        }
    }
}
