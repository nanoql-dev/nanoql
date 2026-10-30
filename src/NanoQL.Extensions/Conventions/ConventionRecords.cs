// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/ConventionRecordsTests.cs (nanoql's own description of built objects as records)

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// Built objects back to records: <see cref="Describe(string, Calendar, ConventionSet?)"/> and its overloads give the
/// records that resolve, in a set, to an object equal to the one given, an index field by field.
/// </summary>
/// <remarks>
/// A value equal to a preset's resolved object (a calendar by type, name and edits, a day counter by its own
/// equality) is an alias of the preset. Any other value's fields are written out, its parts that equal no preset under
/// generated names, <c>name/1</c>, <c>name/2</c>, which a user's name takes only beside its <c>name</c> in one snapshot,
/// as this output does. A day counter that carries a trade's data (Actual/Actual (ISMA) with a schedule, 30/360 (ISDA)
/// with a termination date) is no convention and is refused.
/// A currency is one of QuantLib's by its code, its name and numeric code QuantLib's, since the three are its identity:
/// one that differs from the set's in its other fields is described as an override of its code, the set holding one
/// record per code, and one whose name or numeric code isn't QuantLib's is refused.
/// </remarks>
public static class ConventionRecords
{
    /// <summary>The records describing a calendar under a name.</summary>
    /// <param name="name">The name the calendar takes.</param>
    /// <param name="value">The calendar.</param>
    /// <param name="set">The set the records resolve in; <see cref="ConventionSet.QuantLib"/> when null.</param>
    /// <returns>The records; the last defines <paramref name="name"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">The calendar is of a type no record describes.</exception>
    public static ConventionSnapshot Describe(string name, Calendar value, ConventionSet? set = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        var d = new Describer(ConventionNames.Checked(name, nameof(name)), set ?? ConventionSet.QuantLib);
        d.Calendar(name, value);
        return d.Snapshot;
    }

    /// <summary>The records describing a day counter under a name.</summary>
    /// <param name="name">The name the day counter takes.</param>
    /// <param name="value">The day counter.</param>
    /// <param name="set">The set the records resolve in; <see cref="ConventionSet.QuantLib"/> when null.</param>
    /// <returns>The records; the last defines <paramref name="name"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">The day counter carries a trade's data, or no record describes it.</exception>
    public static ConventionSnapshot Describe(string name, DayCounter value, ConventionSet? set = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        var d = new Describer(ConventionNames.Checked(name, nameof(name)), set ?? ConventionSet.QuantLib);
        d.DayCounter(name, value);
        return d.Snapshot;
    }

    /// <summary>The records describing an Ibor or overnight index under a name.</summary>
    /// <param name="name">The name the index takes.</param>
    /// <param name="value">The index; its forwarding curve is a market object, which no record holds.</param>
    /// <param name="set">The set the records resolve in; <see cref="ConventionSet.QuantLib"/> when null.</param>
    /// <returns>The records; the last defines <paramref name="name"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">The index is a EUR Libor or the CDI index, whose behaviour its fields don't
    /// give, and equals none of the set's presets; or it has no currency.</exception>
    public static ConventionSnapshot Describe(string name, IborIndex value, ConventionSet? set = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        var d = new Describer(ConventionNames.Checked(name, nameof(name)), set ?? ConventionSet.QuantLib);
        d.Index(name, value);
        return d.Snapshot;
    }

    /// <summary>The records describing a currency under a name.</summary>
    /// <param name="name">The name the currency takes.</param>
    /// <param name="value">The currency, one of QuantLib's by its code.</param>
    /// <param name="set">The set the records resolve in; <see cref="ConventionSet.QuantLib"/> when null.</param>
    /// <returns>The records; the last defines <paramref name="name"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">No currency of QuantLib's has the value's code, or its name or numeric code
    /// isn't QuantLib's.</exception>
    public static ConventionSnapshot Describe(string name, Currency value, ConventionSet? set = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        var d = new Describer(ConventionNames.Checked(name, nameof(name)), set ?? ConventionSet.QuantLib);
        d.Currency(name, value);
        return d.Snapshot;
    }

    // Writes records for a value and its parts, each part a preset's name where it equals one.
    private sealed class Describer(string root, ConventionSet set)
    {
        // QuantLib's aliases between presets sharing one implementation: a value equal to one is named by its target
        private readonly HashSet<string> aliases = [.. set.Layers.SelectMany(l => l.Records).OfType<AliasRecord>().Select(a => a.Name)];
        private int next;

        public ConventionSnapshot Snapshot { get; private set; } = ConventionSnapshot.Empty;

        public void Calendar(string name, Calendar value)
        {
            if (PresetEqualTo(value) is string preset)
            {
                Add(new AliasRecord(name, preset));
                return;
            }

            switch (value)
            {
                case JointCalendar j:
                {
                    string joint = j.AddedHolidays.IsEmpty && j.RemovedHolidays.IsEmpty ? name : Generated();
                    Add(new JointCalendarRecord(joint) { Rule = j.Rule, Members = [.. j.Calendars.Select(Part)] });
                    if (joint != name)
                    {
                        Add(new CalendarRecord(name, joint) { AddedHolidays = [.. j.AddedHolidays], RemovedHolidays = [.. j.RemovedHolidays] });
                    }

                    return;
                }

                case BespokeCalendar b:
                {
                    // a bespoke calendar's name is part of its value, so the record is under it, and the name an alias
                    Weekday[] weekends = [.. Enum.GetValues<Weekday>().Distinct().Where(w => b.IsWeekend(w))];
                    Add(new BespokeCalendarRecord(ConventionNames.Checked(b.Name, nameof(value)))
                    {
                        Weekends = [.. weekends],
                        Holidays = [.. b.AddedHolidays],
                        RemovedHolidays = [.. b.RemovedHolidays],
                    });
                    if (b.Name != name)
                    {
                        Add(new AliasRecord(name, b.Name));
                    }

                    return;
                }
            }

            // a preset's calendar with edits of its own: the edits that turn the set's preset into it
            if (PresetOfType(value) is not (string preset2, Calendar bas))
            {
                throw new ArgumentException($"no record describes a calendar of type {value.GetType().Name}", nameof(value));
            }

            Add(new CalendarRecord(name, preset2)
            {
                AddedHolidays = [.. value.AddedHolidays.Except(bas.AddedHolidays).Union(bas.RemovedHolidays.Except(value.RemovedHolidays))],
                RemovedHolidays = [.. value.RemovedHolidays.Except(bas.RemovedHolidays).Union(bas.AddedHolidays.Except(value.AddedHolidays))],
            });
        }

        public void DayCounter(string name, DayCounter value)
        {
            foreach (string preset in PresetFactories.DayCounters.Keys.Where(k => !aliases.Contains(k)))
            {
                if (set.DayCounter(preset) == value)
                {
                    Add(new AliasRecord(name, preset));
                    return;
                }
            }

            if (value is Business252 b)
            {
                string calendar = Part(b.Calendar);
                Add(new DayCounterRecord(name, "Business252") { Calendar = calendar });
                return;
            }

            throw new ArgumentException(
                $"the day counter {value.Name} carries a trade's data or is of a type no record describes", nameof(value));
        }

        public void Currency(string name, Currency value)
        {
            if (!PresetFactories.Currencies.TryGetValue(value.Code, out Func<Currency>? coded))
            {
                throw new ArgumentException($"no currency of QuantLib's has the code {value.Code}", nameof(value));
            }

            // the codes and the name are the currency's identity, which no record changes
            Currency quantLibs;
            using (ConventionProvider.Use(ConventionProvider.QuantLib))
            {
                quantLibs = coded();
            }

            if (value.Name != quantLibs.Name || value.NumericCode != quantLibs.NumericCode)
            {
                throw new ArgumentException(
                    $"the currency {value.Code}, {value.NumericCode}, \"{value.Name}\" isn't QuantLib's {value.Code}, "
                    + $"{quantLibs.NumericCode}, \"{quantLibs.Name}\": its codes and name are its identity, which no record changes",
                    nameof(value));
            }

            Currency preset = set.Currency(value.Code);
            if (!SameData(preset, value))
            {
                Add(new CurrencyPresetRecord(value.Code, value.Code)
                {
                    Symbol = value.Symbol == preset.Symbol ? null : value.Symbol,
                    FractionSymbol = value.FractionSymbol == preset.FractionSymbol ? null : value.FractionSymbol,
                    FractionsPerUnit = value.FractionsPerUnit == preset.FractionsPerUnit ? null : value.FractionsPerUnit,
                    Rounding = CurrencyRounding.Of(value.Rounding) == CurrencyRounding.Of(preset.Rounding) ? null : CurrencyRounding.Of(value.Rounding),
                    TriangulationCurrency = value.TriangulationCurrency?.Code == preset.TriangulationCurrency?.Code
                        ? null
                        : value.TriangulationCurrency?.Code ?? "",
                });
            }

            if (name != value.Code)
            {
                Add(new AliasRecord(name, value.Code));
            }
        }

        public void Index(string name, IborIndex value)
        {
            // a preset of the value's class, equal by every field
            foreach (string preset in PresetFactories.IborIndexes.Keys.Concat(PresetFactories.OvernightIndexes.Keys)
                .Where(k => !aliases.Contains(k)).Order(StringComparer.Ordinal))
            {
                if (SameIndex(set.IborIndex(preset), value))
                {
                    Add(new AliasRecord(name, preset));
                    return;
                }
            }

            if (value is EURLibor or Cdi)
            {
                throw new ArgumentException(
                    $"the {value.Name} index, a {value.GetType().Name} equal to none of the set's presets, has behaviour no record gives",
                    nameof(value));
            }

            Currency currency = value.Currency ?? throw new ArgumentException($"the {value.Name} index has no currency", nameof(value));
            Currency(currency.Code, currency);
            string dayCounter = DayCounterPart(value.DayCounter);
            switch (value)
            {
                case OvernightIndex:
                    Add(new OvernightIndexRecord(name)
                    {
                        Family = value.FamilyName,
                        FixingDays = value.FixingDays,
                        Currency = currency.Code,
                        FixingCalendar = Part(value.FixingCalendar),
                        DayCounter = dayCounter,
                    });
                    break;
                case Libor libor:
                    // its fixing calendar is London's, which the public constructor joins to its financial centre
                    Add(new IborIndexRecord(name)
                    {
                        Family = value.FamilyName,
                        Tenor = value.Tenor,
                        FixingDays = value.FixingDays,
                        Currency = currency.Code,
                        DayCounter = dayCounter,
                        Style = IborIndexStyle.Libor,
                        FinancialCenterCalendar = Part(((JointCalendar)libor.JointCalendar).Calendars[1]),
                    });
                    break;
                case CustomIborIndex custom:
                    Add(new IborIndexRecord(name)
                    {
                        Family = value.FamilyName,
                        Tenor = value.Tenor,
                        FixingDays = value.FixingDays,
                        Currency = currency.Code,
                        FixingCalendar = Part(value.FixingCalendar),
                        Convention = value.BusinessDayConvention,
                        EndOfMonth = value.EndOfMonth,
                        DayCounter = dayCounter,
                        Style = IborIndexStyle.Custom,
                        ValueCalendar = Part(custom.ValueCalendar),
                        MaturityCalendar = Part(custom.MaturityCalendar),
                    });
                    break;
                default:
                    Add(new IborIndexRecord(name)
                    {
                        Family = value.FamilyName,
                        Tenor = value.Tenor,
                        FixingDays = value.FixingDays,
                        Currency = currency.Code,
                        FixingCalendar = Part(value.FixingCalendar),
                        Convention = value.BusinessDayConvention,
                        EndOfMonth = value.EndOfMonth,
                        DayCounter = dayCounter,
                    });
                    break;
            }
        }

        // Two indexes of one class and every field equal: a Libor's joint calendar among them.
        private static bool SameIndex(IborIndex a, IborIndex b) =>
            a.GetType() == b.GetType() && a.FamilyName == b.FamilyName && a.Tenor == b.Tenor && a.FixingDays == b.FixingDays
            && a.Currency == b.Currency && a.FixingCalendar == b.FixingCalendar && a.BusinessDayConvention == b.BusinessDayConvention
            && a.EndOfMonth == b.EndOfMonth && a.DayCounter == b.DayCounter
            && (a is not Libor la || la.JointCalendar == ((Libor)b).JointCalendar);

        // A day counter part's name: a preset's where it equals one, else a generated name with its record.
        private string DayCounterPart(DayCounter part)
        {
            foreach (string preset in PresetFactories.DayCounters.Keys.Where(k => !aliases.Contains(k)))
            {
                if (set.DayCounter(preset) == part)
                {
                    return preset;
                }
            }

            string generated = Generated();
            DayCounter(generated, part);
            return generated;
        }

        private static bool SameData(Currency a, Currency b) =>
            a == b && a.NumericCode == b.NumericCode && a.Symbol == b.Symbol && a.FractionSymbol == b.FractionSymbol
            && a.FractionsPerUnit == b.FractionsPerUnit && CurrencyRounding.Of(a.Rounding) == CurrencyRounding.Of(b.Rounding)
            && a.TriangulationCurrency?.Code == b.TriangulationCurrency?.Code;

        // A part's name: a preset's where it equals one, else a generated name with records of its own.
        private string Part(Calendar part)
        {
            if (PresetEqualTo(part) is string preset)
            {
                return preset;
            }

            string generated = Generated();
            Calendar(generated, part);
            return generated;
        }

        private string? PresetEqualTo(Calendar value)
        {
            foreach (string preset in PresetFactories.Calendars.Keys.Where(k => !aliases.Contains(k)).Order(StringComparer.Ordinal))
            {
                if (set.Calendar(preset) == value)
                {
                    return preset;
                }
            }

            return null;
        }

        // The preset of the value's type and name, and the set's calendar of it, with the set's edits.
        private (string, Calendar)? PresetOfType(Calendar value)
        {
            foreach (string preset in PresetFactories.Calendars.Keys.Where(k => !aliases.Contains(k)).Order(StringComparer.Ordinal))
            {
                Calendar c = set.Calendar(preset);
                if (c.GetType() == value.GetType() && c.Name == value.Name)
                {
                    return (preset, c);
                }
            }

            return null;
        }

        private string Generated()
        {
            string name;
            do
            {
                name = $"{root}/{++next}";
            }
            while (Snapshot.Records.Any(r => r.Name == name));
            return name;
        }

        private void Add(ConventionRecord record) => Snapshot = Snapshot.Add(record);
    }
}
