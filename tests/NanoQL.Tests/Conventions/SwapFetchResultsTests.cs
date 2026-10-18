// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="FixedVsFloatingSwap.FetchResults"/> on the results an engine returns: a fair rate and spread the engine
/// gives are kept, and only one it leaves null is derived from the NPV and its leg's BPS
/// (<c>fixedvsfloatingswap.cpp:185-209</c>). A null NPV derives neither, where QuantLib computes them from
/// <c>Null&lt;Real&gt;()</c>.
/// </summary>
public class SwapFetchResultsTests
{
    private const double BasisPoint = 1.0e-4;
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly FlatForward Curve = new(Today, 0.031, new Actual365Fixed());

    private static VanillaSwap Swap() => new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(Curve))
        .WithFixedRate(0.03)
        .WithFloatingLegSpread(0.001)
        .ToVanillaSwap(Context);

    [Fact]
    public void TheEnginesFairRateAndSpreadAreKept()
    {
        VanillaSwap swap = Swap();

        // An engine's own fair rate and spread, kept (fixedvsfloatingswap.cpp:190-193), though the NPV and BPS would
        // derive others.
        FixedVsFloatingSwapResults kept = swap.FetchResults(new FixedVsFloatingSwapResults
        {
            NPV = 100.0,
            LegBPS = [-50.0, 48.0],
            FairRate = 0.0123,
            FairSpread = 0.0045,
        });
        Assert.Equal(0.0123, kept.FairRate);
        Assert.Equal(0.0045, kept.FairSpread);

        // None given, by a fixed-vs-floating engine or by a swap engine: each derived from the NPV and its leg's BPS.
        double derivedRate = swap.FixedRate - (100.0 / (-50.0 / BasisPoint));
        double derivedSpread = swap.Spread - (100.0 / (48.0 / BasisPoint));
        FixedVsFloatingSwapResults none = swap.FetchResults(new FixedVsFloatingSwapResults
        {
            NPV = 100.0,
            LegBPS = [-50.0, 48.0],
        });
        Assert.Equal(derivedRate, none.FairRate);
        Assert.Equal(derivedSpread, none.FairSpread);
        FixedVsFloatingSwapResults fromSwapEngine = swap.FetchResults(new SwapResults
        {
            NPV = 100.0,
            LegBPS = [-50.0, 48.0],
        });
        Assert.Equal(derivedRate, fromSwapEngine.FairRate);
        Assert.Equal(derivedSpread, fromSwapEngine.FairSpread);

        // One given, the other derived.
        FixedVsFloatingSwapResults rateOnly = swap.FetchResults(new FixedVsFloatingSwapResults
        {
            NPV = 100.0,
            LegBPS = [-50.0, 48.0],
            FairRate = 0.0123,
        });
        Assert.Equal(0.0123, rateOnly.FairRate);
        Assert.Equal(derivedSpread, rateOnly.FairSpread);

        // The overnight-indexed swap keeps them too, in its own results record.
        OvernightIndexedSwap ois = new MakeOIS(new Period(2, TimeUnit.Years), new Estr(Curve))
            .WithFixedRate(0.03)
            .ToOvernightIndexedSwap(Context);
        OvernightIndexedSwapResults oisKept = ois.FetchResults(new OvernightIndexedSwapResults
        {
            NPV = 100.0,
            LegBPS = [-50.0, 48.0],
            FairRate = 0.0123,
            FairSpread = 0.0045,
        });
        Assert.Equal(0.0123, oisKept.FairRate);
        Assert.Equal(0.0045, oisKept.FairSpread);
    }

    [Fact]
    public void ANullNpvDerivesNoFairRateOrSpread()
    {
        VanillaSwap swap = Swap();

        // Leg BPS without an NPV: QuantLib's fixedRate_ - NPV_/(legBPS_[0]/basisPoint) reads NPV_ as Null<Real>()
        // (fixedvsfloatingswap.cpp:199-207); nanoql's null NPV leaves both null.
        FixedVsFloatingSwapResults fixedVsFloating = swap.FetchResults(new FixedVsFloatingSwapResults
        {
            LegBPS = [-50.0, 48.0],
        });
        Assert.Null(fixedVsFloating.NPV);
        Assert.Null(fixedVsFloating.FairRate);
        Assert.Null(fixedVsFloating.FairSpread);
        FixedVsFloatingSwapResults fromSwapEngine = swap.FetchResults(new SwapResults { LegBPS = [-50.0, 48.0] });
        Assert.Null(fromSwapEngine.FairRate);
        Assert.Null(fromSwapEngine.FairSpread);
    }
}
