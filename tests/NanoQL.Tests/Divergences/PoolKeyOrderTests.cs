// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A pool's default keys, read in the pool's order with each name's own key (the S-xcr plan's XR8 (i)): QuantLib's
/// <c>defaultKeys()</c> lists them in name order (<c>pool.cpp:77-82</c>), so names C and A entering with keys KA and KB
/// read C's probability on KB's curve; and its <c>clear()</c> keeps the cleared names' keys (<c>pool.cpp:34-38</c>), so a
/// name added after it reads a cleared name's key. QuantLib's values are the planner's <c>xp_basket.cpp</c> and the plan
/// review's <c>rv_basket.cpp clear</c>; the checks are QuantLib on the pool added in name order, and on a fresh pool.
/// </summary>
public class PoolKeyOrderTests
{
    private static readonly NorthAmericaCorpDefaultKey KA = Key(Seniority.SeniorSec), KB = Key(Seniority.SnrFor);

    [Fact]
    public void EachNameReadsItsOwnKey()
    {
        // QuantLib, C entering with KA and A with KB: probabilities 0.1394099207 (C on KB, h 0.05) and 0.0582870685
        // (A on KA, h 0.02); on its own keys, C 0.0295810536 (h 0.01) and A 0.0861439293 (h 0.03)
        var d = new Date(31, August, 2009);
        var c = new Issuer([(KA, Flat(Today, 0.01)), (KB, Flat(Today, 0.05))]);
        var a = new Issuer([(KA, Flat(Today, 0.02)), (KB, Flat(Today, 0.03))]);
        Pool pool = new Pool().Add("C", c, KA).Add("A", a, KB);
        var basket = new Basket(Today, ["C", "A"], [10.0, 20.0], pool, Context);

        double[] p = [.. basket.Probabilities(d)];

        Assert.Equal(Flat(Today, 0.01).DefaultProbability(d), p[0]);
        Assert.Equal(Flat(Today, 0.03).DefaultProbability(d), p[1]);
        Assert.Equal(0.0295810536, p[0], 1e-10);
        Assert.Equal(0.0861439293, p[1], 1e-10);
        Assert.NotEqual(0.1394099207, p[0], 1e-6);
        Assert.Equal(new[] { (DefaultProbKey)KA, KB }, pool.DefaultKeys);
    }

    [Fact]
    public void AClearedPoolHoldsNoKey()
    {
        // QuantLib, A (KA) and B (KB) cleared and C added with KB: the basket reads C on A's stale KA, 0.1813589659,
        // where C on its own KB (h 0.06) is 0.2593035476
        Pool pool = new Pool()
            .Add("A", new Issuer([(KA, Flat(Today, 0.01)), (KB, Flat(Today, 0.05))]), KA)
            .Add("B", new Issuer([(KA, Flat(Today, 0.02)), (KB, Flat(Today, 0.03))]), KB);
        Pool cleared = pool.Clear().Add("C", new Issuer([(KA, Flat(Today, 0.04)), (KB, Flat(Today, 0.06))]), KB);

        var basket = new Basket(Today, ["C"], [10.0], cleared, Context);

        Assert.Equal(2, pool.Size);
        Assert.Equal(0.2593035476, basket.Probabilities(D5)[0], 1e-10);
        Assert.Equal(new[] { (DefaultProbKey)KB }, cleared.DefaultKeys);
    }
}
