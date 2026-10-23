// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Markov-functional model's outputs after a recalculation (the S-g1d plan's G1R1 (v), round 1's O1-10): QuantLib's
/// <c>modelOutputs</c> clears every output but the at-the-money levels and the annuities, which it appends to
/// (<c>markovfunctional.cpp:618-648</c>), so its first call after a recalculation (a changed market or
/// <c>forceArbitrageIndices</c>) returns them twice, the stale ones first [read]. nanoql computes the outputs per call:
/// a copy calibrated again gives a fresh model's. QuantLib's check: its own first call on a fresh model.
/// </summary>
public class MarkovFunctionalOutputsTests
{
    [Fact]
    public void ARecalibratedCopysOutputsAreAFreshModels()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.03, new Actual365Fixed());
        SwaptionVolatilityStructure vol = new ConstantSwaptionVolatility(
            today, new TARGET(), BusinessDayConvention.ModifiedFollowing, 0.20, new Actual365Fixed());
        Date[] expiries = [.. Enumerable.Range(1, 5).Select(i => new TARGET().Advance(today, i, TimeUnit.Years))];
        Period[] tenors = [.. Enumerable.Repeat(new Period(10, TimeUnit.Years), 5)];
        MarkovFunctional Fresh() => new(
            curve, 0.01, [], [1.0], vol, expiries, tenors, new EuriborSwapIsdaFixA(new Period(1, TimeUnit.Years)), context,
            new MarkovFunctionalModelSettings().WithYGridPoints(32).WithGaussHermitePoints(16));

        MarkovFunctional model = Fresh();
        MarkovFunctionalModelOutputs first = model.ModelOutputs(context);
        (int Left, int Right)[] forced = [.. model.ArbitrageIndices().Select(p => p!.Value)];
        MarkovFunctionalModelOutputs again = model.ForceArbitrageIndices(forced, context).ModelOutputs(context);
        MarkovFunctionalModelOutputs fresh = Fresh().ForceArbitrageIndices(forced, context).ModelOutputs(context);

        // QuantLib's: twice the points' count, the stale ones first.
        Assert.Equal(first.Expiries.Length, again.Atm.Length);
        Assert.Equal(fresh.Atm, again.Atm);
        Assert.Equal(fresh.Annuity, again.Annuity);
        Assert.Equal(fresh.ModelCallPremium.SelectMany(x => x), again.ModelCallPremium.SelectMany(x => x));
    }
}
