// SPDX-FileCopyrightText: Copyright (C) 2008 Frank Hövermann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/processes/extendedblackscholesprocess.hpp ql/experimental/processes/extendedblackscholesprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// A Black-Scholes-Merton process on its Black volatility, stepped by a built-in scheme: QuantLib's
/// <c>ExtendedBlackScholesMertonProcess</c>.
/// </summary>
/// <remarks>
/// <para>
/// The diffusion is the Black volatility at the time and the asset value, extrapolated, and the drift the curves' forward
/// rates over the next 1e-4 of a year less half its square, as QuantLib's. The step is the scheme's, in the log price.
/// </para>
/// <para>
/// Three fixes (the S-xrt plan's XRR5 (xxvi) and XRR7; <c>Divergences/ExtendedBlackScholesProcessTests.cs</c>). Milstein's
/// correction is the log price's, ½σ·∂σ/∂ln S·(ΔW² − 1)Δt, ∂σ/∂ln S the Black volatility's central difference over a
/// log strike of ±1e-4, 0 on a strike-independent volatility, where the step is the log-Euler one; QuantLib adds the
/// asset's own, ½σ²(ΔW² − 1)Δt, to the log price, its mean 1.25e-3 high a step at Δt 0.25 and σ 0.4. Euler's step, and
/// the predictor-corrector's predictor, are the log-Euler step on the process's drift and diffusion; QuantLib's centre
/// on the asset's mean, x₀e^((r−q)Δt), which lacks the −σ²Δt/2 (2.02e-2 high a step at Δt 0.25 and σ 0.4) and refuses a
/// strike-dependent volatility ("not implemented"). The corrector is QuantLib's on a strike-independent volatility. On
/// one that depends on the strike, QuantLib's corrector, its diffusion the mean of the two ends' on the same increment,
/// is Heun's scheme, which converges to the Stratonovich equation, ½σ·∂σ/∂ln S above the log drift: on a SABR smile its
/// error stops falling with the step, E[S_T] 0.09% low; nanoql's takes each end's drift less ½σ·∂σ/∂ln S, the Itô
/// predictor-corrector, which converges at order 1 there and keeps QuantLib's bits where ∂σ/∂ln S is 0 (the S-xrt run's
/// B3-1, ruled 2026-10-29). On flat curves and a flat volatility all three are then the exact step, and on a smile they
/// converge to the process at their orders, Euler's ½ and the others' 1.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class ExtendedBlackScholesMertonProcess : GeneralizedBlackScholesProcess
{
    // The log-strike bump of Milstein's ∂σ/∂ln S.
    private const double LogStrikeBump = 1e-4;

    /// <summary>Initializes a new instance of the <see cref="ExtendedBlackScholesMertonProcess"/> class.</summary>
    /// <param name="x0">The spot.</param>
    /// <param name="dividendTS">The dividend yield curve.</param>
    /// <param name="riskFreeTS">The risk-free curve.</param>
    /// <param name="blackVolTS">The Black volatility.</param>
    /// <param name="d">The base process's discretization, Euler's by default.</param>
    /// <param name="evolDisc">The step's scheme, Milstein's by default.</param>
    /// <exception cref="ArgumentNullException">A curve or the volatility is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="evolDisc"/> isn't a member of its enum.</exception>
    public ExtendedBlackScholesMertonProcess(
        double x0,
        YieldTermStructure dividendTS,
        YieldTermStructure riskFreeTS,
        BlackVolTermStructure blackVolTS,
        StochasticProcess1DDiscretization? d = null,
        ExtendedBlackScholesMertonProcessDiscretization evolDisc = ExtendedBlackScholesMertonProcessDiscretization.Milstein)
        : base(x0, dividendTS, riskFreeTS, blackVolTS, d ?? new EulerDiscretization())
    {
        EnumArguments.Check(evolDisc, nameof(evolDisc));
        Scheme = evolDisc;
    }

    /// <summary>Gets the step's scheme.</summary>
    public ExtendedBlackScholesMertonProcessDiscretization Scheme { get; }

    /// <inheritdoc/>
    /// <remarks>The forward rates over the next 1e-4 of a year less half the diffusion's square.</remarks>
    public override double Drift(double t, double x)
    {
        double sigma = Diffusion(t, x);

        // we could be more anticipatory if we know the right dt
        // for which the drift will be used
        double t1 = t + 0.0001;
        return Rate(t, t1) - (0.5 * sigma * sigma);
    }

    /// <inheritdoc/>
    /// <remarks>The Black volatility at the time and the asset value, extrapolated.</remarks>
    public override double Diffusion(double t, double x) => BlackVolatility.BlackVol(t, x, true);

    /// <inheritdoc/>
    /// <remarks>The scheme's step in the log price (XRR5 (xxvi), XRR7).</remarks>
    public override double Evolve(double t0, double x0, double dt, double dw)
    {
        switch (Scheme)
        {
            case ExtendedBlackScholesMertonProcessDiscretization.Milstein:
            {
                // Milstein scheme, its correction the log price's
                double sigma = Diffusion(t0, x0);
                double dSigma = DSigma(t0, x0);
                return Apply(
                    x0,
                    (Drift(t0, x0) * dt) + (0.5 * sigma * dSigma * ((dw * dw) - 1) * dt) + (sigma * Math.Sqrt(dt) * dw));
            }

            case ExtendedBlackScholesMertonProcessDiscretization.Euler:
                // the log-Euler step
                return LogEuler(t0, x0, dt, dw);

            default:
            {
                // Predictor-Corrector scheme with equal weighting, the predictor the log-Euler step, each end's drift
                // less half σ·∂σ/∂ln S, which the averaged diffusion adds (0 on a strike-independent volatility)
                double predictor = LogEuler(t0, x0, dt, dw);
                double t1 = t0 + 0.0001;
                double sigma0 = Diffusion(t0, x0);
                double sigma1 = Diffusion(t0 + dt, predictor);
                double rate0 = Rate(t0, t1) - (0.5 * Math.Pow(sigma0, 2)) - (0.5 * sigma0 * DSigma(t0, x0));
                double rate1 = Rate(t0 + dt, t1 + dt) - (0.5 * Math.Pow(sigma1, 2))
                    - (0.5 * sigma1 * DSigma(t0 + dt, predictor));
                double driftterm = (0.5 * rate1) + (0.5 * rate0);
                double diffusionterm = 0.5 * (sigma1 + sigma0);
                return Apply(x0, (driftterm * dt) + (diffusionterm * Math.Sqrt(dt) * dw));
            }
        }
    }

    // ∂σ/∂ln S, the Black volatility's central difference over a log strike of ±1e-4.
    private double DSigma(double t, double x) =>
        (BlackVolatility.BlackVol(t, x * Math.Exp(LogStrikeBump), true)
            - BlackVolatility.BlackVol(t, x * Math.Exp(-LogStrikeBump), true))
        / (2.0 * LogStrikeBump);

    private double LogEuler(double t0, double x0, double dt, double dw) =>
        Apply(x0, (Drift(t0, x0) * dt) + (Diffusion(t0, x0) * Math.Sqrt(dt) * dw));

    // The risk-free less the dividend forward rate, the dividend curve read on the risk-free curve's clock as the base
    // process reads it.
    private double Rate(double t1, double t2) =>
        RiskFreeRate.ContinuousForwardRate(t1, t2, true)
        - (DividendYield.SharesTimes(RiskFreeRate)
            ? DividendYield.ContinuousForwardRate(t1, t2, true)
            : DividendYield.ContinuousForwardRateAt(RiskFreeRate, t1, t2, true));
}
