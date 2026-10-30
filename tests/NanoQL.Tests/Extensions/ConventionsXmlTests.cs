// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Xml.Schema;
using NanoQL.Extensions.Conventions;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// The reader and the writer: every kind, fully populated, through <see cref="ConventionsXml.Write(ConventionSnapshot, TextWriter)"/>
/// and <see cref="ConventionsXml.Read(TextReader, string?)"/>, its records equal and its text canonical; foreign
/// elements and attributes kept where they were read; the shipped files byte for byte; and the records, the schema and
/// nanoql's enumerations pinned to one another.
/// </summary>
public class ConventionsXmlTests
{
    private static readonly Date D1 = new(24, Month.December, 2026), D2 = new(31, Month.December, 2026), D3 = new(1, Month.May, 2026);

    private static readonly ForeignContent Foreign = new(
        [new ForeignAttribute("urn:firm", "id", "firm", "7")], ["<firm:note xmlns:firm=\"urn:firm\">kept</firm:note>"]);

    // A record of every kind, every field set.
    private static readonly ConventionSnapshot Full = ConventionSnapshot.Empty
        .Add(new CalendarPresetRecord("TARGET", "TARGET") { QuantLibName = "TARGET", AddedHolidays = [D1, D2], RemovedHolidays = [D3], Foreign = Foreign })
        .Add(new CalendarRecord("Desk", "UnitedKingdom.Settlement") { AddedHolidays = [D1], RemovedHolidays = [D3], Foreign = Foreign })
        .Add(new JointCalendarRecord("Desk+TARGET", "Joint") { Rule = JointCalendarRule.JoinBusinessDays, Members = ["TARGET", "Desk"], Foreign = Foreign })
        .Add(new BespokeCalendarRecord("Island", "Island") { Weekends = [Weekday.Friday, Weekday.Saturday], Holidays = [D1], RemovedHolidays = [D2], Foreign = Foreign })
        .Add(new DayCounterPresetRecord("Business252", "Business252") { QuantLibName = "Business/252(Brazil)", Calendar = "Island", Foreign = Foreign })
        .Add(new DayCounterRecord("Desk252", "Business252") { Calendar = "Desk", Foreign = Foreign })
        .Add(new CurrencyPresetRecord("EUR", "EUR")
        {
            QuantLibName = "European Euro",
            NumericCode = 978,
            Symbol = "E",
            FractionSymbol = "c",
            FractionsPerUnit = 100,
            Rounding = new CurrencyRounding(RoundingType.Closest, 2, 5),
            TriangulationCurrency = "",
            Foreign = Foreign,
        })
        .Add(new AliasRecord("TGT", "TARGET") { Foreign = Foreign })
        .Add(new RegionRecord("Atlantis", "Atlantis") { RegionName = "Atlantis", Code = "AT", Foreign = Foreign })
        .Add(new IborIndexRecord("Desk6M", "Desk")
        {
            Family = "Desk", Tenor = new Period(6, TimeUnit.Months), FixingDays = 2, Currency = "EUR", FixingCalendar = "TARGET",
            Convention = BusinessDayConvention.ModifiedFollowing, EndOfMonth = true, DayCounter = "Actual360", Style = IborIndexStyle.Custom,
            FinancialCenterCalendar = "Switzerland", ValueCalendar = "TARGET", MaturityCalendar = "UnitedKingdom.Settlement", Foreign = Foreign,
        })
        .Add(new IborFamilyRecord("Desk", "Euribor")
        {
            Family = "Desk", FixingDays = 2, FixingDaysAtOneDay = 0, Currency = "EUR", FixingCalendar = "TARGET", FinancialCenterCalendar = "TARGET",
            DayCounter = "Actual360", Units = [new IborFamilyUnit(TimeUnit.Weeks, BusinessDayConvention.Following, false), new IborFamilyUnit(TimeUnit.Months, BusinessDayConvention.ModifiedFollowing, true)],
            Foreign = Foreign,
        })
        .Add(new OvernightIndexRecord("DeskON", "Estr") { Family = "DeskON", FixingDays = 0, Currency = "EUR", FixingCalendar = "TARGET", DayCounter = "Actual360", Foreign = Foreign })
        .Add(new SwapIndexRecord("DeskSwap", "EuriborSwapIsdaFixA")
        {
            Family = "DeskSwap", SettlementDays = 2, Currency = "EUR", FixingCalendar = "TARGET", FixedLegTenor = new Period(1, TimeUnit.Years),
            FixedLegConvention = BusinessDayConvention.ModifiedFollowing, FixedLegDayCounter = "Thirty360.BondBasis", IborIndex = "Euribor",
            IborIndexTenor = new Period(6, TimeUnit.Months), ShortIborIndex = "Euribor", ShortIborIndexTenor = new Period(3, TimeUnit.Months),
            ShortFixedLegTenor = new Period(6, TimeUnit.Months), Foreign = Foreign,
        })
        .Add(new OvernightIndexedSwapIndexRecord("DeskOis", "DeskOis")
        {
            Family = "DeskOis", SettlementDays = 2, Currency = "EUR", OvernightIndex = "Estr", TelescopicValueDates = true,
            Averaging = Coupons.RateAveragingType.Simple, Foreign = Foreign,
        })
        .Add(new ZeroInflationIndexRecord("DeskCPI", "EUHICP")
        {
            Family = "CPI", Region = "Atlantis", Revised = true, Frequency = Frequency.Quarterly, AvailabilityLag = new Period(2, TimeUnit.Months),
            Currency = "EUR", Foreign = Foreign,
        })
        .Add(new YoYInflationIndexRecord("YYDeskCPI", "YYEUHICP")
        {
            UnderlyingIndex = "DeskCPI", Family = "YY_CPI", Region = "Atlantis", Revised = true, Frequency = Frequency.Quarterly,
            AvailabilityLag = new Period(2, TimeUnit.Months), Currency = "EUR", Foreign = Foreign,
        });

    [Fact]
    public void EveryKindFullyPopulatedRoundTripsWithItsRecordsEqualAndItsTextCanonical()
    {
        string text = ConventionsText.Write(Full);
        ConventionSnapshot read = ConventionsText.Read(text);

        Assert.Equal(Full.Records, read.Records);
        Assert.Equal(Full, read);
        Assert.Equal(text, ConventionsText.Write(read));
        Assert.StartsWith($"<Conventions {ConventionsText.Xmlns}>\n  <CalendarPreset name=\"TARGET\" base=\"TARGET\" ", text);
        Assert.EndsWith("</Conventions>\n", text);
        Assert.DoesNotContain("\r", text);
    }

    [Fact]
    public void TheWritersStreamIsUtf8WithoutAByteOrderMarkAndItsTextTheTextWritersText()
    {
        using var stream = new MemoryStream();
        ConventionsXml.Write(Full, stream);
        byte[] bytes = stream.ToArray();

        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Equal(ConventionsText.Write(Full), new UTF8Encoding(false, true).GetString(bytes));
    }

    [Fact]
    public void ForeignElementsAndAttributesAreKeptWhereTheyWereRead()
    {
        string text = $"""
            <Conventions {ConventionsText.Xmlns} xmlns:firm="urn:firm" firm:owner="desk">
              <firm:header>h</firm:header>
              <CalendarPreset name="TARGET" firm:id="1">
                <QuantLibName>TARGET</QuantLibName>
                <firm:note>n</firm:note>
              </CalendarPreset>
              <firm:between a="1" />
              <Alias name="TGT" base="TARGET" />
              <firm:footer />
            </Conventions>
            """;
        ConventionSnapshot read = ConventionsText.Read(text);

        Assert.Equal([new ForeignAttribute("urn:firm", "owner", "firm", "desk")], read.RootAttributes);
        Assert.Equal([0, 1, 2], read.RootElements.Select(e => e.Position));
        Assert.Equal("<firm:between a=\"1\" xmlns:firm=\"urn:firm\" />", read.RootElements[1].Element);
        Assert.Equal([new ForeignAttribute("urn:firm", "id", "firm", "1")], read.Records[0].Foreign.Attributes);
        Assert.Equal(["<firm:note xmlns:firm=\"urn:firm\">n</firm:note>"], read.Records[0].Foreign.Elements);

        // the canonical text keeps each in place, and reads back to the same snapshot
        string written = ConventionsText.Write(read);
        Assert.Equal(read, ConventionsText.Read(written));
        string[] lines = written.Split('\n');
        Assert.Contains("firm:header", lines[1]);
        Assert.Contains("firm:note", lines[4]);
        Assert.Contains("firm:between", lines[6]);
        Assert.Contains("firm:footer", lines[8]);
    }

    [Fact]
    public void ForeignContentComparesByItsText()
    {
        ForeignContent a = new([new ForeignAttribute("urn:firm", "id", "firm", "7")], ["<firm:x xmlns:firm=\"urn:firm\" />"]);
        ForeignContent b = new([new ForeignAttribute("urn:firm", "id", "firm", "7")], ["<firm:x xmlns:firm=\"urn:firm\" />"]);
        ForeignContent c = new([new ForeignAttribute("urn:firm", "id", "firm", "8")], ["<firm:x xmlns:firm=\"urn:firm\" />"]);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
        Assert.NotEqual(new AliasRecord("TGT", "TARGET") { Foreign = a }, new AliasRecord("TGT", "TARGET") { Foreign = c });
    }

    [Fact]
    public void TheShippedFilesRoundTripByteForByteAfterTheirHeaders()
    {
        string folder = Directory.CreateTempSubdirectory("nanoql-defaults-").FullName;
        try
        {
            ConventionsXml.WriteDefaults(folder);
            string[] files = [.. Directory.GetFiles(folder).Select(Path.GetFileName).Order(StringComparer.Ordinal)!];
            Assert.Equal(["aliases.xml", "calendars.xml", "commodities.cxx.xml", "currencies.xml", "daycounters.xml", "indexes.cxx.xml", "indexes.xml", "instruments.cxx.xml", "instruments.xml", "nanoql-conventions.xsd"], files);
            foreach (string file in files.Where(f => f.EndsWith(".xml", StringComparison.Ordinal)))
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(folder, file));
                string text = new UTF8Encoding(false, true).GetString(bytes);
                Assert.NotEqual(0xEF, bytes[0]);
                int body = text.IndexOf("-->\n", StringComparison.Ordinal) + "-->\n".Length;
                Assert.StartsWith("<!--\n  SPDX-FileCopyrightText: 2026 The nanoql Authors\n", text);
                Assert.Equal(text[body..], ConventionsText.Write(ConventionsXml.Read(Path.Combine(folder, file))));
            }

            // the defaults, read alone onto the empty set, are QuantLib's set
            ConventionSet copy = ConventionSet.Empty.With(ConventionsXml.Read(files.Where(f => f.EndsWith(".xml", StringComparison.Ordinal))
                .Select(f => Path.Combine(folder, f)).OrderBy(PresetOrder)));
            Assert.Equal(ConventionSet.QuantLib.Layers[0].Records, copy.Layers[0].Records);
        }
        finally
        {
            Directory.Delete(folder, true);
        }

        static int PresetOrder(string path) =>
            Array.IndexOf(["calendars.xml", "daycounters.xml", "currencies.xml", "indexes.xml", "indexes.cxx.xml", "instruments.xml", "instruments.cxx.xml", "commodities.cxx.xml", "aliases.xml"], Path.GetFileName(path));
    }

    [Fact]
    public void EveryRecordPropertyHasItsSchemaItemAndEverySchemaItemItsProperty()
    {
        XmlSchemaSet schema = ConventionsXml.Schema();
        Type[] kinds = [.. typeof(ConventionRecord).Assembly.GetExportedTypes().Where(t => t.IsSubclassOf(typeof(ConventionRecord))).OrderBy(t => t.Name, StringComparer.Ordinal)];
        Assert.Equal(38, kinds.Length);
        foreach (Type kind in kinds)
        {
            var element = (XmlSchemaElement)schema.GlobalElements[new(kind.Name[..^"Record".Length], ConventionsXml.Namespace)]!;
            Assert.NotNull(element);
            var type = (XmlSchemaComplexType)element.ElementSchemaType!;
            Assert.Equal(Properties(kind), Items(type));
            Assert.True(kind.IsSealed, kind.Name);
        }

        // the nested types: a currency's rounding, an Ibor family's unit and a currency's threshold
        foreach ((string name, Type type) in new[] { ("Rounding", typeof(CurrencyRounding)), ("IborFamilyUnit", typeof(IborFamilyUnit)), ("SwapTenorThreshold", typeof(Instruments.SwapTenorThreshold)) })
        {
            var complex = (XmlSchemaComplexType)schema.GlobalTypes[new(name, ConventionsXml.Namespace)]!;
            Assert.Equal(type.GetProperties().Where(p => p.Name != "EqualityContract").Select(p => p.Name).Order(StringComparer.Ordinal), Items(complex).Order(StringComparer.Ordinal));
        }

        // a record's properties as the schema names them: its name, its base and its fields, a list by its item
        static string[] Properties(Type kind) => [.. kind.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name is not ("Foreign" or "EqualityContract"))
            .Select(p => p.Name switch
            {
                "Name" => "@name",
                "Base" => "@base",
                _ when p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(ImmutableArray<>) => p.Name[..^1],
                _ => p.Name,
            })
            .Order(StringComparer.Ordinal)];

        static string[] Items(XmlSchemaComplexType type) => [.. type.AttributeUses.Values.Cast<XmlSchemaAttribute>().Select(a => "@" + a.Name)
            .Concat(((XmlSchemaSequence)type.ContentTypeParticle).Items.OfType<XmlSchemaElement>().Select(e => e.Name!))
            .Order(StringComparer.Ordinal)];
    }

    [Fact]
    public void TheSchemasEnumerationsAreNanoqlsEnumerationsAndTheShippedAliases()
    {
        XmlSchemaSet schema = ConventionsXml.Schema();
        string[] Values(string type) => [.. ((XmlSchemaSimpleTypeRestriction)((XmlSchemaSimpleType)schema.GlobalTypes[new(type, ConventionsXml.Namespace)]!).Content!)
            .Facets.OfType<XmlSchemaEnumerationFacet>().Select(f => f.Value!)];

        Assert.Equal(Enum.GetNames<Weekday>().Order(StringComparer.Ordinal), Values("Weekday").Order(StringComparer.Ordinal));
        Assert.Equal(Enum.GetNames<JointCalendarRule>().Order(StringComparer.Ordinal), Values("JointCalendarRule").Order(StringComparer.Ordinal));
        Assert.Equal(Enum.GetNames<RoundingType>().Order(StringComparer.Ordinal), Values("RoundingType").Order(StringComparer.Ordinal));
        Assert.Equal(Enum.GetNames<Frequency>().Order(StringComparer.Ordinal), Values("Frequency").Order(StringComparer.Ordinal));
        Assert.Equal(Enum.GetNames<IborIndexStyle>().Order(StringComparer.Ordinal), Values("IborIndexStyle").Order(StringComparer.Ordinal));
        Assert.Equal(Enum.GetNames<Coupons.RateAveragingType>().Order(StringComparer.Ordinal), Values("RateAveragingType").Order(StringComparer.Ordinal));
        Assert.Equal(Enum.GetNames<DateGenerationRule>().Order(StringComparer.Ordinal), Values("DateGenerationRule").Order(StringComparer.Ordinal));
        Assert.Equal(Enum.GetNames<Instruments.SettlementType>().Order(StringComparer.Ordinal), Values("SettlementType").Order(StringComparer.Ordinal));
        Assert.Equal(Enum.GetNames<Instruments.SettlementMethod>().Order(StringComparer.Ordinal), Values("SettlementMethod").Order(StringComparer.Ordinal));
        Assert.Equal(Enum.GetNames<Indexes.CPIInterpolationType>().Order(StringComparer.Ordinal), Values("CPIInterpolationType").Order(StringComparer.Ordinal));
        Assert.Equal(Enum.GetNames<Instruments.BondPriceType>().Order(StringComparer.Ordinal), Values("BondPriceType").Order(StringComparer.Ordinal));
        Assert.Equal(Enum.GetNames<Instruments.CreditDefaultSwapPricingModel>().Order(StringComparer.Ordinal), Values("CreditDefaultSwapPricingModel").Order(StringComparer.Ordinal));
        Assert.Equal(Enum.GetNames<Instruments.SwapTenorComparison>().Order(StringComparer.Ordinal), Values("SwapTenorComparison").Order(StringComparer.Ordinal));

        // a tenor's units, those a period's short form writes
        Assert.Equal(["Days", "Months", "Weeks", "Years"], Values("TimeUnit").Order(StringComparer.Ordinal));
        Assert.All(Values("TimeUnit"), u => Assert.True(Enum.IsDefined(Enum.Parse<TimeUnit>(u))));

        // a business-day convention by its member's name, or by the shipped alias of one
        string[] aliases = [.. ConventionSet.QuantLib.Layers[0].Records.OfType<AliasRecord>()
            .Where(a => Enum.GetNames<BusinessDayConvention>().Contains(a.Base)).Select(a => a.Name)];
        Assert.Equal(5, aliases.Length);
        Assert.Equal(Enum.GetNames<BusinessDayConvention>().Concat(aliases).Order(StringComparer.Ordinal), Values("BusinessDayConvention").Order(StringComparer.Ordinal));
    }
}
