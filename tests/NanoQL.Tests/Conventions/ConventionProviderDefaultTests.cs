// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="ConventionProvider.Default"/>: set and restored, set while a scope is active, swapped while presets are
/// built, and the one set an object takes after it changes. These are the suite's only facts that set it, in a
/// collection that runs alone, each restoring it in <c>finally</c>.
/// </summary>
[Collection(DefaultProviderCollection.Name)]
public class ConventionProviderDefaultTests
{
    [Fact]
    public void DefaultIsCurrentOutsideAnyScopeAndNullRestoresQuantLibs()
    {
        var provider = new TestConventions("default");
        try
        {
            ConventionProvider.Default = provider;
            Assert.Same(provider, ConventionProvider.Default);
            Assert.Same(provider, ConventionProvider.Current);

            ConventionProvider.Default = null;
            Assert.Same(ConventionProvider.QuantLib, ConventionProvider.Default);
            Assert.Same(ConventionProvider.QuantLib, ConventionProvider.Current);
        }
        finally
        {
            ConventionProvider.Default = null;
        }
    }

    [Fact]
    public void AScopeOutranksDefaultSetWhileItIsActive()
    {
        var scoped = new TestConventions("scoped");
        var later = new TestConventions("later");
        try
        {
            using (ConventionProvider.Use(scoped))
            {
                ConventionProvider.Default = later;
                Assert.Same(scoped, ConventionProvider.Current);
            }

            Assert.Same(later, ConventionProvider.Current);
        }
        finally
        {
            ConventionProvider.Default = null;
        }
    }

    [Fact]
    public async Task APresetBuiltWhileDefaultIsSwappedTakesAllItsValuesFromOneProvider()
    {
        // Each provider marks the legacy currency and its triangulation currency; another thread swaps Default between
        // them while the currency is built.
        TestConventions a = TestConventions.MarkingCurrencies("a", "ATS", "EUR");
        TestConventions b = TestConventions.MarkingCurrencies("b", "ATS", "EUR");
        using var stop = new CancellationTokenSource();
        using var swapping = new ManualResetEventSlim();
        try
        {
            Task swapper = Task.Run(
                () =>
                {
                    while (!stop.IsCancellationRequested)
                    {
                        ConventionProvider.Default = a;
                        swapping.Set();
                        ConventionProvider.Default = b;
                    }
                },
                TestContext.Current.CancellationToken);
            swapping.Wait(TestContext.Current.CancellationToken);

            int mixed = 0, fromA = 0, fromB = 0;
            for (int i = 0; i < 20000; ++i)
            {
                var ats = new ATSCurrency();
                string own = TestConventions.MarkOf(ats);
                mixed += own == TestConventions.MarkOf(ats.TriangulationCurrency!) ? 0 : 1;
                fromA += own == "a" ? 1 : 0;
                fromB += own == "b" ? 1 : 0;
            }

            await stop.CancelAsync();
            await swapper;
            Assert.Equal(0, mixed);
            Assert.True(fromA > 0 && fromB > 0, $"the swaps reached the presets: {fromA} from a, {fromB} from b");
        }
        finally
        {
            stop.Cancel();
            ConventionProvider.Default = null;
        }
    }

    [Fact]
    public void AfterDefaultChangesABuilderOnAnEarlierIndexTakesTheEarlierSet()
    {
        // An index built under the earlier set carries it: its builder, made after Default changes, builds in the
        // earlier set, which the probe calendar sees while the swap's schedules are built, and its currency chain
        // resolves EUR there; an index built after the change carries the later set.
        var earlier = TestConventions.MarkingCurrencies("earlier", "EUR");
        var later = TestConventions.MarkingCurrencies("later", "EUR");
        var probe = new ProviderProbeCalendar();
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) };
        IborIndex Index() => new(
            "Probe", new Period(6, TimeUnit.Months), 2, new EURCurrency(), probe, BusinessDayConvention.ModifiedFollowing,
            true, new Actual360());
        try
        {
            ConventionProvider.Default = earlier;
            IborIndex before = Index();
            ConventionProvider.Default = later;
            IborIndex after = Index();
            earlier.Clear();
            later.Clear();
            probe.Clear();

            VanillaSwap swap = new MakeVanillaSwap(new Period(5, TimeUnit.Years), before).WithFixedRate(0.03).ToVanillaSwap(context);
            Assert.True(probe.Saw(earlier));
            Assert.False(probe.Saw(later));
            Assert.True(earlier.HasResolved("currency:EUR"));
            Assert.Empty(later.Resolved);
            Assert.Equal(new Thirty360(Thirty360Convention.BondBasis), swap.FixedDayCount);

            probe.Clear();
            _ = new MakeVanillaSwap(new Period(5, TimeUnit.Years), after).WithFixedRate(0.03).ToVanillaSwap(context);
            Assert.True(probe.Saw(later));
            Assert.Equal("earlier", TestConventions.MarkOf(before.Currency!));
            Assert.Equal("later", TestConventions.MarkOf(after.Currency!));
        }
        finally
        {
            ConventionProvider.Default = null;
        }
    }
}
