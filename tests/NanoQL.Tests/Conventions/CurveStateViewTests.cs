// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The working types' views and copies: a curve state's vector read is a view of its own buffer, valid until the state
/// is next set, or the same read is made with another argument (two spannings' constant-maturity rates share QuantLib's
/// one buffer); <see cref="CurveState.Clone"/> is deep; an evolver's current state is its own curve state, the same
/// object from step to step.
/// </summary>
public class CurveStateViewTests
{
    private static readonly double[] RateTimes = [0.5, 1.0, 1.5, 2.0];

    [Fact]
    public void AViewIsValidUntilTheStateIsNextSet()
    {
        var cs = new LMMCurveState(RateTimes);
        cs.SetOnForwardRates([0.03, 0.04, 0.05]);
        ReadOnlySpan<double> view = cs.ForwardRates();
        Assert.Equal(0.04, view[1]);
        cs.SetOnForwardRates([0.06, 0.07, 0.08]);
        Assert.Equal(0.07, view[1]);
    }

    [Fact]
    public void ALazyReadIsResetAtEachSet()
    {
        var cs = new LMMCurveState(RateTimes);
        cs.SetOnForwardRates([0.03, 0.04, 0.05]);
        _ = cs.CoterminalSwapAnnuity(3, 0);
        cs.SetOnForwardRates([0.06, 0.07, 0.08]);
        var fresh = new LMMCurveState(RateTimes);
        fresh.SetOnForwardRates([0.06, 0.07, 0.08]);
        Assert.Equal(fresh.CoterminalSwapAnnuity(3, 0), cs.CoterminalSwapAnnuity(3, 0));
        Assert.Equal(fresh.CoterminalSwapRate(1), cs.CoterminalSwapRate(1));

        cs.SetOnDiscountRatios([1.1, 1.06, 1.03, 1.0]);
        var freshRatios = new LMMCurveState(RateTimes);
        freshRatios.SetOnDiscountRatios([1.1, 1.06, 1.03, 1.0]);
        Assert.Equal(freshRatios.CoterminalSwapAnnuity(3, 1), cs.CoterminalSwapAnnuity(3, 1));
    }

    [Fact]
    public void TwoSpanningsShareOneBuffer()
    {
        var cs = new LMMCurveState(RateTimes);
        cs.SetOnForwardRates([0.03, 0.04, 0.05]);
        ReadOnlySpan<double> one = cs.CmSwapRates(1);
        double first = one[0];
        ReadOnlySpan<double> two = cs.CmSwapRates(2);
        Assert.Equal(two[0], one[0]);
        Assert.NotEqual(first, one[0]);
        Assert.Equal(0.03, first, 1e-15);
    }

    [Fact]
    public void ACloneIsDeep()
    {
        var cs = new CMSwapCurveState(RateTimes, 2);
        cs.SetOnCMSwapRates([0.03, 0.04, 0.05]);
        var copy = (CMSwapCurveState)cs.Clone();
        double ratio = copy.DiscountRatio(0, 3), forward = copy.ForwardRate(0);
        cs.SetOnCMSwapRates([0.06, 0.07, 0.08]);
        Assert.Equal(ratio, copy.DiscountRatio(0, 3));
        Assert.Equal(forward, copy.ForwardRate(0));
        Assert.NotEqual(ratio, cs.DiscountRatio(0, 3));

        var lmm = new LMMCurveState(RateTimes);
        lmm.SetOnForwardRates([0.03, 0.04, 0.05]);
        _ = lmm.CoterminalSwapAnnuity(3, 1);
        CurveState lmmCopy = lmm.Clone();
        Assert.Equal(lmm.CoterminalSwapAnnuity(3, 0), lmmCopy.CoterminalSwapAnnuity(3, 0));
        Assert.IsType<LMMCurveState>(lmmCopy);
        Assert.IsType<CoterminalSwapCurveState>(new CoterminalSwapCurveState(RateTimes).Clone());
    }

    [Fact]
    public void AnEvolversCurrentStateIsItsOwn()
    {
        var model = new FlatVol(
            [0.2, 0.2, 0.2], new ExponentialForwardCorrelation(RateTimes), new EvolutionDescription(RateTimes), 2, [0.03, 0.04, 0.05], [0.0, 0.0, 0.0]);
        var e = new LogNormalFwdRatePc(model, new MTBrownianGeneratorFactory(42), [3, 3, 3]);
        e.StartNewPath();
        e.AdvanceStep();
        CurveState state = e.CurrentState;
        double f2 = state.ForwardRate(2);
        CurveState kept = state.Clone();
        e.AdvanceStep();
        Assert.Same(state, e.CurrentState);
        Assert.NotEqual(f2, state.ForwardRate(2));
        Assert.Equal(f2, kept.ForwardRate(2));
    }
}
