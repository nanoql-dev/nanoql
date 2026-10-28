// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An event settled at one recovery rate settles the seniority it names (the S-xcr plan's XR8 (iii)): QuantLib's
/// single-rate settlement fills every other seniority with ISDA's conventional recovery (<c>defaultevent.cpp:63-75</c>),
/// so a failure to pay settled for SubLT2 at 0.3 reads 0.65 for SecDom, where each seniority settles at its own auction
/// (ISDA's 2008 Icelandic protocols). And a bankruptcy settled by a map settles the seniorities it holds, where QuantLib's
/// refuses a map without all five (<c>defaultevent.cpp:210-213</c>). QuantLib's values are the S-xcr run's
/// <c>c1_facts.cpp</c> and the plan review's <c>rv_events.cpp</c>; the check is QuantLib's map constructor holding the
/// seniority alone, which reads <c>Null&lt;Real&gt;()</c> for the others.
/// </summary>
public class SingleSenioritySettlementTests
{
    private static readonly Date Defaulted = new(1, June, 2006), Settled = new(1, July, 2006);

    [Fact]
    public void AFailureToPaySettlesTheSeniorityItNames()
    {
        // QuantLib, single rate: SecDom 0.65, SnrFor 0.4, SubLT2 0.3, JrSubT2 0.2, PrefT1 0.15; its map {SubLT2: 0.3}
        // reads SubLT2 0.3 and SecDom Null<Real>()
        var single = new FailureToPayEvent(Defaulted, new EURCurrency(), Seniority.SubLT2, 2e6, Settled, 0.3);
        var map = new FailureToPayEvent(
            Defaulted, new EURCurrency(), Seniority.SubLT2, 2e6, Settled, new Dictionary<Seniority, double> { [Seniority.SubLT2] = 0.3 });

        foreach (Seniority s in new[] { Seniority.SecDom, Seniority.SnrFor, Seniority.JrSubT2, Seniority.PrefT1 })
        {
            Assert.Null(single.RecoveryRate(s));
            Assert.Equal(map.RecoveryRate(s), single.RecoveryRate(s));
        }

        Assert.Equal(0.3, single.RecoveryRate(Seniority.SubLT2));
        Assert.Equal(0.3, map.RecoveryRate(Seniority.SubLT2));
    }

    [Fact]
    public void ABankruptcySettlesTheSeniorityItNamesOrItsMapHolds()
    {
        // QuantLib, single rate SubLT2 0.3: SecDom 0.65 and the others conventional; its map holding SubLT2 alone throws
        // "Bankruptcy event should have settled for all seniorities."
        var single = new BankruptcyEvent(Defaulted, new EURCurrency(), Seniority.SubLT2, Settled, 0.3);
        var map = new BankruptcyEvent(
            Defaulted, new EURCurrency(), Seniority.SubLT2, Settled, new Dictionary<Seniority, double> { [Seniority.SubLT2] = 0.3 });

        Assert.Null(single.RecoveryRate(Seniority.SecDom));
        Assert.Equal(0.3, single.RecoveryRate(Seniority.SubLT2));
        Assert.Null(map.RecoveryRate(Seniority.SecDom));
        Assert.Equal(0.3, map.RecoveryRate(Seniority.SubLT2));
    }
}
