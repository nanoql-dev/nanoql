// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// Reads and writes conventions documents: one <c>Conventions</c> element in the namespace
/// <see cref="Namespace"/>, validated by its XML Schema on every read.
/// </summary>
/// <remarks>
/// <para>
/// A document's root may be the <c>Conventions</c> element or any element holding one. A schema violation is the
/// <see cref="XmlSchemaValidationException"/> .NET raises, with its line and position. DTDs are refused and nothing
/// outside the document is resolved.
/// </para>
/// <para>
/// The writer writes the canonical form: the records in order, attributes <c>name</c> and <c>base</c> first, two-space
/// indents, LF line ends, UTF-8 without a byte-order mark and a final newline; each record's and the root's foreign
/// attributes and elements are written back where they were read.
/// </para>
/// </remarks>
public static class ConventionsXml
{
    /// <summary>The conventions' namespace.</summary>
    public const string Namespace = "https://nanoql.net/schemas/conventions";

    private const string SchemaResource = "NanoQL.Extensions.Conventions.nanoql-conventions.xsd";

    // The shipped presets' resources, in the order QuantLib's set reads them, with the file names they export as.
    private static readonly (string Resource, string File)[] PresetFiles =
    [
        ("NanoQL.Extensions.Conventions.Presets.calendars.xml", "calendars.xml"),
        ("NanoQL.Extensions.Conventions.Presets.daycounters.xml", "daycounters.xml"),
        ("NanoQL.Extensions.Conventions.Presets.currencies.xml", "currencies.xml"),
        ("NanoQL.Extensions.Conventions.Presets.aliases.xml", "aliases.xml"),
    ];

    private static readonly XNamespace Ns = Namespace;

    /// <summary>The conventions' XML Schema, compiled afresh: a schema set's members aren't safe across threads.</summary>
    /// <returns>The compiled schema set.</returns>
    public static XmlSchemaSet Schema()
    {
        var set = new XmlSchemaSet { XmlResolver = null };
        using Stream stream = Resource(SchemaResource);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        set.Add(Namespace, reader);
        set.Compile();
        return set;
    }

    /// <summary>Reads a conventions document from a file.</summary>
    /// <param name="path">The file's path, each record's source.</param>
    /// <returns>Its records.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="XmlSchemaValidationException">The document breaks the schema.</exception>
    /// <exception cref="XmlException">The file isn't well-formed XML or holds a DTD.</exception>
    /// <exception cref="ArgumentException">The document holds no <c>Conventions</c> element, or a record's fields
    /// can't make a record.</exception>
    public static ConventionSnapshot Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using FileStream stream = File.OpenRead(path);
        return Read(stream, path);
    }

    /// <summary>Reads several conventions files into one snapshot, in order, refusing a name two of them define.</summary>
    /// <param name="paths">The files' paths.</param>
    /// <returns>Their records.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="paths"/> or a path is null.</exception>
    /// <exception cref="ArgumentException">Two files define one name in one value kind.</exception>
    /// <exception cref="XmlSchemaValidationException">A document breaks the schema.</exception>
    public static ConventionSnapshot Read(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ConventionSnapshot result = ConventionSnapshot.Empty;
        foreach (string path in paths)
        {
            result = Merge(result, Read(path));
        }

        return result;
    }

    /// <summary>Reads a conventions document from a stream.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="source">The source each record reports, or <see langword="null"/>.</param>
    /// <returns>Its records.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="XmlSchemaValidationException">The document breaks the schema.</exception>
    public static ConventionSnapshot Read(Stream stream, string? source = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = XmlReader.Create(stream, Settings());
        return FromDocument(reader, source);
    }

    /// <summary>Reads a conventions document from a text reader.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="source">The source each record reports, or <see langword="null"/>.</param>
    /// <returns>Its records.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="XmlSchemaValidationException">The document breaks the schema.</exception>
    public static ConventionSnapshot Read(TextReader reader, string? source = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        using var xml = XmlReader.Create(reader, Settings());
        return FromDocument(xml, source);
    }

    /// <summary>Reads a conventions document from an XML reader, validating it as it is read.</summary>
    /// <param name="reader">The reader, positioned before the document or the element holding the conventions.</param>
    /// <param name="source">The source each record reports, or <see langword="null"/>.</param>
    /// <returns>Its records.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="XmlSchemaValidationException">The document breaks the schema.</exception>
    public static ConventionSnapshot Read(XmlReader reader, string? source = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        using var xml = XmlReader.Create(reader, Settings());
        return FromDocument(xml, source);
    }

    /// <summary>Reads a <c>Conventions</c> element, or the first one an element holds, validating it.</summary>
    /// <param name="element">The element.</param>
    /// <param name="source">The source each record reports, or <see langword="null"/>.</param>
    /// <returns>Its records.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="element"/> is null.</exception>
    /// <exception cref="XmlSchemaValidationException">The element breaks the schema.</exception>
    public static ConventionSnapshot Read(XElement element, string? source = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        using XmlReader reader = element.CreateReader();
        return Read(reader, source);
    }

    /// <summary>Writes a snapshot as a conventions document in the canonical form.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="stream">The stream, written UTF-8 without a byte-order mark.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static void Write(ConventionSnapshot snapshot, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(stream);
        using var text = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\n" };
        Write(snapshot, text);
    }

    /// <summary>Writes a snapshot as a conventions document in the canonical form, with a final newline.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="writer">The writer.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static void Write(ConventionSnapshot snapshot, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(writer);
        using (var xml = XmlWriter.Create(writer, WriterSettings()))
        {
            Write(snapshot, xml);
        }

        writer.Write('\n');
        writer.Flush();
    }

    /// <summary>Writes a snapshot's <c>Conventions</c> element to an XML writer.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="writer">The writer, which keeps its own settings.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static void Write(ConventionSnapshot snapshot, XmlWriter writer)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(writer);
        ToXElement(snapshot).WriteTo(writer);
    }

    /// <summary>A snapshot's <c>Conventions</c> element.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns>The element, its records in order with the foreign content where it was read.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    public static XElement ToXElement(ConventionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var root = new XElement(Ns + "Conventions", new XAttribute("xmlns", Namespace));
        Foreign(root, snapshot.RootAttributes);
        int next = 0;
        for (int i = 0; i <= snapshot.Records.Length; ++i)
        {
            for (; next < snapshot.RootElements.Length && snapshot.RootElements[next].Position == i; ++next)
            {
                root.Add(XElement.Parse(snapshot.RootElements[next].Element));
            }

            if (i < snapshot.Records.Length)
            {
                root.Add(Element(snapshot.Records[i]));
            }
        }

        return root;
    }

    /// <summary>
    /// Writes QuantLib's presets, as the library ships them, and the schema into a folder: a full copy of the defaults,
    /// to read alone onto <see cref="ConventionSet.Empty"/>.
    /// </summary>
    /// <param name="folder">The folder, created if needed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="folder"/> is null.</exception>
    public static void WriteDefaults(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        Directory.CreateDirectory(folder);
        foreach ((string resource, string file) in PresetFiles.Append((SchemaResource, "nanoql-conventions.xsd")))
        {
            using Stream input = Resource(resource);
            using FileStream output = File.Create(Path.Combine(folder, file));
            input.CopyTo(output);
        }
    }

    // QuantLib's presets, the shipped files read into one snapshot.
    internal static ConventionSnapshot ReadPresets()
    {
        ConventionSnapshot result = ConventionSnapshot.Empty;
        foreach ((string resource, string file) in PresetFiles)
        {
            using Stream stream = Resource(resource);
            result = Merge(result, Read(stream, file));
        }

        return result;
    }

    private static ConventionSnapshot Merge(ConventionSnapshot into, ConventionSnapshot add)
    {
        int offset = into.Records.Length;
        for (int i = 0; i < add.Records.Length; ++i)
        {
            into = into.Add(add.Records[i], add.Sources[i]);
        }

        foreach ((int position, string element) in add.RootElements)
        {
            into = into.InsertRootElement(offset + position, element);
        }

        // the first file's root attributes, then each later file's that names another attribute
        IEnumerable<ForeignAttribute> later = add.RootAttributes.Where(
            a => !into.RootAttributes.Any(b => b.Namespace == a.Namespace && b.LocalName == a.LocalName));
        return into.WithRootAttributes(into.RootAttributes.Concat(later));
    }

    private static Stream Resource(string name) =>
        typeof(ConventionsXml).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"the resource {name} is missing");

    private static XmlReaderSettings Settings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        ValidationType = ValidationType.Schema,
        Schemas = Schema(),
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
    };

    private static XmlWriterSettings WriterSettings() => new()
    {
        Indent = true,
        IndentChars = "  ",
        NewLineChars = "\n",
        NewLineHandling = NewLineHandling.Replace,
        OmitXmlDeclaration = true,
        Encoding = new UTF8Encoding(false),
    };

    private static ConventionSnapshot FromDocument(XmlReader reader, string? source)
    {
        XDocument document = XDocument.Load(reader, LoadOptions.SetLineInfo);
        XElement root = document.Root is { } r && r.Name == Ns + "Conventions"
            ? r
            : document.Descendants(Ns + "Conventions").FirstOrDefault()
              ?? throw new ArgumentException($"the document holds no Conventions element in {Namespace}", nameof(reader));
        ConventionSnapshot snapshot = ConventionSnapshot.Empty.WithRootAttributes(ForeignAttributes(root));
        foreach (XElement child in root.Elements())
        {
            snapshot = child.Name.Namespace == Ns
                ? snapshot.Add(Record(child), source)
                : snapshot.AddRootElement(Text(child));
        }

        return snapshot;
    }

    // A record from its element, which the schema has validated.
    private static ConventionRecord Record(XElement e)
    {
        string name = (string)e.Attribute("name")!;
        string? @base = (string?)e.Attribute("base");
        ForeignContent foreign = new(ForeignAttributes(e), e.Elements().Where(c => c.Name.Namespace != Ns).Select(Text));
        try
        {
            ConventionRecord record = e.Name.LocalName switch
            {
                "CalendarPreset" => new CalendarPresetRecord(name, @base)
                {
                    QuantLibName = Field(e, "QuantLibName"),
                    AddedHolidays = Dates(e, "AddedHoliday"),
                    RemovedHolidays = Dates(e, "RemovedHoliday"),
                },
                "Calendar" => new CalendarRecord(name, @base!)
                {
                    AddedHolidays = Dates(e, "AddedHoliday"),
                    RemovedHolidays = Dates(e, "RemovedHoliday"),
                },
                "JointCalendar" => new JointCalendarRecord(name, @base)
                {
                    Rule = Field(e, "Rule") is string rule ? Enum.Parse<JointCalendarRule>(rule) : null,
                    Members = [.. Fields(e, "Member")],
                },
                "BespokeCalendar" => new BespokeCalendarRecord(name, @base)
                {
                    Weekends = [.. Fields(e, "Weekend").Select(Enum.Parse<Weekday>)],
                    Holidays = Dates(e, "Holiday"),
                    RemovedHolidays = Dates(e, "RemovedHoliday"),
                },
                "DayCounterPreset" => new DayCounterPresetRecord(name, @base)
                {
                    QuantLibName = Field(e, "QuantLibName"),
                    Calendar = Field(e, "Calendar"),
                },
                "DayCounter" => new DayCounterRecord(name, @base!) { Calendar = Field(e, "Calendar") },
                "CurrencyPreset" => new CurrencyPresetRecord(name, @base)
                {
                    QuantLibName = Field(e, "QuantLibName"),
                    NumericCode = Int(Field(e, "NumericCode")),
                    Symbol = Field(e, "Symbol"),
                    FractionSymbol = Field(e, "FractionSymbol"),
                    FractionsPerUnit = Int(Field(e, "FractionsPerUnit")),
                    Rounding = e.Element(Ns + "Rounding") is { } rounding
                        ? new CurrencyRounding(
                            Enum.Parse<RoundingType>(Field(rounding, "Type")!),
                            Int(Field(rounding, "Precision")),
                            Int(Field(rounding, "Digit")))
                        : null,
                    TriangulationCurrency = Field(e, "TriangulationCurrency"),
                },
                "Alias" => new AliasRecord(name, @base!),
                _ => throw new ArgumentException($"unknown record kind {e.Name.LocalName}", nameof(e)),
            };
            return record with { Foreign = foreign };
        }
        catch (ArgumentException x)
        {
            var line = (IXmlLineInfo)e;
            throw new ArgumentException($"{e.Name.LocalName} {name} at {line.LineNumber}:{line.LinePosition}: {x.Message}", x);
        }
    }

    // A record's element, its fields in the schema's order.
    private static XElement Element(ConventionRecord record)
    {
        var e = new XElement(Ns + ConventionKinds.Element(record), new XAttribute("name", record.Name));
        if (record.Base is string b)
        {
            e.Add(new XAttribute("base", b));
        }

        Foreign(e, record.Foreign.Attributes);
        switch (record)
        {
            case CalendarPresetRecord r:
                Add(e, "QuantLibName", r.QuantLibName);
                Add(e, "AddedHoliday", r.AddedHolidays.Select(Text));
                Add(e, "RemovedHoliday", r.RemovedHolidays.Select(Text));
                break;
            case CalendarRecord r:
                Add(e, "AddedHoliday", r.AddedHolidays.Select(Text));
                Add(e, "RemovedHoliday", r.RemovedHolidays.Select(Text));
                break;
            case JointCalendarRecord r:
                Add(e, "Rule", r.Rule?.ToString());
                Add(e, "Member", r.Members);
                break;
            case BespokeCalendarRecord r:
                Add(e, "Weekend", r.Weekends.Select(WeekdayName));
                Add(e, "Holiday", r.Holidays.Select(Text));
                Add(e, "RemovedHoliday", r.RemovedHolidays.Select(Text));
                break;
            case DayCounterPresetRecord r:
                Add(e, "QuantLibName", r.QuantLibName);
                Add(e, "Calendar", r.Calendar);
                break;
            case DayCounterRecord r:
                Add(e, "Calendar", r.Calendar);
                break;
            case CurrencyPresetRecord r:
                Add(e, "QuantLibName", r.QuantLibName);
                Add(e, "NumericCode", r.NumericCode?.ToString(CultureInfo.InvariantCulture));
                Add(e, "Symbol", r.Symbol);
                Add(e, "FractionSymbol", r.FractionSymbol);
                Add(e, "FractionsPerUnit", r.FractionsPerUnit?.ToString(CultureInfo.InvariantCulture));
                if (r.Rounding is { } rounding)
                {
                    var re = new XElement(Ns + "Rounding");
                    Add(re, "Type", rounding.Type.ToString());
                    Add(re, "Precision", rounding.Precision?.ToString(CultureInfo.InvariantCulture));
                    Add(re, "Digit", rounding.Digit?.ToString(CultureInfo.InvariantCulture));
                    e.Add(re);
                }

                Add(e, "TriangulationCurrency", r.TriangulationCurrency);
                break;
            case AliasRecord:
                break;
            default:
                throw new ArgumentException($"unknown record kind {record.GetType().Name}", nameof(record));
        }

        foreach (string element in record.Foreign.Elements)
        {
            e.Add(XElement.Parse(element));
        }

        return e;
    }

    private static void Add(XElement e, string field, string? value)
    {
        if (value is not null)
        {
            e.Add(new XElement(Ns + field, value));
        }
    }

    private static void Add(XElement e, string field, IEnumerable<string> values)
    {
        foreach (string value in values)
        {
            e.Add(new XElement(Ns + field, value));
        }
    }

    private static void Foreign(XElement e, ImmutableArray<ForeignAttribute> attributes)
    {
        foreach (ForeignAttribute a in attributes)
        {
            XNamespace ns = a.Namespace;
            if (e.GetPrefixOfNamespace(ns) is null)
            {
                e.Add(new XAttribute(XNamespace.Xmlns + a.Prefix, a.Namespace));
            }

            e.Add(new XAttribute(ns + a.LocalName, a.Value));
        }
    }

    private static IEnumerable<ForeignAttribute> ForeignAttributes(XElement e) =>
        e.Attributes()
            .Where(a => !a.IsNamespaceDeclaration && a.Name.Namespace != XNamespace.None && a.Name.Namespace != Ns)
            .Select(a => new ForeignAttribute(
                a.Name.NamespaceName, a.Name.LocalName, e.GetPrefixOfNamespace(a.Name.Namespace) ?? "", a.Value));

    // A foreign element's text with the namespace declarations it needs, so it writes back alone.
    private static string Text(XElement e) => e.ToString(SaveOptions.DisableFormatting | SaveOptions.OmitDuplicateNamespaces);

    private static string? Field(XElement e, string field) => e.Element(Ns + field)?.Value;

    private static IEnumerable<string> Fields(XElement e, string field) => e.Elements(Ns + field).Select(c => c.Value);

    private static ImmutableArray<Date> Dates(XElement e, string field) => [.. Fields(e, field).Select(ParseDate)];

    private static int? Int(string? text) => text is null ? null : int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static Date ParseDate(string text)
    {
        var d = DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new Date(d.Day, (Month)d.Month, d.Year);
    }

    private static string Text(Date d) => FormattableString.Invariant($"{d.Year:D4}-{(int)d.Month:D2}-{d.DayOfMonth:D2}");

    // A weekday by its full name, the canonical one of its two.
    private static string WeekdayName(Weekday w) => w switch
    {
        Weekday.Sunday => "Sunday",
        Weekday.Monday => "Monday",
        Weekday.Tuesday => "Tuesday",
        Weekday.Wednesday => "Wednesday",
        Weekday.Thursday => "Thursday",
        Weekday.Friday => "Friday",
        Weekday.Saturday => "Saturday",
        _ => throw new ArgumentOutOfRangeException(nameof(w), w, "unknown weekday"),
    };
}
