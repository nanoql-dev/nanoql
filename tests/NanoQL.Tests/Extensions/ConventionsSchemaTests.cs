// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using NanoQL.Extensions.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// The schema, applied on every read: a valid file reads; a record missing a required field, an unknown element or
/// attribute in the conventions' namespace, unqualified foreign content, a duplicate name in a value kind, a date
/// outside QuantLib's range or with a time zone, and a DTD are refused, a schema violation as .NET's exception with its
/// line and position; a <c>Conventions</c> element inside another document reads; several files read into one snapshot
/// refuse a name two of them define; and no schema name hints at time or versioning.
/// </summary>
public class ConventionsSchemaTests
{
    [Fact]
    public void AValidFileReads()
    {
        ConventionSnapshot s = ConventionsText.Records("""
            <CalendarPreset name="TARGET"><QuantLibName>TARGET</QuantLibName><AddedHoliday>2026-12-24</AddedHoliday></CalendarPreset>
            <Calendar name="Desk" base="TARGET"><RemovedHoliday>2026-05-01</RemovedHoliday></Calendar>
            <JointCalendar name="Both"><Rule>JoinHolidays</Rule><Member>TARGET</Member><Member>Desk</Member></JointCalendar>
            <BespokeCalendar name="Island"><Weekend>Fri</Weekend><Weekend>Saturday</Weekend><Holiday>2026-01-01</Holiday></BespokeCalendar>
            <DayCounter name="Desk252" base="Business252"><Calendar>Desk</Calendar></DayCounter>
            <CurrencyPreset name="EUR" base="EUR"><Rounding><Type>Up</Type><Precision>3</Precision><Digit>5</Digit></Rounding></CurrencyPreset>
            <Alias name="Tgt" base="TARGET" />
            """);

        Assert.Equal(7, s.Records.Length);
        Assert.Equal([new Date(24, Month.December, 2026)], ((CalendarPresetRecord)s.Records[0]).AddedHolidays);
        Assert.Equal([Weekday.Friday, Weekday.Saturday], ((BespokeCalendarRecord)s.Records[3]).Weekends);
        Assert.Equal(new CurrencyRounding(Numerics.RoundingType.Up, 3, 5), ((CurrencyPresetRecord)s.Records[5]).Rounding);
    }

    [Theory]
    [InlineData("<CalendarPreset><QuantLibName>TARGET</QuantLibName></CalendarPreset>", "name")]
    [InlineData("<Calendar name=\"Desk\" />", "base")]
    [InlineData("<JointCalendar><Member>TARGET</Member></JointCalendar>", "name")]
    [InlineData("<BespokeCalendar />", "name")]
    [InlineData("<DayCounterPreset />", "name")]
    [InlineData("<DayCounter name=\"Desk252\" />", "base")]
    [InlineData("<CurrencyPreset />", "name")]
    [InlineData("<CurrencyPreset name=\"EUR\" base=\"EUR\"><Rounding><Precision>2</Precision></Rounding></CurrencyPreset>", "Type")]
    [InlineData("<Alias name=\"Desk\" />", "base")]
    public void ARecordMissingARequiredFieldIsRefusedWithItsLineAndPosition(string record, string field)
    {
        XmlSchemaValidationException e = Assert.Throws<XmlSchemaValidationException>(() => ConventionsText.Records(record));

        Assert.Contains(field, e.Message, StringComparison.Ordinal);
        Assert.Equal(1, e.LineNumber);
        Assert.True(e.LinePosition > 0);
    }

    [Theory]
    [InlineData("<CalendarPreset name=\"TARGET\"><AddedHolidays>2026-12-24</AddedHolidays></CalendarPreset>")]
    [InlineData("<CalendarPreset name=\"TARGET\" market=\"x\" />")]
    [InlineData("<Holidays />")]
    [InlineData("<CalendarPreset name=\"TARGET\"><AddedHoliday>2026-12-24</AddedHoliday><QuantLibName>TARGET</QuantLibName></CalendarPreset>")]
    [InlineData("<JointCalendar name=\"J\"><Rule>JoinAll</Rule></JointCalendar>")]
    [InlineData("<BespokeCalendar name=\"B\"><Weekend>Funday</Weekend></BespokeCalendar>")]
    [InlineData("<Alias name=\" padded\" base=\"TARGET\" />")]
    public void AnUnknownOrMisplacedItemInTheNamespaceOrABadValueIsRefused(string record)
    {
        Assert.Throws<XmlSchemaValidationException>(() => ConventionsText.Records(record));
    }

    [Theory]
    [InlineData("<Note xmlns=\"\">x</Note>")]
    [InlineData("<CalendarPreset name=\"TARGET\"><Note xmlns=\"\">x</Note></CalendarPreset>")]
    public void UnqualifiedForeignContentIsRefused(string record)
    {
        Assert.Throws<XmlSchemaValidationException>(() => ConventionsText.Records(record));
    }

    [Fact]
    public void ForeignContentAfterARecordsFieldsIsTakenAndBeforeThemRefused()
    {
        Assert.Single(ConventionsText.Records("<CalendarPreset name=\"TARGET\"><QuantLibName>TARGET</QuantLibName><f:x xmlns:f=\"urn:f\" /></CalendarPreset>").Records);
        Assert.Throws<XmlSchemaValidationException>(
            () => ConventionsText.Records("<CalendarPreset name=\"TARGET\"><f:x xmlns:f=\"urn:f\" /><QuantLibName>TARGET</QuantLibName></CalendarPreset>"));
    }

    [Theory]
    [InlineData("<Calendar name=\"A\" base=\"TARGET\" /><JointCalendar name=\"A\"><Member>TARGET</Member></JointCalendar>")]
    [InlineData("<DayCounterPreset name=\"Actual360\" /><DayCounter name=\"Actual360\" base=\"Actual360\" />")]
    [InlineData("<CurrencyPreset name=\"EUR\" base=\"EUR\" /><Alias name=\"EUR\" base=\"USD\" />")]
    [InlineData("<Calendar name=\"A\" base=\"TARGET\" /><Alias name=\"A\" base=\"TARGET\" />")]
    public void ADuplicateNameInAValueKindIsRefused(string records)
    {
        Assert.Throws<XmlSchemaValidationException>(() => ConventionsText.Records(records));
    }

    [Fact]
    public void ANameInTwoValueKindsIsTaken()
    {
        ConventionSnapshot s = ConventionsText.Records("<Calendar name=\"Desk\" base=\"TARGET\" /><DayCounter name=\"Desk\" base=\"Actual360\" />");

        Assert.Equal(2, s.Records.Length);
    }

    [Fact]
    public void AConventionsElementInsideAnotherDocumentReads()
    {
        string text = $"""
            <firm:Configuration xmlns:firm="urn:firm">
              <firm:Settings />
              <Conventions {ConventionsText.Xmlns}><Alias name="Desk" base="TARGET" /></Conventions>
            </firm:Configuration>
            """;

        Assert.Equal([new AliasRecord("Desk", "TARGET")], ConventionsText.Read(text).Records);
        Assert.Equal([new AliasRecord("Desk", "TARGET")], ConventionsXml.Read(XElement.Parse(text)).Records);
        ArgumentException e = Assert.Throws<ArgumentException>(() => ConventionsText.Read("<firm:Configuration xmlns:firm=\"urn:firm\" />"));
        Assert.Contains("no Conventions element", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADtdIsRefused()
    {
        string text = $"<!DOCTYPE Conventions [<!ENTITY e \"TARGET\">]><Conventions {ConventionsText.Xmlns}><Alias name=\"Desk\" base=\"&e;\" /></Conventions>";

        Assert.Throws<XmlException>(() => ConventionsText.Read(text));
    }

    [Theory]
    [InlineData("1901-01-01", true)]
    [InlineData("2199-12-31", true)]
    [InlineData("1900-12-31", false)]
    [InlineData("2200-01-01", false)]
    [InlineData("2026-10-30Z", false)]
    [InlineData("2026-10-30+01:00", false)]
    [InlineData("2026-02-30", false)]
    [InlineData("26-10-30", false)]
    public void ADateIsInQuantLibsRangeWithoutATimeZone(string date, bool taken)
    {
        string record = $"<Calendar name=\"Desk\" base=\"TARGET\"><AddedHoliday>{date}</AddedHoliday></Calendar>";
        if (taken)
        {
            var read = (CalendarRecord)Assert.Single(ConventionsText.Records(record).Records);
            Assert.Equal(date, $"{read.AddedHolidays[0].Year:D4}-{(int)read.AddedHolidays[0].Month:D2}-{read.AddedHolidays[0].DayOfMonth:D2}");
        }
        else
        {
            Assert.Throws<XmlSchemaValidationException>(() => ConventionsText.Records(record));
        }
    }

    [Fact]
    public void SeveralFilesReadIntoOneSnapshotRefusingANameTwoDefine()
    {
        string folder = Directory.CreateTempSubdirectory("nanoql-files-").FullName;
        try
        {
            string File(string name, string records)
            {
                string path = Path.Combine(folder, name);
                System.IO.File.WriteAllText(path, $"<Conventions {ConventionsText.Xmlns}>{records}</Conventions>");
                return path;
            }

            string uk = File("uk.xml", "<Calendar name=\"London\" base=\"UnitedKingdom.Settlement\" />");
            string eu = File("eu.xml", "<Calendar name=\"Frankfurt\" base=\"TARGET\" /><DayCounter name=\"London\" base=\"Actual365Fixed\" />");
            string again = File("again.xml", "<Alias name=\"London\" base=\"TARGET\" />");

            ConventionSnapshot both = ConventionsXml.Read([uk, eu]);
            Assert.Equal(["London", "Frankfurt", "London"], both.Records.Select(r => r.Name));
            Assert.Equal([uk, eu, eu], both.Sources);
            ArgumentException e = Assert.Throws<ArgumentException>(() => ConventionsXml.Read([uk, again]));
            Assert.Contains("London", e.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void NoSchemaNameHintsAtTimeOrVersioning()
    {
        // A convention has no validity dates and the format no version: no word of a name starts with one of these, and
        // no two read "as of" (QuantLib's unit conversions are no versions)
        string[] banned = ["valid", "known", "asof", "version", "effective", "expir"];
        string[] names = [.. SchemaDocument().Descendants().Attributes("name").Select(a => a.Value)];

        Assert.NotEmpty(names);
        Assert.Contains("UnitOfMeasureConversion", names);
        Assert.All(names, n =>
        {
            string[] words = [.. Regex.Split(n, "(?<=[a-z0-9])(?=[A-Z])|[^A-Za-z0-9]+").Where(w => w.Length > 0).Select(w => w.ToLowerInvariant())];
            Assert.DoesNotContain(words, w => banned.Any(b => w.StartsWith(b, StringComparison.Ordinal)));
            Assert.DoesNotContain(words.Zip(words.Skip(1)), p => p is ("as", "of"));
        });
    }

    [Fact]
    public void EveryElementAndAttributeTheSchemaDeclaresIsDocumented()
    {
        // Editors show each item's documentation
        XNamespace xs = "http://www.w3.org/2001/XMLSchema";
        XElement[] declared = [.. SchemaDocument().Descendants()
            .Where(e => (e.Name == xs + "element" || e.Name == xs + "attribute") && e.Attribute("name") is not null)];

        Assert.True(declared.Length > 30, $"{declared.Length} declarations");
        Assert.Empty(declared.Where(e => string.IsNullOrWhiteSpace((string?)e.Element(xs + "annotation")?.Element(xs + "documentation")))
            .Select(e => (string)e.Attribute("name")!));
    }

    // The schema as the defaults export it.
    private static XDocument SchemaDocument()
    {
        string folder = Directory.CreateTempSubdirectory("nanoql-schema-").FullName;
        try
        {
            ConventionsXml.WriteDefaults(folder);
            return XDocument.Load(Path.Combine(folder, "nanoql-conventions.xsd"));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
