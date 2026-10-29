// SPDX-FileCopyrightText: Copyright (C) 2010 Michael Heckl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/processes/vegastressedblackscholesprocess.hpp ql/experimental/processes/vegastressedblackscholesprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// A Black-Scholes process whose volatility is stressed by a level inside a box of times and asset values: QuantLib's
/// <c>VegaStressedBlackScholesProcess</c>.
/// </summary>
/// <remarks>
/// <para>
/// The diffusion inside the box [lower time, upper time] × [lower asset, upper asset], ends included, is the process's
/// plus the stress level. QuantLib's setters, which change the process in place, return a copy here.
/// </para>
/// <para>
/// With a nonzero stress the standard deviation, the variance and the step go through the discretization, QuantLib's own
/// route for a strike-dependent volatility, so the stressed diffusion reaches them; QuantLib's take the exact forms from
/// the term structures on a strike-independent volatility, which read no diffusion, so the stress never reaches a step
/// there (the S-xrt plan's XRR5 (xxvii); <c>Divergences/VegaStressedProcessTests.cs</c>). The expectation keeps
/// QuantLib's exact value from the curves, the mean of the asset reading no volatility, where the discretized route
/// refuses ("not implemented"). With no stress the process is QuantLib's.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class VegaStressedBlackScholesProcess : GeneralizedBlackScholesProcess
{
    /// <summary>Initializes a new instance of the <see cref="VegaStressedBlackScholesProcess"/> class.</summary>
    /// <param name="x0">The spot.</param>
    /// <param name="dividendTS">The dividend yield curve.</param>
    /// <param name="riskFreeTS">The risk-free curve.</param>
    /// <param name="blackVolTS">The Black volatility.</param>
    /// <param name="lowerTimeBorderForStressTest">The box's earliest time.</param>
    /// <param name="upperTimeBorderForStressTest">The box's latest time.</param>
    /// <param name="lowerAssetBorderForStressTest">The box's lowest asset value.</param>
    /// <param name="upperAssetBorderForStressTest">The box's highest asset value.</param>
    /// <param name="stressLevel">The stress added to the volatility inside the box.</param>
    /// <param name="disc">The discretization, Euler's by default.</param>
    /// <exception cref="ArgumentNullException">A curve or the volatility is null.</exception>
    public VegaStressedBlackScholesProcess(
        double x0,
        YieldTermStructure dividendTS,
        YieldTermStructure riskFreeTS,
        BlackVolTermStructure blackVolTS,
        double lowerTimeBorderForStressTest = 0,
        double upperTimeBorderForStressTest = 1000000,
        double lowerAssetBorderForStressTest = 0,
        double upperAssetBorderForStressTest = 1000000,
        double stressLevel = 0,
        StochasticProcess1DDiscretization? disc = null)
        : base(x0, dividendTS, riskFreeTS, blackVolTS, disc ?? new EulerDiscretization())
    {
        LowerTimeBorderForStressTest = lowerTimeBorderForStressTest;
        UpperTimeBorderForStressTest = upperTimeBorderForStressTest;
        LowerAssetBorderForStressTest = lowerAssetBorderForStressTest;
        UpperAssetBorderForStressTest = upperAssetBorderForStressTest;
        StressLevel = stressLevel;
        Discretization = disc ?? new EulerDiscretization();
    }

    /// <summary>Gets the box's earliest time: QuantLib's <c>getLowerTimeBorderForStressTest</c>.</summary>
    public double LowerTimeBorderForStressTest { get; private init; }

    /// <summary>Gets the box's latest time: QuantLib's <c>getUpperTimeBorderForStressTest</c>.</summary>
    public double UpperTimeBorderForStressTest { get; private init; }

    /// <summary>Gets the box's lowest asset value: QuantLib's <c>getLowerAssetBorderForStressTest</c>.</summary>
    public double LowerAssetBorderForStressTest { get; private init; }

    /// <summary>Gets the box's highest asset value: QuantLib's <c>getUpperAssetBorderForStressTest</c>.</summary>
    public double UpperAssetBorderForStressTest { get; private init; }

    /// <summary>Gets the stress added inside the box: QuantLib's <c>getStressLevel</c>.</summary>
    public double StressLevel { get; private init; }

    private StochasticProcess1DDiscretization Discretization { get; }

    /// <summary>A copy with another earliest time of the box: QuantLib's <c>setLowerTimeBorderForStressTest</c>.</summary>
    /// <param name="ltb">The time.</param>
    /// <returns>The copy.</returns>
    public VegaStressedBlackScholesProcess SetLowerTimeBorderForStressTest(double ltb) =>
        With(ltb, UpperTimeBorderForStressTest, LowerAssetBorderForStressTest, UpperAssetBorderForStressTest, StressLevel);

    /// <summary>A copy with another latest time of the box: QuantLib's <c>setUpperTimeBorderForStressTest</c>.</summary>
    /// <param name="utb">The time.</param>
    /// <returns>The copy.</returns>
    public VegaStressedBlackScholesProcess SetUpperTimeBorderForStressTest(double utb) =>
        With(LowerTimeBorderForStressTest, utb, LowerAssetBorderForStressTest, UpperAssetBorderForStressTest, StressLevel);

    /// <summary>A copy with another lowest asset value of the box: QuantLib's
    /// <c>setLowerAssetBorderForStressTest</c>.</summary>
    /// <param name="lab">The asset value.</param>
    /// <returns>The copy.</returns>
    public VegaStressedBlackScholesProcess SetLowerAssetBorderForStressTest(double lab) =>
        With(LowerTimeBorderForStressTest, UpperTimeBorderForStressTest, lab, UpperAssetBorderForStressTest, StressLevel);

    /// <summary>A copy with another highest asset value of the box: QuantLib's
    /// <c>setUpperAssetBorderForStressTest</c>.</summary>
    /// <param name="uab">The asset value.</param>
    /// <returns>The copy.</returns>
    public VegaStressedBlackScholesProcess SetUpperAssetBorderForStressTest(double uab) =>
        With(LowerTimeBorderForStressTest, UpperTimeBorderForStressTest, LowerAssetBorderForStressTest, uab, StressLevel);

    /// <summary>A copy with another stress level: QuantLib's <c>setStressLevel</c>.</summary>
    /// <param name="sl">The stress level.</param>
    /// <returns>The copy.</returns>
    public VegaStressedBlackScholesProcess SetStressLevel(double sl) =>
        With(LowerTimeBorderForStressTest, UpperTimeBorderForStressTest, LowerAssetBorderForStressTest,
            UpperAssetBorderForStressTest, sl);

    /// <inheritdoc/>
    /// <remarks>The process's diffusion, plus the stress level inside the box.</remarks>
    public override double Diffusion(double t, double x)
    {
        if (LowerTimeBorderForStressTest <= t && t <= UpperTimeBorderForStressTest
            && LowerAssetBorderForStressTest <= x && x <= UpperAssetBorderForStressTest)
        {
            return base.Diffusion(t, x) + StressLevel;
        }

        return base.Diffusion(t, x);
    }

    /// <inheritdoc/>
    /// <remarks>Through the discretization with a nonzero stress (XRR5 (xxvii)); QuantLib's otherwise.</remarks>
    public override double StdDeviation(double t0, double x0, double dt) =>
        StressLevel != 0.0 ? Discretization1D().Diffusion(this, t0, x0, dt) : base.StdDeviation(t0, x0, dt);

    /// <inheritdoc/>
    /// <remarks>Through the discretization with a nonzero stress (XRR5 (xxvii)); QuantLib's otherwise.</remarks>
    public override double Variance(double t0, double x0, double dt) =>
        StressLevel != 0.0 ? Discretization1D().Variance(this, t0, x0, dt) : base.Variance(t0, x0, dt);

    /// <inheritdoc/>
    /// <remarks>The discretization's step with a nonzero stress (XRR5 (xxvii)); QuantLib's otherwise.</remarks>
    public override double Evolve(double t0, double x0, double dt, double dw) =>
        StressLevel != 0.0
            ? Apply(x0, Discretization1D().Drift(this, t0, x0, dt) + (StdDeviation(t0, x0, dt) * dw))
            : base.Evolve(t0, x0, dt, dw);

    private VegaStressedBlackScholesProcess With(double ltb, double utb, double lab, double uab, double sl) =>
        new(X0, DividendYield, RiskFreeRate, BlackVolatility, ltb, utb, lab, uab, sl, Discretization);
}
