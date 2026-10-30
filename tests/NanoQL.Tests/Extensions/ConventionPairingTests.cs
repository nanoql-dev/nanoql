// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// A coded preset's name keeps its class (CVD28): a record under it must be one the class can take, since a coded
/// construction can't become another kind. Each kind a calendar, day counter and currency preset's name may take is
/// taken and reaches the coded construction; every other is refused at <see cref="ConventionSet.With"/>, naming the
/// record.
/// </summary>
public class ConventionPairingTests
{
    public static TheoryData<string, string?> Pairings() => new()
    {
        // a calendar preset: its own preset record, or a calendar record based on itself
        { "<CalendarPreset name=\"TARGET\" base=\"TARGET\"><AddedHoliday>2026-10-16</AddedHoliday></CalendarPreset>", null },
        { "<CalendarPreset name=\"TARGET\"><QuantLibName>TARGET</QuantLibName><AddedHoliday>2026-10-16</AddedHoliday></CalendarPreset>", null },
        { "<Calendar name=\"TARGET\" base=\"TARGET\"><AddedHoliday>2026-10-16</AddedHoliday></Calendar>", null },
        { "<Calendar name=\"TARGET\" base=\"UnitedKingdom.Settlement\" />", "a calendar preset takes only a calendar record based on itself" },
        { "<CalendarPreset name=\"TARGET\" base=\"UnitedKingdom.Settlement\" />", "a preset record takes only its own name as its base" },
        { "<JointCalendar name=\"TARGET\"><Member>TARGET</Member><Member>UnitedKingdom.Settlement</Member></JointCalendar>", "a calendar preset takes no joint or bespoke calendar" },
        { "<BespokeCalendar name=\"TARGET\"><Weekend>Sunday</Weekend></BespokeCalendar>", "a calendar preset takes no joint or bespoke calendar" },
        { "<Alias name=\"TARGET\" base=\"UnitedStates.NYSE\" />", "a preset's name takes an alias only where QuantLib shares the presets' implementation" },
        { "<CalendarPreset name=\"Mars\"><QuantLibName>Mars</QuantLibName></CalendarPreset>", "no calendar preset has the name" },

        // QuantLib's shared implementations: an alias, never a record of its own
        { "<Alias name=\"Indonesia.JSX\" base=\"Indonesia.BEJ\" />", null },
        { "<CalendarPreset name=\"Indonesia.JSX\" base=\"Indonesia.JSX\"><AddedHoliday>2026-10-16</AddedHoliday></CalendarPreset>", "QuantLib's calendar shares Indonesia.BEJ's implementation: edit Indonesia.BEJ" },
        { "<Calendar name=\"Indonesia.IDX\" base=\"Indonesia.IDX\" />", "QuantLib's calendar shares Indonesia.BEJ's implementation: edit Indonesia.BEJ" },
        { "<CalendarPreset name=\"NullCalendar\" base=\"NullCalendar\"><AddedHoliday>2026-10-16</AddedHoliday></CalendarPreset>", "NullCalendar is a technical placeholder and takes no edits" },

        // a day counter preset: its own preset record, or a day counter record based on itself
        { "<DayCounterPreset name=\"Actual360\" base=\"Actual360\" />", null },
        { "<DayCounter name=\"Actual360\" base=\"Actual360\" />", null },
        { "<DayCounter name=\"Actual360\" base=\"Thirty360.BondBasis\" />", "a day counter preset takes only a day counter record based on itself" },
        { "<Alias name=\"Actual360\" base=\"Actual365Fixed\" />", "a preset's name takes an alias only where QuantLib shares the presets' implementation" },
        { "<DayCounterPreset name=\"Thirty360.EurobondBasis\" base=\"Thirty360.EurobondBasis\" />", "QuantLib's day counter shares Thirty360.European's implementation: use Thirty360.European" },
        { "<DayCounterPreset name=\"Actual360\" base=\"Actual360\"><Calendar>TARGET</Calendar></DayCounterPreset>", "only Business252 counts a calendar's business days" },
        { "<DayCounterPreset name=\"Business252\" base=\"Business252\"><Calendar>TARGET</Calendar></DayCounterPreset>", null },
        { "<DayCounterPreset name=\"Lunar\"><QuantLibName>Lunar</QuantLibName></DayCounterPreset>", "no day counter preset has the name" },

        // a currency preset: its own record based on itself
        { "<CurrencyPreset name=\"EUR\" base=\"EUR\"><Symbol>€</Symbol></CurrencyPreset>", null },
        { "<CurrencyPreset name=\"EUR\" base=\"USD\" />", "a preset record takes only its own name as its base" },
        { "<CurrencyPreset name=\"EUR\"><Symbol>€</Symbol></CurrencyPreset>", "a field is missing" },
        { "<Alias name=\"EUR\" base=\"USD\" />", "a preset's name takes an alias only where QuantLib shares the presets' implementation" },
    };

    [Theory]
    [MemberData(nameof(Pairings))]
    public void ACodedPresetsNameTakesOnlyARecordItsClassCanTake(string record, string? refusal)
    {
        if (refusal is not null)
        {
            ArgumentException e = Assert.Throws<ArgumentException>(() => ConventionsText.OnQuantLib(record));
            Assert.Contains(refusal, e.Message, StringComparison.Ordinal);
            Assert.Equal("snapshot", e.ParamName);
            return;
        }

        // taken: the coded construction under the set reads it
        ConventionSet set = ConventionsText.OnQuantLib(record);
        string name = ConventionsText.Records(record).Records[0].Name;
        using (ConventionProvider.Use(set))
        {
            if (CodedPresets.Calendars.TryGetValue(name, out Func<Calendar>? calendar))
            {
                Assert.Equal(set.Calendar(name), calendar());
            }
            else if (CodedPresets.DayCounters.TryGetValue(name, out Func<DayCounter>? dayCounter))
            {
                Assert.Equal(set.DayCounter(name), dayCounter());
            }
            else
            {
                Currency coded = CodedPresets.Currencies[name]();
                Assert.Equal((set.Currency(name), set.Currency(name).Symbol), (coded, coded.Symbol));
            }
        }
    }

    [Fact]
    public void ATakenCalendarPresetRecordEditsTheCodedPresetWhereverItIsBuilt()
    {
        ConventionSet set = ConventionsText.OnQuantLib("<CalendarPreset name=\"TARGET\" base=\"TARGET\"><AddedHoliday>2026-10-16</AddedHoliday></CalendarPreset>");
        var holiday = new Date(16, Month.October, 2026);

        using (ConventionProvider.Use(set))
        {
            Assert.True(new TARGET().IsHoliday(holiday));
        }

        Assert.True(set.Calendar("EUTA").IsHoliday(holiday));
        Assert.Equal(new Business252(new TARGET().AddHoliday(holiday)), ConventionsText.OnQuantLib(
            "<CalendarPreset name=\"TARGET\" base=\"TARGET\"><AddedHoliday>2026-10-16</AddedHoliday></CalendarPreset>"
            + "<DayCounterPreset name=\"Business252\" base=\"Business252\"><Calendar>TARGET</Calendar></DayCounterPreset>").DayCounter("Business252"));
    }
}
