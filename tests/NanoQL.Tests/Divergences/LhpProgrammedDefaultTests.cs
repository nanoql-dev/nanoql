// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The LHP model under a default programmed after the evaluation date (the S-xcr plan's XR8 (ii), round 3's O3-1): its
/// averages read the notionals and recoveries of the names alive at the evaluation date, so its statistics are its
/// model's without the event. QuantLib's reads the notionals alive at the statistic's date beside the evaluation date's
/// probabilities and recoveries, past its vectors, undefined (the plan's four runs: 2.24789, 2.24363, 1.77310 and a
/// throw); without the event it reads 12.282321555009048 at ten years on the basket vector's programmed case (the S-xcr
/// run's <c>c2_facts.cpp</c>).
/// </summary>
public class LhpProgrammedDefaultTests
{
    [Fact]
    public void AProgrammedDefaultLeavesTheModelsStatistics()
    {
        var programmed = new BankruptcyEvent(Today + 500, new EURCurrency(), Seniority.SeniorSec, Today + 520, 0.25);
        Basket with = Four([null, null, null, [programmed]]);
        Basket without = Four(null);

        Assert.Equal(12.282321555009048, without.ExpectedTrancheLoss(Today + 3653), 1e-13);
        Assert.Equal(without.ExpectedTrancheLoss(Today + 3653), with.ExpectedTrancheLoss(Today + 3653));
    }

    private static Basket Four(DefaultEvent[]?[]? events)
    {
        string[] names = ["Name0", "Name1", "Name2", "Name3"];
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.01), Flat(Today, 0.02), Flat(Today, 0.015), Flat(Today, 0.03)];
        return new Basket(Today, names, [10.0, 20.0, 30.0, 40.0], PoolOf(names, curves, events), Context, 0.0, 0.5)
            .SetLossModel(new GaussianLHPLossModel(0.2, [0.4, 0.35, 0.5, 0.3]));
    }
}
