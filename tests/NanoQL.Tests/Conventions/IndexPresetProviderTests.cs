// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Extensions.Conventions;
using NanoQL.Indexes;
using NanoQL.Tests.Extensions;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Every index, swap index, inflation and region preset reads its conventions through
/// <see cref="ConventionProvider"/> once, by its class name (a family by its name and tenor), and keeps the provider it
/// read: with nothing set the coded preset's QuantLib values, which each index's vector file checks (G3, unchanged);
/// a provider's otherwise, the calendars and currency it builds coming from that provider too. A fixed-tenor preset is
/// its family at its tenor, so the two read one record (CVD13). A provider changing what a preset fixes is refused.
/// </summary>
public class IndexPresetProviderTests
{
    public static TheoryData<string> IborPresets() => [.. CodedPresets.IborIndexes.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(IborPresets))]
    public void AnIborOrOvernightPresetReadsItsConventionsOnceAndKeepsItsProvider(string name)
    {
        var identity = new TestConventions("identity");
        IborIndex plain = CodedPresets.IborIndexes[name]();
        IborIndex read = Under(identity, () => CodedPresets.IborIndexes[name]());

        Assert.Equal(IndexFields.Of(plain), IndexFields.Of(read));
        Assert.Same(identity, TestConventions.ProviderOf(read));
        Assert.Single(identity.Resolved, r => r.StartsWith("family:", StringComparison.Ordinal)
            || r.StartsWith("overnight:", StringComparison.Ordinal) || r.StartsWith("ibor:", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryOtherIndexPresetReadsThroughTheProvider()
    {
        var identity = new TestConventions("identity");
        void Reads(object built, string resolve)
        {
            Assert.Contains(resolve, identity.Resolved);
            if (built is FinancialIndex index)
            {
                Assert.Same(identity, TestConventions.ProviderOf(index));
            }
        }

        foreach ((string name, Func<Period, SwapIndex> swap) in CodedPresets.SwapIndexes)
        {
            Reads(Under(identity, () => swap(new Period(10, TimeUnit.Years))), $"swap:{name}@10Y");
        }

        foreach ((string name, Func<int, IborIndex> daily) in CodedPresets.DailyTenorLibors)
        {
            Reads(Under(identity, () => daily(1)), "ibor:" + name);
        }

        foreach ((string name, Func<ZeroInflationIndex> zero) in CodedPresets.ZeroInflationIndexes)
        {
            Reads(Under(identity, zero), "inflation:" + name);
        }

        foreach ((string name, Func<YoYInflationIndex> yoy) in CodedPresets.YoYInflationIndexes)
        {
            Reads(Under(identity, yoy), "inflation:" + name);
        }

        foreach ((string name, Func<Region> region) in CodedPresets.Regions)
        {
            Reads(Under(identity, region), "region:" + name);
        }

        Reads(Under(identity, () => new BMAIndex()), "ibor:BMAIndex");
        Reads(Under(identity, () => new AUCPI(Frequency.Quarterly, true)), "inflation:AUCPI");
        Reads(Under(identity, () => new USDLiborON()), "ibor:USDLiborON");
        Reads(Under(identity, () => new EURLiborON()), "ibor:EURLiborON");
    }

    [Fact]
    public void APresetsCalendarsAndCurrencyComeFromItsProvider()
    {
        // a holiday on London's exchange and on Brazil's settlement calendar, the USD Libor's and the CDI's
        Date holiday = new(16, Month.October, 2026);
        var london = TestConventions.AddingHoliday("london", "UnitedKingdom.Exchange", holiday);
        var brazil = TestConventions.AddingHoliday("brazil", "Brazil.Settlement", holiday);

        var libor = Under(london, () => new USDLibor(new Period(3, TimeUnit.Months)));
        Cdi cdi = Under(brazil, () => new Cdi());

        Assert.True(libor.FixingCalendar.IsHoliday(holiday));
        Assert.True(libor.JointCalendar.IsHoliday(holiday));
        Assert.False(new USDLibor(new Period(3, TimeUnit.Months)).FixingCalendar.IsHoliday(holiday));
        Assert.True(cdi.FixingCalendar.IsHoliday(holiday));
        Assert.True(((Business252)cdi.DayCounter).Calendar.IsHoliday(holiday));
    }

    [Fact]
    public void AFixedTenorPresetAndItsFamilyAtItsTenorReadOneRecord()
    {
        ConventionSet set = ConventionSet.QuantLib.With(ConventionsText.Records(
            "<IborIndex name=\"Euribor6M\" base=\"Euribor6M\"><FixingDays>1</FixingDays></IborIndex>"));
        using (ConventionProvider.Use(set))
        {
            Assert.Equal(1, new Euribor6M().FixingDays);
            Assert.Equal(1, new Euribor(new Period(6, TimeUnit.Months)).FixingDays);
            Assert.Equal(2, new Euribor(new Period(3, TimeUnit.Months)).FixingDays);
        }

        Assert.Equal(1, set.IborIndex("Euribor", new Period(6, TimeUnit.Months)).FixingDays);
        Assert.Equal(2, new Euribor6M().FixingDays);
    }

    [Fact]
    public void AProvidersChangeOfWhatAPresetFixesIsRefused()
    {
        var overnight = new TestConventions("p")
        {
            Indexes = new Dictionary<string, Func<IborIndexConventions, IborIndexConventions>>
            {
                ["Sofr"] = c => c with { Convention = BusinessDayConvention.ModifiedFollowing },
                ["BMAIndex"] = c => c with { EndOfMonth = true },
                ["USDLibor"] = c => c with { FinancialCenterCalendar = null },
                ["CADLiborON"] = c => c with { FinancialCenterCalendar = null },
                ["EURLibor"] = c => c with { FinancialCenterCalendar = null },
                ["GBPLibor"] = c => c with { Currency = new EURCurrency() },
            },
            Regions = new Dictionary<string, Func<RegionData, RegionData>> { ["EURegion"] = d => d with { Name = "Eurozone" } },
        };

        string Refusal(Func<object> build) => Assert.ThrowsAny<Exception>(() => Under(overnight, build)).Message;

        Assert.Equal("the Sofr overnight index's convention is Following without end of month, not ModifiedFollowing", Refusal(() => new Sofr()));
        Assert.Equal("the BMA index's conventions take no convention or end of month but QuantLib's", Refusal(() => new BMAIndex()));
        Assert.Equal("the USDLibor conventions name no financial-centre calendar", Refusal(() => new USDLibor(new Period(3, TimeUnit.Months))));
        Assert.Equal("the CADLibor conventions name no financial-centre calendar", Refusal(() => new CADLiborON()));
        Assert.Equal("the EURLibor conventions name no financial-centre calendar", Refusal(() => new EURLibor(new Period(3, TimeUnit.Months))));
        Assert.Equal("for EUR Libor dedicated EurLibor constructor must be used", Refusal(() => new GBPLibor(new Period(3, TimeUnit.Months))));
        Assert.Equal("the data for the EURegion region changes its name: \"Eurozone\" for QuantLib's \"EU\"", Refusal(() => new EURegion()));

        // the region's code is configuration
        var coded = new TestConventions("c") { Regions = new Dictionary<string, Func<RegionData, RegionData>> { ["EURegion"] = d => d with { Code = "EZ" } } };
        Assert.Equal(("EU", "EZ"), Under(coded, () => new EURegion()) is var r ? (r.Name, r.Code) : default);
    }

    [Fact]
    public void ASwapIndexsIborIndexIsBuiltInItsProviderOnItsCurve()
    {
        // QuantLib's reference builds the Ibor index on the swap index's curve, in its provider's scope
        var probe = new TestConventions("probe");
        var curve = new TermStructures.FlatForward(new Date(15, Month.October, 2026), 0.03, new Actual365Fixed());
        var swap = Under(probe, () => new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), curve));

        Assert.Same(curve, swap.IborIndex.ForwardingTermStructure);
        Assert.IsType<Euribor>(swap.IborIndex);
        Assert.Same(probe, TestConventions.ProviderOf(swap.IborIndex));
        Assert.Contains("family:Euribor@6M", probe.Resolved);
    }

    private static T Under<T>(ConventionProvider provider, Func<T> build)
    {
        using (ConventionProvider.Use(provider))
        {
            return build();
        }
    }
}
