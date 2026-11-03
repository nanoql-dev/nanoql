// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Extensions.Conventions;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// The set: an override reaches every reference to its name and every coded construction under the set's scope;
/// <see cref="ConventionSet.With"/> leaves <see cref="ConventionProvider.Default"/> and its receiver alone, resolves
/// every record of the new set across its layers and refuses a cycle, an unknown name, an edited identity or a value the
/// core refuses when the file is loaded; sets side by side on two threads; equality and hash; the empty set's code kinds;
/// QuantLib's set and QuantLib's provider giving equal presets; the lookups of the presets that take arguments, each
/// built in the set and refusing a name of another kind; and the provenance report.
/// </summary>
public class ConventionSetTests
{
    private static readonly Date Holiday = new(16, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.October, 2026) };

    [Fact]
    public void AnOverrideReachesEveryReferenceToItsNameAndEveryCodedConstructionUnderTheSet()
    {
        ConventionSet set = ConventionsText.OnQuantLib($"""
            <Calendar name="TARGET" base="TARGET"><AddedHoliday>2026-10-16</AddedHoliday></Calendar>
            <JointCalendar name="London+TARGET"><Member>UnitedKingdom.Settlement</Member><Member>TARGET</Member></JointCalendar>
            <Calendar name="Desk" base="TARGET" />
            <DayCounter name="TARGET252" base="Business252"><Calendar>TARGET</Calendar></DayCounter>
            """);
        Date before = Holiday - 1, after = Holiday + 3;

        Assert.True(new TARGET().IsBusinessDay(Holiday));
        Assert.False(set.Calendar("TARGET").IsBusinessDay(Holiday));
        Assert.False(set.Calendar("EUTA").IsBusinessDay(Holiday));
        Assert.False(set.Calendar("London+TARGET").IsBusinessDay(Holiday));
        Assert.False(set.Calendar("Desk").IsBusinessDay(Holiday));
        Assert.Equal(new Business252(new TARGET()).DayCount(before, after) - 1, set.DayCounter("TARGET252").DayCount(before, after));
        using (ConventionProvider.Use(set))
        {
            Assert.False(new TARGET().IsBusinessDay(Holiday));
            Assert.False(new Euribor6M().FixingCalendar.IsBusinessDay(Holiday));
            Assert.False(new JointCalendar(new UnitedKingdom(), new TARGET()).IsBusinessDay(Holiday));
        }

        Assert.True(new TARGET().IsBusinessDay(Holiday));
        Assert.Equal(set.Calendar("TARGET"), new TARGET().AddHoliday(Holiday));
    }

    [Fact]
    public void WithLeavesTheDefaultAndItsReceiverUnchanged()
    {
        ConventionSet before = ConventionSet.QuantLib;
        ConventionSet after = before.With(ConventionsText.Records("<Calendar name=\"TARGET\" base=\"TARGET\"><AddedHoliday>2026-10-16</AddedHoliday></Calendar>"));

        Assert.Same(ConventionProvider.QuantLib, ConventionProvider.Default);
        Assert.Single(before.Layers);
        Assert.Equal(2, after.Layers.Length);
        Assert.True(before.Calendar("TARGET").IsBusinessDay(Holiday));
        Assert.False(after.Calendar("TARGET").IsBusinessDay(Holiday));
        Assert.True(new TARGET().IsBusinessDay(Holiday));
    }

    [Fact]
    public async Task TwoSetsSideBySideOnParallelThreadsEachBuildTheirOwn()
    {
        ConventionSet a = ConventionsText.OnQuantLib("<Calendar name=\"TARGET\" base=\"TARGET\"><AddedHoliday>2026-10-16</AddedHoliday></Calendar>");
        ConventionSet b = ConventionsText.OnQuantLib("<Calendar name=\"TARGET\" base=\"TARGET\"><AddedHoliday>2026-10-19</AddedHoliday></Calendar>");
        using var start = new ManualResetEventSlim();

        Task<(int A, int B)> Run(ConventionSet set) => Task.Run(() =>
        {
            start.Wait(TestContext.Current.CancellationToken);
            int onA = 0, onB = 0;
            for (int i = 0; i < 2000; ++i)
            {
                using (ConventionProvider.Use(set))
                {
                    var target = new TARGET();
                    onA += target.IsHoliday(Holiday) ? 1 : 0;
                    onB += target.IsHoliday(new Date(19, Month.October, 2026)) ? 1 : 0;
                }
            }

            return (onA, onB);
        });

        Task<(int A, int B)> first = Run(a), second = Run(b);
        start.Set();

        Assert.Equal((2000, 0), await first);
        Assert.Equal((0, 2000), await second);
    }

    [Fact]
    public void TwoSetsAreEqualWhenTheyHoldEqualSnapshotsInOrderOnTheSameBase()
    {
        const string A = "<Calendar name=\"Desk\" base=\"TARGET\" />", B = "<Calendar name=\"Other\" base=\"TARGET\" />";
        ConventionSet x = ConventionsText.OnQuantLib(A).With(ConventionsText.Records(B));
        ConventionSet y = ConventionsText.OnQuantLib(A).With(ConventionsText.Records(B));

        Assert.Equal(x, y);
        Assert.Equal(x.GetHashCode(), y.GetHashCode());
        Assert.NotEqual(x, ConventionsText.OnQuantLib(B).With(ConventionsText.Records(A)));
        Assert.NotEqual(ConventionsText.OnQuantLib(A), ConventionSet.Empty.With(ConventionsText.Records(A)));
        Assert.True(ConventionSet.QuantLib.IsOnQuantLib);
        Assert.False(ConventionSet.Empty.With(ConventionsText.Records(A)).IsOnQuantLib);
        Assert.Equal(ConventionSet.QuantLib, ConventionSet.QuantLib);
        Assert.NotEqual(ConventionSet.QuantLib, ConventionSet.Empty);
    }

    [Fact]
    public void TheSetsSnapshotsProvenanceAndForeignContentCompareByValueAsObjects()
    {
        // the members the coverage report found no test reaching: the object overloads and the provenance's
        const string A = "<Calendar name=\"Desk\" base=\"TARGET\" />";
        ConventionSet x = ConventionsText.OnQuantLib(A), y = ConventionsText.OnQuantLib(A);
        ConventionSnapshot s = ConventionsText.Records(A);

        Assert.True(x.Equals((object)y));
        Assert.False(x.Equals((object)s));
        Assert.True(s.Equals((object)ConventionsText.Records(A)));
        Assert.False(s.Equals((object)x));
        Assert.Equal(x.Provenance(), y.Provenance());
        Assert.Equal(x.Provenance().GetHashCode(), y.Provenance().GetHashCode());
        Assert.NotEqual(x.Provenance(), ConventionSet.QuantLib.Provenance());

        ConventionSnapshot foreign = ConventionsText.Records("<Calendar name=\"Desk\" base=\"TARGET\" xmlns:firm=\"urn:firm\" firm:id=\"7\" />");
        Assert.False(foreign.Records[0].Foreign.IsEmpty);
        Assert.True(s.Records[0].Foreign.IsEmpty);
    }

    [Fact]
    public void ALaterLayerOverridesAnEarlierOnesNameForEveryRecordThatReferencesIt()
    {
        // the joint calendar, from the first layer, reads the second's Desk
        ConventionSet set = ConventionsText.OnQuantLib("""
            <Calendar name="Desk" base="TARGET" />
            <JointCalendar name="Both"><Member>Desk</Member><Member>UnitedKingdom.Settlement</Member></JointCalendar>
            """).With(ConventionsText.Records("<Calendar name=\"Desk\" base=\"TARGET\"><AddedHoliday>2026-10-16</AddedHoliday></Calendar>"));

        Assert.False(set.Calendar("Both").IsBusinessDay(Holiday));

        // a later layer's record based on its own name edits the earlier one's
        ConventionSet edited = set.With(ConventionsText.Records("<Calendar name=\"Desk\" base=\"Desk\"><AddedHoliday>2026-10-19</AddedHoliday></Calendar>"));
        Assert.False(edited.Calendar("Desk").IsBusinessDay(Holiday));
        Assert.False(edited.Calendar("Desk").IsBusinessDay(new Date(19, Month.October, 2026)));
    }

    [Theory]
    [InlineData("<Calendar name=\"A\" base=\"B\" /><Calendar name=\"B\" base=\"A\" />", "Calendar A: A is part of a cycle")]
    [InlineData("<JointCalendar name=\"J\"><Member>TARGET</Member><Member>J</Member></JointCalendar>", "JointCalendar J: J is part of a cycle")]
    [InlineData("<Calendar name=\"A\" base=\"Nowhere\" />", "Calendar A: unknown calendar Nowhere")]
    [InlineData("<DayCounter name=\"D\" base=\"Business252\"><Calendar>Nowhere</Calendar></DayCounter>", "DayCounter D: unknown calendar Nowhere")]
    [InlineData("<Alias name=\"Desk\" base=\"Nowhere\" />", "Alias Desk: unknown calendar, day counter, currency, region, index, unit of measure or business-day convention Nowhere")]
    [InlineData("<JointCalendar name=\"J\"><Member>TARGET</Member></JointCalendar>", "JointCalendar J: the joint calendar J needs two or more members")]
    [InlineData("<DayCounter name=\"D\" base=\"Actual360\"><Calendar>TARGET</Calendar></DayCounter>", "DayCounter D: the day counter D's base Actual360 counts no calendar's days: only Business252 does")]
    [InlineData("<CalendarPreset name=\"TARGET\"><QuantLibName>Target</QuantLibName></CalendarPreset>", "CalendarPreset TARGET: QuantLib's name is TARGET, not Target")]
    [InlineData("<DayCounterPreset name=\"Actual360\"><QuantLibName>ACT/360</QuantLibName></DayCounterPreset>", "DayCounterPreset Actual360: QuantLib's name is Actual/360, not ACT/360")]
    [InlineData("<CurrencyPreset name=\"EUR\" base=\"EUR\"><QuantLibName>Euro</QuantLibName></CurrencyPreset>", "CurrencyPreset EUR: QuantLib's name is European Euro, not Euro")]
    [InlineData("<CurrencyPreset name=\"EUR\" base=\"EUR\"><NumericCode>979</NumericCode></CurrencyPreset>", "CurrencyPreset EUR: QuantLib's numeric code is 978, not 979")]
    [InlineData("<CurrencyPreset name=\"EUX\" base=\"EUX\" />", "CurrencyPreset EUX: no currency of QuantLib's has the code: a user may not define one")]
    [InlineData("<CurrencyPreset name=\"EUR\" base=\"EUR\"><TriangulationCurrency>USD</TriangulationCurrency></CurrencyPreset><CurrencyPreset name=\"USD\" base=\"USD\"><TriangulationCurrency>EUR</TriangulationCurrency></CurrencyPreset>", "the currency EUR is part of a cycle of triangulation currencies")]
    [InlineData("<Calendar name=\"Desk/2\" base=\"TARGET\" />", "Calendar Desk/2: a name of a user's own may not hold '/'")]
    [InlineData("<CalendarPreset name=\"TARGET\"><QuantLibName>TARGET</QuantLibName><AddedHoliday>2026-10-16</AddedHoliday><RemovedHoliday>2026-10-16</RemovedHoliday></CalendarPreset>", "CalendarPreset TARGET: October 16th, 2026 is both added and removed")]
    public void WithRefusesARecordThatCantResolveNamingIt(string records, string message)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => ConventionsText.OnQuantLib(records));

        Assert.Contains(message, e.Message, StringComparison.Ordinal);
        Assert.Equal("snapshot", e.ParamName);
    }

    [Fact]
    public void ALayerThatBreaksAnEarlierLayersRecordIsRefusedAtWith()
    {
        ConventionSet set = ConventionsText.OnQuantLib("<Calendar name=\"A\" base=\"B\" /><Calendar name=\"B\" base=\"TARGET\" />");

        ArgumentException e = Assert.Throws<ArgumentException>(() => set.With(ConventionsText.Records("<Calendar name=\"B\" base=\"A\" />")));
        Assert.Contains("is part of a cycle", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADuplicateNameInOneSnapshotIsRefused()
    {
        ConventionSnapshot s = ConventionSnapshot.Empty.Add(new CalendarRecord("Desk", "TARGET"));

        ArgumentException e = Assert.Throws<ArgumentException>(() => s.Add(new AliasRecord("Desk", "TARGET")));
        Assert.Contains("Desk", e.Message, StringComparison.Ordinal);
        Assert.Equal(2, s.Add(new DayCounterRecord("Desk", "Actual360")).Records.Length);
    }

    [Theory]
    [InlineData("calendar")]
    [InlineData("day counter")]
    [InlineData("currency")]
    public void AnUnknownNameIsRefusedNamingItsKindAndTheName(string kind)
    {
        ConventionSet set = ConventionSet.QuantLib;
        ArgumentException e = kind switch
        {
            "calendar" => Assert.Throws<ArgumentException>(() => set.Calendar("Nowhere")),
            "day counter" => Assert.Throws<ArgumentException>(() => set.DayCounter("Nowhere")),
            _ => Assert.Throws<ArgumentException>(() => set.Currency("Nowhere")),
        };

        Assert.StartsWith($"unknown {kind} Nowhere", e.Message, StringComparison.Ordinal);
        Assert.Equal(kind == "currency" ? "code" : "name", e.ParamName);
    }

    [Fact]
    public void TheEmptySetResolvesTheCodeKindsToQuantLibsPresetsAndNoAliases()
    {
        ConventionSet empty = ConventionSet.Empty;

        Assert.Equal(new TARGET(), empty.Calendar("TARGET"));
        Assert.Equal(new Actual360(), empty.DayCounter("Actual360"));
        Assert.Equal(new EURCurrency(), empty.Currency("EUR"));
        Assert.Throws<ArgumentException>(() => empty.Calendar("EUTA"));
        Assert.Throws<ArgumentException>(() => empty.DayCounter("ACT/360"));
        Assert.Empty(empty.Layers);
    }

    [Fact]
    public void QuantLibsSetAndQuantLibsProviderBuildEqualPresets()
    {
        foreach ((string name, Func<Calendar> calendar) in CodedPresets.Calendars)
        {
            Assert.Equal(Under(ConventionProvider.QuantLib, calendar), Under(ConventionSet.QuantLib, calendar));
            Assert.Equal(calendar(), ConventionSet.QuantLib.Calendar(name));
        }

        foreach ((string name, Func<DayCounter> dayCounter) in CodedPresets.DayCounters)
        {
            Assert.Equal(Under(ConventionProvider.QuantLib, dayCounter), Under(ConventionSet.QuantLib, dayCounter));
            Assert.Equal(dayCounter(), ConventionSet.QuantLib.DayCounter(name));
        }

        foreach ((string code, Func<Currency> currency) in CodedPresets.Currencies)
        {
            Currency a = Under(ConventionProvider.QuantLib, currency), b = Under(ConventionSet.QuantLib, currency);
            Assert.Equal(
                (a, a.NumericCode, a.Symbol, a.FractionSymbol, a.FractionsPerUnit, CurrencyRounding.Of(a.Rounding), a.TriangulationCurrency),
                (b, b.NumericCode, b.Symbol, b.FractionSymbol, b.FractionsPerUnit, CurrencyRounding.Of(b.Rounding), b.TriangulationCurrency));
            Assert.Equal(a, ConventionSet.QuantLib.Currency(code));
        }

        // the registries see QuantLib's currencies under the set as under QuantLib's provider
        Assert.Equal(1.95583, ExchangeRates.Default.Lookup(Under(ConventionSet.QuantLib, () => new EURCurrency()), new DEMCurrency(), Context).Rate, 1e-15);
    }

    [Fact]
    public void ACurrencyEditedInTheSetKeepsItsIdentityAndItsExchangeRates()
    {
        ConventionSet set = ConventionsText.OnQuantLib("""
            <CurrencyPreset name="EUR" base="EUR"><Symbol>€</Symbol><Rounding><Type>Up</Type><Precision>3</Precision><Digit>5</Digit></Rounding></CurrencyPreset>
            """);
        Currency eur = set.Currency("EUR");

        Assert.Equal(new EURCurrency(), eur);
        Assert.Equal("€", eur.Symbol);
        Assert.Equal(1.235, eur.Rounding.Evaluate(1.2341), 15);
        Assert.Equal(1.95583, ExchangeRates.Default.Lookup(eur, new DEMCurrency(), Context).Rate, 1e-15);
        using (ConventionProvider.Use(set))
        {
            Assert.Equal("€", new DEMCurrency().TriangulationCurrency!.Symbol);
        }
    }

    [Fact]
    public void ACurrencysIdentityCheckBuildsNoPresetThroughTheSet()
    {
        // EUR through DEM and DEM through USD is no cycle, though QuantLib's DEM triangulates through EUR
        ConventionSet set = ConventionsText.OnQuantLib(
            "<CurrencyPreset name=\"EUR\" base=\"EUR\"><TriangulationCurrency>DEM</TriangulationCurrency></CurrencyPreset>"
            + "<CurrencyPreset name=\"DEM\" base=\"DEM\"><TriangulationCurrency>USD</TriangulationCurrency></CurrencyPreset>");

        Assert.Equal("DEM", set.Currency("EUR").TriangulationCurrency!.Code);
        Assert.Equal("USD", set.Currency("DEM").TriangulationCurrency!.Code);
        Assert.Equal("EUR", set.Currency("ATS").TriangulationCurrency!.Code);
        Assert.Contains("part of a cycle of triangulation currencies", Assert.Throws<ArgumentException>(() => ConventionsText.OnQuantLib(
            "<CurrencyPreset name=\"EUR\" base=\"EUR\"><TriangulationCurrency>DEM</TriangulationCurrency></CurrencyPreset>")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheProvenanceReportGivesEachNamesSourceAndWarnsOfARecordNothingUses()
    {
        ConventionSnapshot mine = ConventionsText.Read(
            $"<Conventions {ConventionsText.Xmlns}><Calendar name=\"TARGET\" base=\"TARGET\" /><Calendar name=\"TARGTE\" base=\"TARGET\" /><Calendar name=\"Desk\" base=\"TARGET\" /><JointCalendar name=\"Both\"><Member>Desk</Member><Member>TARGET</Member></JointCalendar></Conventions>",
            "mine.xml");
        ConventionProvenance report = ConventionSet.QuantLib.With(mine).Provenance();

        ConventionOrigin Of(string name) => report.Origins.Single(o => o.Name == name && o.Kind == ConventionKind.Calendar);
        Assert.Equal(new ConventionOrigin(ConventionKind.Calendar, "TARGET", "Calendar", "mine.xml"), Of("TARGET"));
        Assert.Equal(new ConventionOrigin(ConventionKind.Calendar, "UnitedStates.NYSE", "CalendarPreset", null), Of("UnitedStates.NYSE"));
        Assert.Equal(new ConventionOrigin(ConventionKind.Alias, "EUTA", "Alias", null), report.Origins.Single(o => o.Name == "EUTA"));

        // TARGTE overrides nothing and nothing names it; Both is named by nothing either, but a joint calendar of one's
        // own is what a file is for, so only a record that names no preset and no record warns
        Assert.Contains("Calendar TARGTE (mine.xml) overrides no name and nothing references it", report.Warnings);
        Assert.DoesNotContain(report.Warnings, w => w.Contains("Desk", StringComparison.Ordinal) || w.Contains("TARGET ", StringComparison.Ordinal));
        Assert.Empty(ConventionSet.QuantLib.Provenance().Warnings);
    }

    [Fact]
    public void ThePresetsThatTakeArgumentsAreLookedUpWithThem()
    {
        ConventionSet set = ConventionSet.QuantLib;
        var lag = new Period(3, TimeUnit.Months);

        IborIndex daily = set.IborIndex("DailyTenorUSDLibor", 2);
        Assert.IsType<DailyTenorUSDLibor>(daily);
        Assert.Equal((new DailyTenorUSDLibor(2).Name, 2), (daily.Name, daily.FixingDays));
        Assert.IsType<BMAIndex>(set.InterestRateIndex("BMAIndex"));
        Assert.IsType<Euribor6M>(set.InterestRateIndex("Euribor6M"));

        ZeroInflationIndex au = set.ZeroInflationIndex("AUCPI", Frequency.Quarterly, false);
        Assert.IsType<AUCPI>(au);
        Assert.Equal((Frequency.Quarterly, false), (au.Frequency, au.Revised));
        ZeroInflationIndex generic = set.ZeroInflationIndex("GenericCPI", Frequency.Monthly, true, lag, new GBPCurrency());
        Assert.IsType<GenericCPI>(generic);
        Assert.Equal((Frequency.Monthly, true, lag, "GBP"), (generic.Frequency, generic.Revised, generic.AvailabilityLag, generic.Currency.Code));
        YoYInflationIndex yyau = set.YoYInflationIndex("YYAUCPI", Frequency.Quarterly, true);
        Assert.IsType<YYAUCPI>(yyau);
        Assert.Equal((Frequency.Quarterly, true), (yyau.Frequency, yyau.Revised));
        YoYInflationIndex yygeneric = set.YoYInflationIndex("YYGenericCPI", Frequency.Monthly, false, lag, new EURCurrency());
        Assert.IsType<YYGenericCPI>(yygeneric);
        Assert.Equal((Frequency.Monthly, false, lag, "EUR"), (yygeneric.Frequency, yygeneric.Revised, yygeneric.AvailabilityLag, yygeneric.Currency.Code));
    }

    [Theory]
    [InlineData("ibor", "True", "False")]
    [InlineData("interest rate", "US government bond market", "TARGET")]
    [InlineData("zero", "2M", "4M")]
    [InlineData("zero with lag", "Generic", "UK")]
    [InlineData("yoy", "2M", "4M")]
    [InlineData("yoy with lag", "Generic", "UK")]
    public void APresetsLookupWithArgumentsBuildsItInTheSet(string lookup, string quantLibs, string sets)
    {
        // each lookup's preset reads a record the set overrides: the daily-tenor LIBOR its fixing calendar's, the BMA
        // index its fixing calendar, AUCPI and YYAUCPI their availability lags, the generic CPIs their regions
        ConventionSet set = ConventionsText.OnQuantLib("""
            <Calendar name="UnitedKingdom.Exchange" base="UnitedKingdom.Exchange"><AddedHoliday>2026-10-16</AddedHoliday></Calendar>
            <IborIndex name="BMAIndex" base="BMAIndex"><FixingCalendar>TARGET</FixingCalendar></IborIndex>
            <ZeroInflationIndex name="AUCPI" base="AUCPI"><AvailabilityLag>4M</AvailabilityLag></ZeroInflationIndex>
            <YoYInflationIndex name="YYAUCPI" base="YYAUCPI"><AvailabilityLag>4M</AvailabilityLag></YoYInflationIndex>
            <ZeroInflationIndex name="GenericCPI" base="GenericCPI"><Region>UKRegion</Region></ZeroInflationIndex>
            <YoYInflationIndex name="YYGenericCPI" base="YYGenericCPI"><Region>UKRegion</Region></YoYInflationIndex>
            """);
        var lag = new Period(3, TimeUnit.Months);
        Func<ConventionSet, string> read = lookup switch
        {
            "ibor" => s => s.IborIndex("DailyTenorUSDLibor", 2).FixingCalendar.IsBusinessDay(Holiday).ToString(),
            "interest rate" => s => s.InterestRateIndex("BMAIndex").FixingCalendar.Name,
            "zero" => s => s.ZeroInflationIndex("AUCPI", Frequency.Quarterly, false).AvailabilityLag.ToString(),
            "zero with lag" => s => s.ZeroInflationIndex("GenericCPI", Frequency.Monthly, false, lag, new EURCurrency()).Region.Name,
            "yoy" => s => s.YoYInflationIndex("YYAUCPI", Frequency.Quarterly, false).AvailabilityLag.ToString(),
            _ => s => s.YoYInflationIndex("YYGenericCPI", Frequency.Monthly, false, lag, new EURCurrency()).Region.Name,
        };

        Assert.Equal((quantLibs, sets), (read(ConventionSet.QuantLib), read(set)));
    }

    [Theory]
    [InlineData("ibor")]
    [InlineData("interest rate")]
    [InlineData("zero")]
    [InlineData("zero with lag")]
    [InlineData("yoy")]
    [InlineData("yoy with lag")]
    public void APresetsLookupWithArgumentsFollowsTheSetsAlias(string lookup)
    {
        // a user's alias of each preset, as the set's other index lookups follow one
        ConventionSet set = ConventionsText.OnQuantLib("""
            <Alias name="USD-LIBOR-ON" base="DailyTenorUSDLibor" />
            <Alias name="USD-SIFMA" base="BMAIndex" />
            <Alias name="AU-CPI" base="AUCPI" />
            <Alias name="GENERIC-CPI" base="GenericCPI" />
            <Alias name="AU-YY-CPI" base="YYAUCPI" />
            <Alias name="GENERIC-YY-CPI" base="YYGenericCPI" />
            """);
        var lag = new Period(3, TimeUnit.Months);
        object index = lookup switch
        {
            "ibor" => set.IborIndex("USD-LIBOR-ON", 2),
            "interest rate" => set.InterestRateIndex("USD-SIFMA"),
            "zero" => set.ZeroInflationIndex("AU-CPI", Frequency.Quarterly, false),
            "zero with lag" => set.ZeroInflationIndex("GENERIC-CPI", Frequency.Monthly, false, lag, new EURCurrency()),
            "yoy" => set.YoYInflationIndex("AU-YY-CPI", Frequency.Quarterly, false),
            _ => set.YoYInflationIndex("GENERIC-YY-CPI", Frequency.Monthly, false, lag, new EURCurrency()),
        };

        Type expected = lookup switch
        {
            "ibor" => typeof(DailyTenorUSDLibor),
            "interest rate" => typeof(BMAIndex),
            "zero" => typeof(AUCPI),
            "zero with lag" => typeof(GenericCPI),
            "yoy" => typeof(YYAUCPI),
            _ => typeof(YYGenericCPI),
        };
        Assert.IsType(expected, index);
    }

    [Theory]
    [InlineData("ibor")]
    [InlineData("interest rate")]
    [InlineData("zero")]
    [InlineData("zero with lag")]
    [InlineData("yoy")]
    [InlineData("yoy with lag")]
    public void APresetsLookupWithArgumentsRefusesANameOfAnotherKind(string lookup)
    {
        ConventionSet set = ConventionSet.QuantLib;
        var lag = new Period(3, TimeUnit.Months);
        ArgumentException e = lookup switch
        {
            "ibor" => Assert.Throws<ArgumentException>(() => set.IborIndex("Euribor6M", 2)),
            "interest rate" => Assert.Throws<ArgumentException>(() => set.InterestRateIndex("UKRPI")),
            "zero" => Assert.Throws<ArgumentException>(() => set.ZeroInflationIndex("UKRPI", Frequency.Monthly, false)),
            "zero with lag" => Assert.Throws<ArgumentException>(() => set.ZeroInflationIndex("AUCPI", Frequency.Monthly, false, lag, new AUDCurrency())),
            "yoy" => Assert.Throws<ArgumentException>(() => set.YoYInflationIndex("YYUKRPI", Frequency.Monthly, false)),
            _ => Assert.Throws<ArgumentException>(() => set.YoYInflationIndex("YYAUCPI", Frequency.Monthly, false, lag, new AUDCurrency())),
        };

        Assert.Equal("name", e.ParamName);
    }

    [Fact]
    public void ANameAloneLookupOfAPresetThatTakesArgumentsNamesTheLookupThatTakesThem()
    {
        ConventionSet set = ConventionSet.QuantLib;

        Assert.Contains("IborIndex(name, settlementDays) takes them", Refusal(() => set.IborIndex("DailyTenorUSDLibor")), StringComparison.Ordinal);
        Assert.Contains("InterestRateIndex(name) gives it", Refusal(() => set.IborIndex("BMAIndex")), StringComparison.Ordinal);
        Assert.Contains("ZeroInflationIndex(name, frequency, revised) takes them", Refusal(() => set.ZeroInflationIndex("AUCPI")), StringComparison.Ordinal);
        Assert.Contains(
            "YoYInflationIndex(name, frequency, revised, availabilityLag, currency) takes them",
            Refusal(() => set.YoYInflationIndex("YYGenericCPI")),
            StringComparison.Ordinal);
        Assert.StartsWith("unknown interest-rate index Nowhere", Refusal(() => set.InterestRateIndex("Nowhere")), StringComparison.Ordinal);

        static string Refusal(Func<object> lookup) => Assert.Throws<ArgumentException>(lookup).Message;
    }

    private static T Under<T>(ConventionProvider provider, Func<T> build)
    {
        using (ConventionProvider.Use(provider))
        {
            return build();
        }
    }
}
