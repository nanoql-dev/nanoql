// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/hestonslvprocess.hpp ql/processes/hestonslvprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Processes;

/// <summary>
/// The Heston stochastic-local volatility process: the Heston process's spot with its volatility √v scaled by a
/// leverage function L(t, S), and its variance's volatility scaled by a mixing factor.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: QuantLib's process re-reads its Heston process's parameters when notified; here they are read at
/// construction. Its step, the quadratic-exponential scheme for the variance and the leverage at the step's start, runs
/// in place without allocating, the allocating step its values bit for bit.
/// </para>
/// <para>
/// Its times are the Heston process's, the curves read as that process reads them, and the leverage function at the
/// instants those times denote, on its own clock; QuantLib's reads where the structures share the risk-free curve's
/// times.
/// </para>
/// </remarks>
public sealed class HestonSLVProcess : StochasticProcess
{
    private static readonly CumulativeNormalDistribution CumNormal = new();

    private readonly double kappa;
    private readonly double theta;
    private readonly double sigma;
    private readonly double rho;
    private readonly double v0;
    private readonly double mixingFactor;
    private readonly double mixedSigma;
    private readonly HestonProcess hestonProcess;
    private readonly LocalVolTermStructure leverageFct;

    /// <summary>Initializes a new instance of the <see cref="HestonSLVProcess"/> class.</summary>
    /// <param name="hestonProcess">The Heston process.</param>
    /// <param name="leverageFct">The leverage function.</param>
    /// <param name="mixingFactor">The factor scaling the variance's volatility σ.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public HestonSLVProcess(HestonProcess hestonProcess, LocalVolTermStructure leverageFct, double mixingFactor = 1.0)
    {
        ArgumentNullException.ThrowIfNull(hestonProcess);
        ArgumentNullException.ThrowIfNull(leverageFct);
        this.hestonProcess = hestonProcess;
        this.leverageFct = leverageFct;
        this.mixingFactor = mixingFactor;
        v0 = hestonProcess.V0;
        kappa = hestonProcess.Kappa;
        theta = hestonProcess.Theta;
        sigma = hestonProcess.Sigma;
        rho = hestonProcess.Rho;
        mixedSigma = mixingFactor * sigma;
    }

    /// <inheritdoc/>
    public override int Size => 2;

    /// <inheritdoc/>
    public override int Factors => 2;

    /// <summary>Gets the initial variance v₀.</summary>
    public double V0 => v0;

    /// <summary>Gets the correlation ρ.</summary>
    public double Rho => rho;

    /// <summary>Gets the variance's mean-reversion speed κ.</summary>
    public double Kappa => kappa;

    /// <summary>Gets the variance's mean-reversion level θ.</summary>
    public double Theta => theta;

    /// <summary>Gets the variance's volatility σ, before the mixing factor.</summary>
    public double Sigma => sigma;

    /// <summary>Gets the mixing factor.</summary>
    public double MixingFactor => mixingFactor;

    /// <summary>Gets the leverage function.</summary>
    public LocalVolTermStructure LeverageFct => leverageFct;

    /// <summary>Gets the spot.</summary>
    public double S0 => hestonProcess.S0;

    /// <summary>Gets the dividend curve.</summary>
    public YieldTermStructure DividendYield => hestonProcess.DividendYield;

    /// <summary>Gets the risk-free curve.</summary>
    public YieldTermStructure RiskFreeRate => hestonProcess.RiskFreeRate;

    /// <inheritdoc/>
    public override Vector InitialValues() => hestonProcess.InitialValues();

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="result"/> is shorter than the process's size.</exception>
    public override void InitialValues(Span<double> result) => hestonProcess.InitialValues(result);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="x0"/> or <paramref name="dx"/> holds fewer values than the state, where QuantLib reads past its
    /// end.
    /// </exception>
    public override Vector Apply(Vector x0, Vector dx) => hestonProcess.Apply(x0, dx);

    /// <inheritdoc/>
    /// <remarks>The spot's drift r − q − ½(√v·L)², its volatility at least 1e-8, and the variance's κ(θ − v).</remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="x"/> holds fewer values than the state, where QuantLib reads past its end.
    /// </exception>
    public override Vector Drift(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        CheckState(x.Size, nameof(x));
        double vol = Max(1e-8, Math.Sqrt(x[1]) * Leverage(t, x[0]));

        return new Vector(
        [
            hestonProcess.RiskFreeForwardRate(t, t) - hestonProcess.DividendRate(t, t) - (0.5 * vol * vol),
            kappa * (theta - x[1]),
        ]);
    }

    /// <inheritdoc/>
    /// <remarks>[[√v·L, 0], [ρσ_m√v, √(1 − ρ²)σ_m√v]], the spot's volatility at least 1e-8.</remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="x"/> holds fewer values than the state, where QuantLib reads past its end.
    /// </exception>
    public override Matrix Diffusion(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        CheckState(x.Size, nameof(x));
        double vol = Max(1e-8, Math.Sqrt(x[1]) * Leverage(t, x[0]));

        double sigma2 = mixedSigma * Math.Sqrt(x[1]);
        double sqrhov = Math.Sqrt(1.0 - (rho * rho));

        var tmp = new Matrix(2, 2);
        tmp[0, 0] = vol;
        tmp[0, 1] = 0.0;
        tmp[1, 0] = rho * sigma2;
        tmp[1, 1] = sqrhov * sigma2;
        return tmp;
    }

    /// <inheritdoc/>
    public override Vector Evolve(double t0, Vector x0, double dt, Vector dw)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dw);
        var retVal = new Vector(2);
        Evolve(t0, x0.AsSpan(), dt, dw.AsSpan(), retVal.AsSpan());
        return retVal;
    }

    /// <inheritdoc/>
    /// <remarks>The variance by the quadratic-exponential scheme, the spot by the leverage at the step's start.</remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="x0"/> holds fewer values than the state, or <paramref name="dw"/> fewer increments than the
    /// process's factors, where QuantLib reads past their end; or <paramref name="result"/> is shorter than the state.
    /// </exception>
    public override void Evolve(double t0, ReadOnlySpan<double> x0, double dt, ReadOnlySpan<double> dw, Span<double> result)
    {
        CheckState(x0.Length, nameof(x0));
        CheckIncrements(dw.Length, nameof(dw));
        CheckResult(result.Length);
        double ex = Math.Exp(-kappa * dt);

        double m = theta + ((x0[1] - theta) * ex);
        double s2 = (x0[1] * mixedSigma * mixedSigma * ex / kappa * (1 - ex))
            + (theta * mixedSigma * mixedSigma / (2 * kappa) * (1 - ex) * (1 - ex));
        double psi = s2 / (m * m);

        double v1;
        if (psi < 1.5)
        {
            double b2 = (2 / psi) - 1 + Math.Sqrt(2 / psi * ((2 / psi) - 1));
            double b = Math.Sqrt(b2);
            double a = m / (1 + b2);

            v1 = a * (b + dw[1]) * (b + dw[1]);
        }
        else
        {
            double p = (psi - 1) / (psi + 1);
            double beta = (1 - p) / m;
            double u = CumNormal.Evaluate(dw[1]);

            v1 = (u <= p) ? 0.0 : Math.Log((1 - p) / (1 - u)) / beta;
        }

        double mu = hestonProcess.ForwardDriftInPlace(t0, dt);

        double rho1 = Math.Sqrt(1 - (rho * rho));

        double l0 = Leverage(t0, x0[0]);
        double vAverage = 0.5 * (x0[1] + v1) * l0 * l0;

        result[0] = x0[0] * Math.Exp((mu * dt) - (0.5 * vAverage * dt)
            + (rho / mixedSigma * l0 * (v1 - (kappa * theta * dt) + (0.5 * (x0[1] + v1) * kappa * dt) - x0[1]))
            + (rho1 * Math.Sqrt(vAverage * dt) * dw[0]));
        result[1] = v1;
    }

    /// <inheritdoc/>
    public override double Time(Date date) => hestonProcess.Time(date);

    // The model's clock, the Heston process's (HestonProcess.ModelClock): the risk-free curve where the dividend curve
    // isn't dated before it.
    internal TermStructure ModelClock() => hestonProcess.ModelClock();

    // The process on an engine's clock: over the Heston process on the clock, the leverage read at the instants its
    // times denote; the process itself where the clock shares its times.
    internal HestonSLVProcess OnClock(TermStructure clock)
    {
        HestonProcess onClock = hestonProcess.OnClock(clock);
        return ReferenceEquals(onClock, hestonProcess) ? this : new HestonSLVProcess(onClock, leverageFct, mixingFactor);
    }

    // std::max
    private static double Max(double a, double b) => a < b ? b : a;

    // the leverage at a spot and a time on the process's clock, read at the instant the time denotes
    private double Leverage(double t, double spot) =>
        leverageFct.LocalVol(FdmHestonLocalVolatilityVarianceMesher.LeverageTime(leverageFct, hestonProcess.TimeClock, t), spot, true);
}
