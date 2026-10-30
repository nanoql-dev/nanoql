// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Time;
using NanoQL.Utilities;

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
        ("NanoQL.Extensions.Conventions.Presets.indexes.xml", "indexes.xml"),
        ("NanoQL.Extensions.Conventions.Presets.indexes.cxx.xml", "indexes.cxx.xml"),
        ("NanoQL.Extensions.Conventions.Presets.instruments.xml", "instruments.xml"),
        ("NanoQL.Extensions.Conventions.Presets.instruments.cxx.xml", "instruments.cxx.xml"),
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
                "Region" => new RegionRecord(name, @base) { RegionName = Field(e, "RegionName"), Code = Field(e, "Code") },
                "IborIndex" => new IborIndexRecord(name, @base)
                {
                    Family = Field(e, "Family"),
                    Tenor = PeriodOf(Field(e, "Tenor")),
                    FixingDays = Int(Field(e, "FixingDays")),
                    Currency = Field(e, "Currency"),
                    FixingCalendar = Field(e, "FixingCalendar"),
                    Convention = ConventionOf(Field(e, "Convention")),
                    EndOfMonth = Bool(Field(e, "EndOfMonth")),
                    DayCounter = Field(e, "DayCounter"),
                    Style = Field(e, "Style") is string style ? Enum.Parse<IborIndexStyle>(style) : null,
                    FinancialCenterCalendar = Field(e, "FinancialCenterCalendar"),
                    ValueCalendar = Field(e, "ValueCalendar"),
                    MaturityCalendar = Field(e, "MaturityCalendar"),
                },
                "IborFamily" => new IborFamilyRecord(name, @base)
                {
                    Family = Field(e, "Family"),
                    FixingDays = Int(Field(e, "FixingDays")),
                    FixingDaysAtOneDay = Int(Field(e, "FixingDaysAtOneDay")),
                    Currency = Field(e, "Currency"),
                    FixingCalendar = Field(e, "FixingCalendar"),
                    FinancialCenterCalendar = Field(e, "FinancialCenterCalendar"),
                    DayCounter = Field(e, "DayCounter"),
                    Units =
                    [
                        .. e.Elements(Ns + "Unit").Select(u => new IborFamilyUnit(
                            Enum.Parse<TimeUnit>(Field(u, "TimeUnit")!),
                            ConventionOf(Field(u, "Convention"))!.Value,
                            Bool(Field(u, "EndOfMonth"))!.Value)),
                    ],
                },
                "OvernightIndex" => new OvernightIndexRecord(name, @base)
                {
                    Family = Field(e, "Family"),
                    FixingDays = Int(Field(e, "FixingDays")),
                    Currency = Field(e, "Currency"),
                    FixingCalendar = Field(e, "FixingCalendar"),
                    DayCounter = Field(e, "DayCounter"),
                },
                "SwapIndex" => new SwapIndexRecord(name, @base)
                {
                    Family = Field(e, "Family"),
                    SettlementDays = Int(Field(e, "SettlementDays")),
                    Currency = Field(e, "Currency"),
                    FixingCalendar = Field(e, "FixingCalendar"),
                    FixedLegTenor = PeriodOf(Field(e, "FixedLegTenor")),
                    FixedLegConvention = ConventionOf(Field(e, "FixedLegConvention")),
                    FixedLegDayCounter = Field(e, "FixedLegDayCounter"),
                    IborIndex = Field(e, "IborIndex"),
                    IborIndexTenor = PeriodOf(Field(e, "IborIndexTenor")),
                    ShortIborIndex = Field(e, "ShortIborIndex"),
                    ShortIborIndexTenor = PeriodOf(Field(e, "ShortIborIndexTenor")),
                    ShortFixedLegTenor = PeriodOf(Field(e, "ShortFixedLegTenor")),
                },
                "OvernightIndexedSwapIndex" => new OvernightIndexedSwapIndexRecord(name, @base)
                {
                    Family = Field(e, "Family"),
                    SettlementDays = Int(Field(e, "SettlementDays")),
                    Currency = Field(e, "Currency"),
                    OvernightIndex = Field(e, "OvernightIndex"),
                    TelescopicValueDates = Bool(Field(e, "TelescopicValueDates")),
                    Averaging = Field(e, "Averaging") is string averaging ? Enum.Parse<RateAveragingType>(averaging) : null,
                },
                "ZeroInflationIndex" => new ZeroInflationIndexRecord(name, @base)
                {
                    Family = Field(e, "Family"),
                    Region = Field(e, "Region"),
                    Revised = Bool(Field(e, "Revised")),
                    Frequency = Field(e, "Frequency") is string frequency ? Enum.Parse<Frequency>(frequency) : null,
                    AvailabilityLag = PeriodOf(Field(e, "AvailabilityLag")),
                    Currency = Field(e, "Currency"),
                },
                "YoYInflationIndex" => new YoYInflationIndexRecord(name, @base)
                {
                    UnderlyingIndex = Field(e, "UnderlyingIndex"),
                    Family = Field(e, "Family"),
                    Region = Field(e, "Region"),
                    Revised = Bool(Field(e, "Revised")),
                    Frequency = Field(e, "Frequency") is string yoyFrequency ? Enum.Parse<Frequency>(yoyFrequency) : null,
                    AvailabilityLag = PeriodOf(Field(e, "AvailabilityLag")),
                    Currency = Field(e, "Currency"),
                },
                "ScheduleConvention" => new ScheduleConventionRecord(name, @base)
                {
                    Tenor = PeriodOf(Field(e, "Tenor")),
                    Frequency = EnumOf<Frequency>(Field(e, "Frequency")),
                    Calendar = Field(e, "Calendar"),
                    Convention = ConventionOf(Field(e, "Convention")),
                    TerminationDateConvention = ConventionOf(Field(e, "TerminationDateConvention")),
                    Rule = EnumOf<DateGenerationRule>(Field(e, "Rule")),
                    EndOfMonth = Bool(Field(e, "EndOfMonth")),
                },
                "SwapConvention" => new SwapConventionRecord(name, @base)
                {
                    SettlementDays = Int(Field(e, "SettlementDays")),
                    SettlementCalendar = Field(e, "SettlementCalendar"),
                    FixedLegTenor = PeriodOf(Field(e, "FixedLegTenor")),
                    FixedLegTenorThreshold = ThresholdOf(e.Element(Ns + "FixedLegTenorThreshold")),
                    FixedLegCalendar = Field(e, "FixedLegCalendar"),
                    FixedLegConvention = ConventionOf(Field(e, "FixedLegConvention")),
                    FixedLegTerminationDateConvention = ConventionOf(Field(e, "FixedLegTerminationDateConvention")),
                    FixedLegRule = EnumOf<DateGenerationRule>(Field(e, "FixedLegRule")),
                    FixedLegEndOfMonth = Bool(Field(e, "FixedLegEndOfMonth")),
                    FixedLegDayCounter = Field(e, "FixedLegDayCounter"),
                    Index = Field(e, "Index"),
                    IndexTenor = PeriodOf(Field(e, "IndexTenor")),
                    FloatingLegTenor = PeriodOf(Field(e, "FloatingLegTenor")),
                    FloatingLegCalendar = Field(e, "FloatingLegCalendar"),
                    FloatingLegConvention = ConventionOf(Field(e, "FloatingLegConvention")),
                    FloatingLegTerminationDateConvention = ConventionOf(Field(e, "FloatingLegTerminationDateConvention")),
                    FloatingLegRule = EnumOf<DateGenerationRule>(Field(e, "FloatingLegRule")),
                    FloatingLegEndOfMonth = Bool(Field(e, "FloatingLegEndOfMonth")),
                    FloatingLegDayCounter = Field(e, "FloatingLegDayCounter"),
                    PaymentConvention = ConventionOf(Field(e, "PaymentConvention")),
                },
                "OisConvention" => new OisConventionRecord(name, @base)
                {
                    Index = Field(e, "Index"),
                    SettlementDays = Int(Field(e, "SettlementDays")),
                    SettlementCalendar = Field(e, "SettlementCalendar"),
                    PaymentLag = Int(Field(e, "PaymentLag")),
                    PaymentConvention = ConventionOf(Field(e, "PaymentConvention")),
                    PaymentFrequency = EnumOf<Frequency>(Field(e, "PaymentFrequency")),
                    PaymentCalendar = Field(e, "PaymentCalendar"),
                    FixedLegFrequency = EnumOf<Frequency>(Field(e, "FixedLegFrequency")),
                    FixedLegCalendar = Field(e, "FixedLegCalendar"),
                    FixedLegConvention = ConventionOf(Field(e, "FixedLegConvention")),
                    FixedLegTerminationDateConvention = ConventionOf(Field(e, "FixedLegTerminationDateConvention")),
                    FixedLegRule = EnumOf<DateGenerationRule>(Field(e, "FixedLegRule")),
                    FixedLegEndOfMonth = Bool(Field(e, "FixedLegEndOfMonth")),
                    FixedLegDayCounter = Field(e, "FixedLegDayCounter"),
                    OvernightLegCalendar = Field(e, "OvernightLegCalendar"),
                    OvernightLegConvention = ConventionOf(Field(e, "OvernightLegConvention")),
                    OvernightLegTerminationDateConvention = ConventionOf(Field(e, "OvernightLegTerminationDateConvention")),
                    OvernightLegRule = EnumOf<DateGenerationRule>(Field(e, "OvernightLegRule")),
                    OvernightLegEndOfMonth = Bool(Field(e, "OvernightLegEndOfMonth")),
                    TelescopicValueDates = Bool(Field(e, "TelescopicValueDates")),
                    Averaging = EnumOf<RateAveragingType>(Field(e, "Averaging")),
                    LookbackDays = Int(Field(e, "LookbackDays")),
                    LockoutDays = Int(Field(e, "LockoutDays")),
                    ObservationShift = Bool(Field(e, "ObservationShift")),
                },
                "CdsConvention" => new CdsConventionRecord(name, @base)
                {
                    SettlementDays = Int(Field(e, "SettlementDays")),
                    Calendar = Field(e, "Calendar"),
                    CouponFrequency = EnumOf<Frequency>(Field(e, "CouponFrequency")),
                    PaymentConvention = ConventionOf(Field(e, "PaymentConvention")),
                    Rule = EnumOf<DateGenerationRule>(Field(e, "Rule")),
                    DayCounter = Field(e, "DayCounter"),
                    LastPeriodDayCounter = Field(e, "LastPeriodDayCounter"),
                    SettlesAccrual = Bool(Field(e, "SettlesAccrual")),
                    PaysAtDefaultTime = Bool(Field(e, "PaysAtDefaultTime")),
                    RebatesAccrual = Bool(Field(e, "RebatesAccrual")),
                    CashSettlementDays = Int(Field(e, "CashSettlementDays")),
                    PricingModel = EnumOf<CreditDefaultSwapPricingModel>(Field(e, "PricingModel")),
                },
                "ZeroCouponInflationSwapConvention" => new ZeroCouponInflationSwapConventionRecord(name, @base)
                {
                    Index = Field(e, "Index"),
                    ObservationLag = PeriodOf(Field(e, "ObservationLag")),
                    Interpolation = EnumOf<CPIInterpolationType>(Field(e, "Interpolation")),
                    Calendar = Field(e, "Calendar"),
                    Convention = ConventionOf(Field(e, "Convention")),
                    DayCounter = Field(e, "DayCounter"),
                    InflationCalendar = Field(e, "InflationCalendar"),
                    InflationConvention = ConventionOf(Field(e, "InflationConvention")),
                    AdjustObservationDates = Bool(Field(e, "AdjustObservationDates")),
                },
                "YearOnYearInflationSwapConvention" => new YearOnYearInflationSwapConventionRecord(name, @base)
                {
                    Index = Field(e, "Index"),
                    ObservationLag = PeriodOf(Field(e, "ObservationLag")),
                    Interpolation = EnumOf<CPIInterpolationType>(Field(e, "Interpolation")),
                    Calendar = Field(e, "Calendar"),
                    PaymentConvention = ConventionOf(Field(e, "PaymentConvention")),
                    DayCounter = Field(e, "DayCounter"),
                },
                "CrossCurrencyBasisSwapConvention" => new CrossCurrencyBasisSwapConventionRecord(name, @base)
                {
                    FixingDays = Int(Field(e, "FixingDays")),
                    Calendar = Field(e, "Calendar"),
                    Convention = ConventionOf(Field(e, "Convention")),
                    EndOfMonth = Bool(Field(e, "EndOfMonth")),
                    BaseCurrencyIndex = Field(e, "BaseCurrencyIndex"),
                    BaseCurrencyIndexTenor = PeriodOf(Field(e, "BaseCurrencyIndexTenor")),
                    QuoteCurrencyIndex = Field(e, "QuoteCurrencyIndex"),
                    QuoteCurrencyIndexTenor = PeriodOf(Field(e, "QuoteCurrencyIndexTenor")),
                    CollateralOnBaseCurrency = Bool(Field(e, "CollateralOnBaseCurrency")),
                    BasisOnBaseCurrencyLeg = Bool(Field(e, "BasisOnBaseCurrencyLeg")),
                    PaymentFrequency = EnumOf<Frequency>(Field(e, "PaymentFrequency")),
                    QuoteCurrencyPaymentFrequency = EnumOf<Frequency>(Field(e, "QuoteCurrencyPaymentFrequency")),
                    PaymentLag = Int(Field(e, "PaymentLag")),
                    IndexedCoupons = Bool(Field(e, "IndexedCoupons")),
                    PaymentLagOnNotionalExchanges = Bool(Field(e, "PaymentLagOnNotionalExchanges")),
                    MarkToMarket = Bool(Field(e, "MarkToMarket")),
                    BaseCurrencyLegResettable = Bool(Field(e, "BaseCurrencyLegResettable")),
                    FxResetFixingDays = Int(Field(e, "FxResetFixingDays")),
                    FxResetFixingCalendar = Field(e, "FxResetFixingCalendar"),
                },
                "CrossCurrencySwapConvention" => new CrossCurrencySwapConventionRecord(name, @base)
                {
                    FixingDays = Int(Field(e, "FixingDays")),
                    Calendar = Field(e, "Calendar"),
                    Convention = ConventionOf(Field(e, "Convention")),
                    EndOfMonth = Bool(Field(e, "EndOfMonth")),
                    FixedFrequency = EnumOf<Frequency>(Field(e, "FixedFrequency")),
                    FixedDayCounter = Field(e, "FixedDayCounter"),
                    FloatingIndex = Field(e, "FloatingIndex"),
                    FloatingIndexTenor = PeriodOf(Field(e, "FloatingIndexTenor")),
                    CollateralOnFixedLeg = Bool(Field(e, "CollateralOnFixedLeg")),
                    PaymentLag = Int(Field(e, "PaymentLag")),
                    FloatingPaymentFrequency = EnumOf<Frequency>(Field(e, "FloatingPaymentFrequency")),
                    IndexedCoupons = Bool(Field(e, "IndexedCoupons")),
                },
                "FxSwapConvention" => new FxSwapConventionRecord(name, @base)
                {
                    BaseCurrency = Field(e, "BaseCurrency"),
                    QuoteCurrency = Field(e, "QuoteCurrency"),
                    FixingDays = Int(Field(e, "FixingDays")),
                    Calendar = Field(e, "Calendar"),
                    Convention = ConventionOf(Field(e, "Convention")),
                    EndOfMonth = Bool(Field(e, "EndOfMonth")),
                    CollateralOnBaseCurrency = Bool(Field(e, "CollateralOnBaseCurrency")),
                    TradingCalendar = Field(e, "TradingCalendar"),
                    PaymentCalendar = Field(e, "PaymentCalendar"),
                },
                "CapFloorConvention" => new CapFloorConventionRecord(name, @base)
                {
                    Index = Field(e, "Index"),
                    IndexTenor = PeriodOf(Field(e, "IndexTenor")),
                    Tenor = PeriodOf(Field(e, "Tenor")),
                    Calendar = Field(e, "Calendar"),
                    Convention = ConventionOf(Field(e, "Convention")),
                    TerminationDateConvention = ConventionOf(Field(e, "TerminationDateConvention")),
                    Rule = EnumOf<DateGenerationRule>(Field(e, "Rule")),
                    EndOfMonth = Bool(Field(e, "EndOfMonth")),
                    DayCounter = Field(e, "DayCounter"),
                    FirstCapletExcluded = Bool(Field(e, "FirstCapletExcluded")),
                },
                "SwaptionConvention" => new SwaptionConventionRecord(name, @base)
                {
                    SwapIndex = Field(e, "SwapIndex"),
                    SettlementType = EnumOf<SettlementType>(Field(e, "SettlementType")),
                    SettlementMethod = EnumOf<SettlementMethod>(Field(e, "SettlementMethod")),
                    OptionConvention = ConventionOf(Field(e, "OptionConvention")),
                    ExerciseCalendar = Field(e, "ExerciseCalendar"),
                },
                "CmsConvention" => new CmsConventionRecord(name, @base)
                {
                    SwapIndex = Field(e, "SwapIndex"),
                    SwapIndexTenor = PeriodOf(Field(e, "SwapIndexTenor")),
                    CmsLegTenor = PeriodOf(Field(e, "CmsLegTenor")),
                    CmsLegCalendar = Field(e, "CmsLegCalendar"),
                    CmsLegConvention = ConventionOf(Field(e, "CmsLegConvention")),
                    CmsLegTerminationDateConvention = ConventionOf(Field(e, "CmsLegTerminationDateConvention")),
                    CmsLegRule = EnumOf<DateGenerationRule>(Field(e, "CmsLegRule")),
                    CmsLegEndOfMonth = Bool(Field(e, "CmsLegEndOfMonth")),
                    CmsLegDayCounter = Field(e, "CmsLegDayCounter"),
                    FloatingLegIndex = Field(e, "FloatingLegIndex"),
                    FloatingLegIndexTenor = PeriodOf(Field(e, "FloatingLegIndexTenor")),
                    FloatingLegTenor = PeriodOf(Field(e, "FloatingLegTenor")),
                    FloatingLegCalendar = Field(e, "FloatingLegCalendar"),
                    FloatingLegConvention = ConventionOf(Field(e, "FloatingLegConvention")),
                    FloatingLegTerminationDateConvention = ConventionOf(Field(e, "FloatingLegTerminationDateConvention")),
                    FloatingLegRule = EnumOf<DateGenerationRule>(Field(e, "FloatingLegRule")),
                    FloatingLegEndOfMonth = Bool(Field(e, "FloatingLegEndOfMonth")),
                    FloatingLegDayCounter = Field(e, "FloatingLegDayCounter"),
                },
                "MultipleResetsSwapConvention" => new MultipleResetsSwapConventionRecord(name, @base)
                {
                    Index = Field(e, "Index"),
                    IndexTenor = PeriodOf(Field(e, "IndexTenor")),
                    ResetsPerCoupon = Int(Field(e, "ResetsPerCoupon")),
                    SettlementDays = Int(Field(e, "SettlementDays")),
                    FixedLegFrequency = EnumOf<Frequency>(Field(e, "FixedLegFrequency")),
                    FixedLegDayCounter = Field(e, "FixedLegDayCounter"),
                    FixedLegConvention = ConventionOf(Field(e, "FixedLegConvention")),
                    Averaging = EnumOf<RateAveragingType>(Field(e, "Averaging")),
                },
                "YoYInflationCapFloorConvention" => new YoYInflationCapFloorConventionRecord(name, @base)
                {
                    Index = Field(e, "Index"),
                    ObservationLag = PeriodOf(Field(e, "ObservationLag")),
                    Interpolation = EnumOf<CPIInterpolationType>(Field(e, "Interpolation")),
                    Calendar = Field(e, "Calendar"),
                    PaymentDayCounter = Field(e, "PaymentDayCounter"),
                    PaymentAdjustment = ConventionOf(Field(e, "PaymentAdjustment")),
                    FixingDays = Int(Field(e, "FixingDays")),
                },
                "BondConvention" => new BondConventionRecord(name, @base)
                {
                    SettlementDays = Int(Field(e, "SettlementDays")),
                    Frequency = EnumOf<Frequency>(Field(e, "Frequency")),
                    Calendar = Field(e, "Calendar"),
                    Convention = ConventionOf(Field(e, "Convention")),
                    TerminationDateConvention = ConventionOf(Field(e, "TerminationDateConvention")),
                    Rule = EnumOf<DateGenerationRule>(Field(e, "Rule")),
                    EndOfMonth = Bool(Field(e, "EndOfMonth")),
                    AccrualDayCounter = Field(e, "AccrualDayCounter"),
                    PaymentConvention = ConventionOf(Field(e, "PaymentConvention")),
                    PaymentCalendar = Field(e, "PaymentCalendar"),
                    Redemption = Double(Field(e, "Redemption")),
                    ExCouponPeriod = PeriodOf(Field(e, "ExCouponPeriod")),
                    ExCouponCalendar = Field(e, "ExCouponCalendar"),
                    ExCouponConvention = ConventionOf(Field(e, "ExCouponConvention")),
                    ExCouponEndOfMonth = Bool(Field(e, "ExCouponEndOfMonth")),
                    PriceType = EnumOf<BondPriceType>(Field(e, "PriceType")),
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
            case RegionRecord r:
                Add(e, "RegionName", r.RegionName);
                Add(e, "Code", r.Code);
                break;
            case IborIndexRecord r:
                Add(e, "Family", r.Family);
                Add(e, "Tenor", r.Tenor?.ToString());
                Add(e, "FixingDays", Text(r.FixingDays));
                Add(e, "Currency", r.Currency);
                Add(e, "FixingCalendar", r.FixingCalendar);
                Add(e, "Convention", r.Convention?.ToString());
                Add(e, "EndOfMonth", Text(r.EndOfMonth));
                Add(e, "DayCounter", r.DayCounter);
                Add(e, "Style", r.Style?.ToString());
                Add(e, "FinancialCenterCalendar", r.FinancialCenterCalendar);
                Add(e, "ValueCalendar", r.ValueCalendar);
                Add(e, "MaturityCalendar", r.MaturityCalendar);
                break;
            case IborFamilyRecord r:
                Add(e, "Family", r.Family);
                Add(e, "FixingDays", Text(r.FixingDays));
                Add(e, "FixingDaysAtOneDay", Text(r.FixingDaysAtOneDay));
                Add(e, "Currency", r.Currency);
                Add(e, "FixingCalendar", r.FixingCalendar);
                Add(e, "FinancialCenterCalendar", r.FinancialCenterCalendar);
                Add(e, "DayCounter", r.DayCounter);
                foreach (IborFamilyUnit u in r.Units)
                {
                    var ue = new XElement(Ns + "Unit");
                    Add(ue, "TimeUnit", u.TimeUnit.ToString());
                    Add(ue, "Convention", u.Convention.ToString());
                    Add(ue, "EndOfMonth", Text(u.EndOfMonth));
                    e.Add(ue);
                }

                break;
            case OvernightIndexRecord r:
                Add(e, "Family", r.Family);
                Add(e, "FixingDays", Text(r.FixingDays));
                Add(e, "Currency", r.Currency);
                Add(e, "FixingCalendar", r.FixingCalendar);
                Add(e, "DayCounter", r.DayCounter);
                break;
            case SwapIndexRecord r:
                Add(e, "Family", r.Family);
                Add(e, "SettlementDays", Text(r.SettlementDays));
                Add(e, "Currency", r.Currency);
                Add(e, "FixingCalendar", r.FixingCalendar);
                Add(e, "FixedLegTenor", r.FixedLegTenor?.ToString());
                Add(e, "FixedLegConvention", r.FixedLegConvention?.ToString());
                Add(e, "FixedLegDayCounter", r.FixedLegDayCounter);
                Add(e, "IborIndex", r.IborIndex);
                Add(e, "IborIndexTenor", r.IborIndexTenor?.ToString());
                Add(e, "ShortIborIndex", r.ShortIborIndex);
                Add(e, "ShortIborIndexTenor", r.ShortIborIndexTenor?.ToString());
                Add(e, "ShortFixedLegTenor", r.ShortFixedLegTenor?.ToString());
                break;
            case OvernightIndexedSwapIndexRecord r:
                Add(e, "Family", r.Family);
                Add(e, "SettlementDays", Text(r.SettlementDays));
                Add(e, "Currency", r.Currency);
                Add(e, "OvernightIndex", r.OvernightIndex);
                Add(e, "TelescopicValueDates", Text(r.TelescopicValueDates));
                Add(e, "Averaging", r.Averaging?.ToString());
                break;
            case ZeroInflationIndexRecord r:
                Add(e, "Family", r.Family);
                Add(e, "Region", r.Region);
                Add(e, "Revised", Text(r.Revised));
                Add(e, "Frequency", r.Frequency?.ToString());
                Add(e, "AvailabilityLag", r.AvailabilityLag?.ToString());
                Add(e, "Currency", r.Currency);
                break;
            case YoYInflationIndexRecord r:
                Add(e, "UnderlyingIndex", r.UnderlyingIndex);
                Add(e, "Family", r.Family);
                Add(e, "Region", r.Region);
                Add(e, "Revised", Text(r.Revised));
                Add(e, "Frequency", r.Frequency?.ToString());
                Add(e, "AvailabilityLag", r.AvailabilityLag?.ToString());
                Add(e, "Currency", r.Currency);
                break;
            case ScheduleConventionRecord r:
                Add(e, "Tenor", r.Tenor?.ToString());
                Add(e, "Frequency", r.Frequency?.ToString());
                Add(e, "Calendar", r.Calendar);
                Add(e, "Convention", r.Convention?.ToString());
                Add(e, "TerminationDateConvention", r.TerminationDateConvention?.ToString());
                Add(e, "Rule", r.Rule?.ToString());
                Add(e, "EndOfMonth", Text(r.EndOfMonth));
                break;
            case SwapConventionRecord r:
                Add(e, "SettlementDays", Text(r.SettlementDays));
                Add(e, "SettlementCalendar", r.SettlementCalendar);
                Add(e, "FixedLegTenor", r.FixedLegTenor?.ToString());
                Add(e, "FixedLegTenorThreshold", r.FixedLegTenorThreshold);
                Add(e, "FixedLegCalendar", r.FixedLegCalendar);
                Add(e, "FixedLegConvention", r.FixedLegConvention?.ToString());
                Add(e, "FixedLegTerminationDateConvention", r.FixedLegTerminationDateConvention?.ToString());
                Add(e, "FixedLegRule", r.FixedLegRule?.ToString());
                Add(e, "FixedLegEndOfMonth", Text(r.FixedLegEndOfMonth));
                Add(e, "FixedLegDayCounter", r.FixedLegDayCounter);
                Add(e, "Index", r.Index);
                Add(e, "IndexTenor", r.IndexTenor?.ToString());
                Add(e, "FloatingLegTenor", r.FloatingLegTenor?.ToString());
                Add(e, "FloatingLegCalendar", r.FloatingLegCalendar);
                Add(e, "FloatingLegConvention", r.FloatingLegConvention?.ToString());
                Add(e, "FloatingLegTerminationDateConvention", r.FloatingLegTerminationDateConvention?.ToString());
                Add(e, "FloatingLegRule", r.FloatingLegRule?.ToString());
                Add(e, "FloatingLegEndOfMonth", Text(r.FloatingLegEndOfMonth));
                Add(e, "FloatingLegDayCounter", r.FloatingLegDayCounter);
                Add(e, "PaymentConvention", r.PaymentConvention?.ToString());
                break;
            case OisConventionRecord r:
                Add(e, "Index", r.Index);
                Add(e, "SettlementDays", Text(r.SettlementDays));
                Add(e, "SettlementCalendar", r.SettlementCalendar);
                Add(e, "PaymentLag", Text(r.PaymentLag));
                Add(e, "PaymentConvention", r.PaymentConvention?.ToString());
                Add(e, "PaymentFrequency", r.PaymentFrequency?.ToString());
                Add(e, "PaymentCalendar", r.PaymentCalendar);
                Add(e, "FixedLegFrequency", r.FixedLegFrequency?.ToString());
                Add(e, "FixedLegCalendar", r.FixedLegCalendar);
                Add(e, "FixedLegConvention", r.FixedLegConvention?.ToString());
                Add(e, "FixedLegTerminationDateConvention", r.FixedLegTerminationDateConvention?.ToString());
                Add(e, "FixedLegRule", r.FixedLegRule?.ToString());
                Add(e, "FixedLegEndOfMonth", Text(r.FixedLegEndOfMonth));
                Add(e, "FixedLegDayCounter", r.FixedLegDayCounter);
                Add(e, "OvernightLegCalendar", r.OvernightLegCalendar);
                Add(e, "OvernightLegConvention", r.OvernightLegConvention?.ToString());
                Add(e, "OvernightLegTerminationDateConvention", r.OvernightLegTerminationDateConvention?.ToString());
                Add(e, "OvernightLegRule", r.OvernightLegRule?.ToString());
                Add(e, "OvernightLegEndOfMonth", Text(r.OvernightLegEndOfMonth));
                Add(e, "TelescopicValueDates", Text(r.TelescopicValueDates));
                Add(e, "Averaging", r.Averaging?.ToString());
                Add(e, "LookbackDays", Text(r.LookbackDays));
                Add(e, "LockoutDays", Text(r.LockoutDays));
                Add(e, "ObservationShift", Text(r.ObservationShift));
                break;
            case CdsConventionRecord r:
                Add(e, "SettlementDays", Text(r.SettlementDays));
                Add(e, "Calendar", r.Calendar);
                Add(e, "CouponFrequency", r.CouponFrequency?.ToString());
                Add(e, "PaymentConvention", r.PaymentConvention?.ToString());
                Add(e, "Rule", r.Rule?.ToString());
                Add(e, "DayCounter", r.DayCounter);
                Add(e, "LastPeriodDayCounter", r.LastPeriodDayCounter);
                Add(e, "SettlesAccrual", Text(r.SettlesAccrual));
                Add(e, "PaysAtDefaultTime", Text(r.PaysAtDefaultTime));
                Add(e, "RebatesAccrual", Text(r.RebatesAccrual));
                Add(e, "CashSettlementDays", Text(r.CashSettlementDays));
                Add(e, "PricingModel", r.PricingModel?.ToString());
                break;
            case ZeroCouponInflationSwapConventionRecord r:
                Add(e, "Index", r.Index);
                Add(e, "ObservationLag", r.ObservationLag?.ToString());
                Add(e, "Interpolation", r.Interpolation?.ToString());
                Add(e, "Calendar", r.Calendar);
                Add(e, "Convention", r.Convention?.ToString());
                Add(e, "DayCounter", r.DayCounter);
                Add(e, "InflationCalendar", r.InflationCalendar);
                Add(e, "InflationConvention", r.InflationConvention?.ToString());
                Add(e, "AdjustObservationDates", Text(r.AdjustObservationDates));
                break;
            case YearOnYearInflationSwapConventionRecord r:
                Add(e, "Index", r.Index);
                Add(e, "ObservationLag", r.ObservationLag?.ToString());
                Add(e, "Interpolation", r.Interpolation?.ToString());
                Add(e, "Calendar", r.Calendar);
                Add(e, "PaymentConvention", r.PaymentConvention?.ToString());
                Add(e, "DayCounter", r.DayCounter);
                break;
            case CrossCurrencyBasisSwapConventionRecord r:
                Add(e, "FixingDays", Text(r.FixingDays));
                Add(e, "Calendar", r.Calendar);
                Add(e, "Convention", r.Convention?.ToString());
                Add(e, "EndOfMonth", Text(r.EndOfMonth));
                Add(e, "BaseCurrencyIndex", r.BaseCurrencyIndex);
                Add(e, "BaseCurrencyIndexTenor", r.BaseCurrencyIndexTenor?.ToString());
                Add(e, "QuoteCurrencyIndex", r.QuoteCurrencyIndex);
                Add(e, "QuoteCurrencyIndexTenor", r.QuoteCurrencyIndexTenor?.ToString());
                Add(e, "CollateralOnBaseCurrency", Text(r.CollateralOnBaseCurrency));
                Add(e, "BasisOnBaseCurrencyLeg", Text(r.BasisOnBaseCurrencyLeg));
                Add(e, "PaymentFrequency", r.PaymentFrequency?.ToString());
                Add(e, "QuoteCurrencyPaymentFrequency", r.QuoteCurrencyPaymentFrequency?.ToString());
                Add(e, "PaymentLag", Text(r.PaymentLag));
                Add(e, "IndexedCoupons", Text(r.IndexedCoupons));
                Add(e, "PaymentLagOnNotionalExchanges", Text(r.PaymentLagOnNotionalExchanges));
                Add(e, "MarkToMarket", Text(r.MarkToMarket));
                Add(e, "BaseCurrencyLegResettable", Text(r.BaseCurrencyLegResettable));
                Add(e, "FxResetFixingDays", Text(r.FxResetFixingDays));
                Add(e, "FxResetFixingCalendar", r.FxResetFixingCalendar);
                break;
            case CrossCurrencySwapConventionRecord r:
                Add(e, "FixingDays", Text(r.FixingDays));
                Add(e, "Calendar", r.Calendar);
                Add(e, "Convention", r.Convention?.ToString());
                Add(e, "EndOfMonth", Text(r.EndOfMonth));
                Add(e, "FixedFrequency", r.FixedFrequency?.ToString());
                Add(e, "FixedDayCounter", r.FixedDayCounter);
                Add(e, "FloatingIndex", r.FloatingIndex);
                Add(e, "FloatingIndexTenor", r.FloatingIndexTenor?.ToString());
                Add(e, "CollateralOnFixedLeg", Text(r.CollateralOnFixedLeg));
                Add(e, "PaymentLag", Text(r.PaymentLag));
                Add(e, "FloatingPaymentFrequency", r.FloatingPaymentFrequency?.ToString());
                Add(e, "IndexedCoupons", Text(r.IndexedCoupons));
                break;
            case FxSwapConventionRecord r:
                Add(e, "BaseCurrency", r.BaseCurrency);
                Add(e, "QuoteCurrency", r.QuoteCurrency);
                Add(e, "FixingDays", Text(r.FixingDays));
                Add(e, "Calendar", r.Calendar);
                Add(e, "Convention", r.Convention?.ToString());
                Add(e, "EndOfMonth", Text(r.EndOfMonth));
                Add(e, "CollateralOnBaseCurrency", Text(r.CollateralOnBaseCurrency));
                Add(e, "TradingCalendar", r.TradingCalendar);
                Add(e, "PaymentCalendar", r.PaymentCalendar);
                break;
            case CapFloorConventionRecord r:
                Add(e, "Index", r.Index);
                Add(e, "IndexTenor", r.IndexTenor?.ToString());
                Add(e, "Tenor", r.Tenor?.ToString());
                Add(e, "Calendar", r.Calendar);
                Add(e, "Convention", r.Convention?.ToString());
                Add(e, "TerminationDateConvention", r.TerminationDateConvention?.ToString());
                Add(e, "Rule", r.Rule?.ToString());
                Add(e, "EndOfMonth", Text(r.EndOfMonth));
                Add(e, "DayCounter", r.DayCounter);
                Add(e, "FirstCapletExcluded", Text(r.FirstCapletExcluded));
                break;
            case SwaptionConventionRecord r:
                Add(e, "SwapIndex", r.SwapIndex);
                Add(e, "SettlementType", r.SettlementType?.ToString());
                Add(e, "SettlementMethod", r.SettlementMethod?.ToString());
                Add(e, "OptionConvention", r.OptionConvention?.ToString());
                Add(e, "ExerciseCalendar", r.ExerciseCalendar);
                break;
            case CmsConventionRecord r:
                Add(e, "SwapIndex", r.SwapIndex);
                Add(e, "SwapIndexTenor", r.SwapIndexTenor?.ToString());
                Add(e, "CmsLegTenor", r.CmsLegTenor?.ToString());
                Add(e, "CmsLegCalendar", r.CmsLegCalendar);
                Add(e, "CmsLegConvention", r.CmsLegConvention?.ToString());
                Add(e, "CmsLegTerminationDateConvention", r.CmsLegTerminationDateConvention?.ToString());
                Add(e, "CmsLegRule", r.CmsLegRule?.ToString());
                Add(e, "CmsLegEndOfMonth", Text(r.CmsLegEndOfMonth));
                Add(e, "CmsLegDayCounter", r.CmsLegDayCounter);
                Add(e, "FloatingLegIndex", r.FloatingLegIndex);
                Add(e, "FloatingLegIndexTenor", r.FloatingLegIndexTenor?.ToString());
                Add(e, "FloatingLegTenor", r.FloatingLegTenor?.ToString());
                Add(e, "FloatingLegCalendar", r.FloatingLegCalendar);
                Add(e, "FloatingLegConvention", r.FloatingLegConvention?.ToString());
                Add(e, "FloatingLegTerminationDateConvention", r.FloatingLegTerminationDateConvention?.ToString());
                Add(e, "FloatingLegRule", r.FloatingLegRule?.ToString());
                Add(e, "FloatingLegEndOfMonth", Text(r.FloatingLegEndOfMonth));
                Add(e, "FloatingLegDayCounter", r.FloatingLegDayCounter);
                break;
            case MultipleResetsSwapConventionRecord r:
                Add(e, "Index", r.Index);
                Add(e, "IndexTenor", r.IndexTenor?.ToString());
                Add(e, "ResetsPerCoupon", Text(r.ResetsPerCoupon));
                Add(e, "SettlementDays", Text(r.SettlementDays));
                Add(e, "FixedLegFrequency", r.FixedLegFrequency?.ToString());
                Add(e, "FixedLegDayCounter", r.FixedLegDayCounter);
                Add(e, "FixedLegConvention", r.FixedLegConvention?.ToString());
                Add(e, "Averaging", r.Averaging?.ToString());
                break;
            case YoYInflationCapFloorConventionRecord r:
                Add(e, "Index", r.Index);
                Add(e, "ObservationLag", r.ObservationLag?.ToString());
                Add(e, "Interpolation", r.Interpolation?.ToString());
                Add(e, "Calendar", r.Calendar);
                Add(e, "PaymentDayCounter", r.PaymentDayCounter);
                Add(e, "PaymentAdjustment", r.PaymentAdjustment?.ToString());
                Add(e, "FixingDays", Text(r.FixingDays));
                break;
            case BondConventionRecord r:
                Add(e, "SettlementDays", Text(r.SettlementDays));
                Add(e, "Frequency", r.Frequency?.ToString());
                Add(e, "Calendar", r.Calendar);
                Add(e, "Convention", r.Convention?.ToString());
                Add(e, "TerminationDateConvention", r.TerminationDateConvention?.ToString());
                Add(e, "Rule", r.Rule?.ToString());
                Add(e, "EndOfMonth", Text(r.EndOfMonth));
                Add(e, "AccrualDayCounter", r.AccrualDayCounter);
                Add(e, "PaymentConvention", r.PaymentConvention?.ToString());
                Add(e, "PaymentCalendar", r.PaymentCalendar);
                Add(e, "Redemption", Text(r.Redemption));
                Add(e, "ExCouponPeriod", r.ExCouponPeriod?.ToString());
                Add(e, "ExCouponCalendar", r.ExCouponCalendar);
                Add(e, "ExCouponConvention", r.ExCouponConvention?.ToString());
                Add(e, "ExCouponEndOfMonth", Text(r.ExCouponEndOfMonth));
                Add(e, "PriceType", r.PriceType?.ToString());
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

    private static void Add(XElement e, string field, SwapTenorThreshold? threshold)
    {
        if (threshold is not null)
        {
            var t = new XElement(Ns + field);
            Add(t, "Comparison", threshold.Comparison.ToString());
            Add(t, "SwapTenor", threshold.SwapTenor.ToString());
            Add(t, "FixedLegTenor", threshold.FixedLegTenor.ToString());
            e.Add(t);
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

    private static bool? Bool(string? text) => text is null ? null : XmlConvert.ToBoolean(text);

    private static Period? PeriodOf(string? text) => text is null ? null : PeriodParser.Parse(text);

    private static double? Double(string? text) => text is null ? null : XmlConvert.ToDouble(text);

    private static T? EnumOf<T>(string? text)
        where T : struct, Enum =>
        text is null ? null : Enum.Parse<T>(text);

    private static SwapTenorThreshold? ThresholdOf(XElement? t) =>
        t is null
            ? null
            : new SwapTenorThreshold(
                Enum.Parse<SwapTenorComparison>(Field(t, "Comparison")!),
                PeriodOf(Field(t, "SwapTenor"))!.Value,
                PeriodOf(Field(t, "FixedLegTenor"))!.Value);

    // a business-day convention by its member's name or by a shipped FpML code that maps to one exactly
    private static BusinessDayConvention? ConventionOf(string? text) =>
        text is null ? null : PresetFactories.BusinessDayConventionAliases.TryGetValue(text, out BusinessDayConvention c) ? c : Enum.Parse<BusinessDayConvention>(text);

    private static string? Text(int? value) => value?.ToString(CultureInfo.InvariantCulture);

    private static string? Text(bool? value) => value is bool b ? (b ? "true" : "false") : null;

    private static string? Text(double? value) => value is double d ? XmlConvert.ToString(d) : null;

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
