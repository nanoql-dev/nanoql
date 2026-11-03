// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/batesprocess.hpp ql/processes/batesprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.TermStructures;
using Vector = NanoQL.Numerics.Vector;

namespace NanoQL.Processes;

/// <summary>
/// The Bates process: the Heston square-root stochastic-volatility process with log-normal jumps,
/// dS = (r − q − λm)S dt + √v S dW₁ + (e^J − 1)S dN, dv = κ(θ − v) dt + σ√v dW₂, dW₁dW₂ = ρ dt, N a Poisson process of
/// intensity λ and J normal with mean ν and standard deviation δ, m = e^{ν + δ²/2} − 1.
/// </summary>
/// <remarks>
/// Immutable, a <see cref="HestonProcess"/> as QuantLib's derives from it: its drift less λm, two more factors, the
/// first drawing the step's number of jumps through the inverse Poisson distribution and the second their sizes, and
/// QuantLib's default discretization, full truncation, where the Heston process's is the quadratic-exponential
/// martingale scheme. The in-place
/// <see cref="Evolve(double, ReadOnlySpan{double}, double, ReadOnlySpan{double}, Span{double})"/> allocates nothing
/// where the Heston step doesn't.
/// </remarks>
public sealed class BatesProcess : HestonProcess
{
    // the standard normal's cumulative distribution, QuantLib's member
    private static readonly CumulativeNormalDistribution CumNormalDist = new();

    private readonly double m;

    /// <summary>Initializes a new instance of the <see cref="BatesProcess"/> class.</summary>
    /// <param name="riskFreeRate">The risk-free curve.</param>
    /// <param name="dividendYield">The dividend curve.</param>
    /// <param name="s0">The underlying's value.</param>
    /// <param name="v0">The variance's value.</param>
    /// <param name="kappa">The variance's mean-reversion speed κ.</param>
    /// <param name="theta">The variance's long-run mean θ.</param>
    /// <param name="sigma">The variance's volatility σ.</param>
    /// <param name="rho">The correlation ρ of the two Brownian motions.</param>
    /// <param name="lambda">The jumps' intensity λ.</param>
    /// <param name="nu">The log jump size's mean ν.</param>
    /// <param name="delta">The log jump size's standard deviation δ.</param>
    /// <param name="d">The discretization the Heston part steps by; full truncation by default.</param>
    /// <exception cref="ArgumentNullException">A curve is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="d"/> isn't a member of its enum.</exception>
    public BatesProcess(
        YieldTermStructure riskFreeRate,
        YieldTermStructure dividendYield,
        double s0,
        double v0,
        double kappa,
        double theta,
        double sigma,
        double rho,
        double lambda,
        double nu,
        double delta,
        HestonProcessDiscretization d = HestonProcessDiscretization.FullTruncation)
        : base(riskFreeRate, dividendYield, s0, v0, kappa, theta, sigma, rho, d)
    {
        Lambda = lambda;
        Delta = delta;
        Nu = nu;
        m = Math.Exp(nu + (0.5 * delta * delta)) - 1;
    }

    /// <inheritdoc/>
    /// <remarks>The Heston part's factors and two more: the jumps' number and their sizes.</remarks>
    public override int Factors => base.Factors + 2;

    /// <summary>Gets the jumps' intensity λ.</summary>
    public double Lambda { get; }

    /// <summary>Gets the log jump size's mean ν.</summary>
    public double Nu { get; }

    /// <summary>Gets the log jump size's standard deviation δ.</summary>
    public double Delta { get; }

    /// <inheritdoc/>
    public override Vector Drift(double t, Vector x)
    {
        Vector retVal = base.Drift(t, x);
        retVal[0] -= Lambda * m;
        return retVal;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The Heston step refuses.</exception>
    public override Vector Evolve(double t0, Vector x0, double dt, Vector dw)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dw);
        int hestonFactors = base.Factors;

        double n = Jumps(dw[hestonFactors], dt);
        Vector retVal = base.Evolve(t0, x0, dt, dw);
        retVal[0] *= Math.Exp((-Lambda * m * dt) + (Nu * n) + (Delta * Math.Sqrt(n) * dw[hestonFactors + 1]));

        return retVal;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The Heston step refuses.</exception>
    public override void Evolve(double t0, ReadOnlySpan<double> x0, double dt, ReadOnlySpan<double> dw, Span<double> result)
    {
        int hestonFactors = base.Factors;

        double n = Jumps(dw[hestonFactors], dt);
        base.Evolve(t0, x0, dt, dw, result);
        result[0] *= Math.Exp((-Lambda * m * dt) + (Nu * n) + (Delta * Math.Sqrt(n) * dw[hestonFactors + 1]));
    }

    // the step's number of jumps: the inverse Poisson distribution of λ·dt at the draw's probability, clamped
    private double Jumps(double draw, double dt)
    {
        double p = CumNormalDist.Evaluate(draw);
        if (p < 0.0)
        {
            p = 0.0;
        }
        else if (p >= 1.0)
        {
            p = 1.0 - NumericLimits.QL_EPSILON;
        }

        return new InverseCumulativePoisson(Lambda * dt).Evaluate(p);
    }
}
