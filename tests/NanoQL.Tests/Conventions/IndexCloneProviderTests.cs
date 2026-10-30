// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// An index keeps the provider it was built with, and every clone keeps the same one, built under another provider or
/// none, reading no provider: each index type's clone, and a swap index cloned by tenor while a Gaussian model prices.
/// </summary>
public class IndexCloneProviderTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());
    private static readonly YieldTermStructure Other = new FlatForward(Today, 0.02, new Actual365Fixed());

    public static TheoryData<string> Clones() =>
    [
        "IborIndex", "OvernightIndex", "CustomIborIndex", "Libor", "EURLibor", "Cdi", "Shibor", "SwapIndex(curve)",
        "SwapIndex(curves)", "SwapIndex(tenor)", "ZeroInflationIndex", "YoYInflationIndex(ratio)",
        "YoYInflationIndex(quoted)", "EquityIndex",
    ];

    [Theory]
    [MemberData(nameof(Clones))]
    public void AClonesProviderIsItsSourcesAndTheCloneReadsNone(string kind)
    {
        var built = new TestConventions("built");
        var cloning = new TestConventions("cloning");
        FinancialIndex source;
        using (ConventionProvider.Use(built))
        {
            source = Build(kind);
        }

        FinancialIndex clone;
        using (ConventionProvider.Use(cloning))
        {
            clone = Clone(kind, source);
        }

        Assert.Same(built, TestConventions.ProviderOf(source));
        Assert.Same(built, TestConventions.ProviderOf(clone));
        Assert.Empty(cloning.Resolved);
        Assert.Equal(source.Name, clone.Name);
        Assert.Equal(source.FixingCalendar, clone.FixingCalendar);
    }

    [Fact]
    public void ASwapIndexsCloneKeepsItsOwnProviderAndItsIborIndexCloneTheIborIndexs()
    {
        var iborProvider = new TestConventions("ibor");
        var swapProvider = new TestConventions("swap");
        IborIndex ibor;
        using (ConventionProvider.Use(iborProvider))
        {
            ibor = new Euribor6M();
        }

        SwapIndex swap;
        using (ConventionProvider.Use(swapProvider))
        {
            swap = new SwapIndex(
                "S", new Period(5, TimeUnit.Years), 2, new EURCurrency(), new TARGET(), new Period(1, TimeUnit.Years),
                BusinessDayConvention.ModifiedFollowing, new Thirty360(Thirty360Convention.BondBasis), ibor);
        }

        SwapIndex clone = swap.Clone(Other);
        Assert.Same(swapProvider, TestConventions.ProviderOf(clone));
        Assert.Same(iborProvider, TestConventions.ProviderOf(clone.IborIndex));
        Assert.Same(Other, clone.ForwardingTermStructure);
    }

    [Fact]
    public void ASwapIndexClonedByTenorWhileAGaussianModelPricesBuildsInItsProvider()
    {
        // The model's memo clones the swap index by tenor and builds the clone's underlying swap: on the swap index's
        // provider, which the probe calendar of its Ibor index sees while the swap's schedules are built.
        var probe = new ProviderProbeCalendar();
        var built = new TestConventions("built");
        var pricing = new TestConventions("pricing");
        var context = new EvaluationContext { EvaluationDate = Today };
        SwapIndex swapIndex;
        using (ConventionProvider.Use(built))
        {
            var ibor = new IborIndex(
                "Probe", new Period(6, TimeUnit.Months), 2, new EURCurrency(), probe,
                BusinessDayConvention.ModifiedFollowing, true, new Actual360(), Curve);
            swapIndex = new SwapIndex(
                "ProbeSwap", new Period(10, TimeUnit.Years), 2, new EURCurrency(), probe, new Period(1, TimeUnit.Years),
                BusinessDayConvention.ModifiedFollowing, new Thirty360(Thirty360Convention.BondBasis), ibor);
        }

        var model = new Gsr(Curve, [Today + new Period(1, TimeUnit.Years)], [0.008, 0.01], [0.02, 0.03]);
        probe.Clear();
        double rate;
        using (ConventionProvider.Use(pricing))
        {
            rate = model.SwapRate(new Date(16, Month.October, 2028), new Period(5, TimeUnit.Years), context, swapIdx: swapIndex);
        }

        Assert.True(double.IsFinite(rate));
        Assert.True(probe.Saw(built), $"the probe saw {string.Join(", ", probe.Seen)}");
    }

    [Fact]
    public void EURLiborsTargetDatesFollowTheProviderOfItsConstruction()
    {
        // A TARGET holiday on the value date moves EURLibor's value date, and its clone's, wherever they are asked.
        Date fixing = new(12, Month.October, 2026);
        Date valueDate = new EURLibor6M().ValueDate(fixing);
        var provider = TestConventions.AddingHoliday("p", "TARGET", valueDate);
        IborIndex edited;
        using (ConventionProvider.Use(provider))
        {
            edited = new EURLibor6M();
        }

        IborIndex clone = edited.Clone(Curve);
        Assert.Equal(valueDate + 1, edited.ValueDate(fixing));
        Assert.Equal(valueDate + 1, clone.ValueDate(fixing));
        Assert.Equal(edited.MaturityDate(valueDate + 1), clone.MaturityDate(valueDate + 1));
        Assert.Equal(edited.FixingDate(valueDate + 1), clone.FixingDate(valueDate + 1));
    }

    private static FinancialIndex Build(string kind) => kind switch
    {
        "IborIndex" => new IborIndex(
            "Ibor", new Period(3, TimeUnit.Months), 2, new EURCurrency(), new TARGET(),
            BusinessDayConvention.ModifiedFollowing, true, new Actual360(), Curve),
        "OvernightIndex" => new OvernightIndex("Overnight", 0, new EURCurrency(), new TARGET(), new Actual360(), Curve),
        "CustomIborIndex" => new CustomIborIndex(
            "Custom", new Period(3, TimeUnit.Months), 2, new USDCurrency(), new UnitedStates(UnitedStatesMarket.NYSE),
            new UnitedStates(UnitedStatesMarket.Settlement), new UnitedKingdom(), BusinessDayConvention.ModifiedFollowing,
            true, new Actual360(), Curve),
        "Libor" => new USDLibor(new Period(3, TimeUnit.Months), Curve),
        "EURLibor" => new EURLibor6M(Curve),
        "Cdi" => new Cdi(Curve),
        "Shibor" => new Shibor(new Period(3, TimeUnit.Months), Curve),
        "SwapIndex(curve)" or "SwapIndex(curves)" or "SwapIndex(tenor)" => new EuriborSwapIsdaFixA(
            new Period(10, TimeUnit.Years), Curve, Curve),
        "ZeroInflationIndex" => new EUHICP(),
        "YoYInflationIndex(ratio)" => new YoYInflationIndex(new UKRPI()),
        "YoYInflationIndex(quoted)" => new YYEUHICP(),
        "EquityIndex" => new EquityIndex("Equity", new TARGET(), new EURCurrency(), Curve, Curve, 100.0),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown index"),
    };

    private static FinancialIndex Clone(string kind, FinancialIndex source) => kind switch
    {
        "OvernightIndex" or "Cdi" => ((OvernightIndex)source).Clone(Other),
        "SwapIndex(curve)" => ((SwapIndex)source).Clone(Other),
        "SwapIndex(curves)" => ((SwapIndex)source).Clone(Other, Other),
        "SwapIndex(tenor)" => ((SwapIndex)source).Clone(new Period(10, TimeUnit.Years)),
        "ZeroInflationIndex" => ((ZeroInflationIndex)source).Clone(null),
        "YoYInflationIndex(ratio)" or "YoYInflationIndex(quoted)" => ((YoYInflationIndex)source).Clone(null),
        "EquityIndex" => ((EquityIndex)source).Clone(Other, Other, 101.0),
        _ => ((IborIndex)source).Clone(Other),
    };
}
