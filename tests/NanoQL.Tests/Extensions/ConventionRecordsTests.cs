// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Extensions.Conventions;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// Built objects back to records: a value equal to a preset becomes an alias of it, any other its fields under its
/// name, its parts that equal no preset under generated names; each through
/// <see cref="ConventionRecords.Describe(string, Calendar, ConventionSet?)"/>, the writer, the reader and the set
/// resolves to an object equal to the one described. A day counter carrying a trade's data is refused.
/// </summary>
public class ConventionRecordsTests
{
    private static readonly Date Holiday = new(16, Month.October, 2026), MayDay = new(1, Month.May, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.October, 2026) };

    [Fact]
    public void AValueEqualToAPresetIsAnAliasOfIt()
    {
        Assert.Equal([new AliasRecord("Mine", "TARGET")], ConventionRecords.Describe("Mine", new TARGET()).Records);
        Assert.Equal([new AliasRecord("Mine", "Actual360")], ConventionRecords.Describe("Mine", new Actual360()).Records);
        Assert.Equal([new AliasRecord("Euro", "EUR")], ConventionRecords.Describe("Euro", new EURCurrency()).Records);
        Assert.Empty(ConventionRecords.Describe("EUR", new EURCurrency()).Records);
        Assert.Equal(new TARGET(), Resolved("Mine", new TARGET()));
    }

    [Fact]
    public void AnEditedPresetIsACalendarRecordOnItWithItsEdits()
    {
        Calendar edited = new TARGET().AddHoliday(Holiday).RemoveHoliday(MayDay);
        ConventionSnapshot s = ConventionRecords.Describe("MyTarget", edited);

        Assert.Equal([new CalendarRecord("MyTarget", "TARGET") { AddedHolidays = [Holiday], RemovedHolidays = [MayDay] }], s.Records);
        Calendar resolved = Resolved("MyTarget", edited);
        Assert.Equal(edited, resolved);
        Assert.IsType<TARGET>(resolved);
        Assert.Equal(edited.HolidayList(new Date(1, Month.January, 2026), new Date(31, Month.December, 2026)),
            resolved.HolidayList(new Date(1, Month.January, 2026), new Date(31, Month.December, 2026)));
    }

    [Fact]
    public void AnEditIsDescribedAgainstTheSetsOwnPreset()
    {
        // the set adds the holiday to TARGET, so QuantLib's plain TARGET is the set's with it removed
        ConventionSet set = ConventionsText.OnQuantLib("<Calendar name=\"TARGET\" base=\"TARGET\"><AddedHoliday>2026-10-16</AddedHoliday></Calendar>");
        ConventionSnapshot s = ConventionRecords.Describe("Plain", new TARGET(), set);

        Assert.Equal([new CalendarRecord("Plain", "TARGET") { RemovedHolidays = [Holiday] }], s.Records);
        Assert.True(set.With(ConventionsText.Read(ConventionsText.Write(s))).Calendar("Plain").IsBusinessDay(Holiday));
    }

    [Fact]
    public void AJointCalendarsPartsArePresetsOrGeneratedRecords()
    {
        var joint = new JointCalendar(new TARGET(), new UnitedKingdom().AddHoliday(Holiday), JointCalendarRule.JoinBusinessDays);
        ConventionSnapshot s = ConventionRecords.Describe("Desk", joint);

        Assert.Equal(
            [
                new CalendarRecord("Desk/1", "UnitedKingdom.Settlement") { AddedHolidays = [Holiday] },
                new JointCalendarRecord("Desk") { Rule = JointCalendarRule.JoinBusinessDays, Members = ["TARGET", "Desk/1"] },
            ],
            s.Records);
        Calendar resolved = Resolved("Desk", joint);
        Assert.Equal(joint, resolved);
        Assert.Equal([new TARGET(), new UnitedKingdom().AddHoliday(Holiday)], ((JointCalendar)resolved).Calendars);
        Assert.Equal(JointCalendarRule.JoinBusinessDays, ((JointCalendar)resolved).Rule);

        // a joint calendar with edits of its own is a generated joint record and a calendar record on it
        Calendar editedJoint = new JointCalendar(new TARGET(), new UnitedKingdom()).AddHoliday(new Date(6, Month.May, 2026));
        ConventionSnapshot edited = ConventionRecords.Describe("Both", editedJoint);
        Assert.Equal(["Both/1", "Both"], edited.Records.Select(r => r.Name));
        Assert.Equal(editedJoint, Resolved("Both", editedJoint));
    }

    [Fact]
    public void ABespokeCalendarIsARecordUnderItsOwnNameAndAnAliasUnderTheOneGiven()
    {
        // a Wednesday: a holiday added on a weekend day is no edit, as in QuantLib
        Date wednesday = Holiday - 2;
        Calendar island = new BespokeCalendar("Island").AddWeekend(Weekday.Friday).AddWeekend(Weekday.Saturday).AddHoliday(wednesday);
        ConventionSnapshot s = ConventionRecords.Describe("Mine", island);

        Assert.Equal(
            [
                new BespokeCalendarRecord("Island") { Weekends = [Weekday.Friday, Weekday.Saturday], Holidays = [wednesday] },
                new AliasRecord("Mine", "Island"),
            ],
            s.Records);
        Calendar resolved = Resolved("Mine", island);
        Assert.Equal(island, resolved);
        Assert.True(resolved.IsWeekend(Weekday.Friday) && !resolved.IsWeekend(Weekday.Sunday));
        Assert.Equal(island.BusinessDayList(Holiday - 10, Holiday + 10), resolved.BusinessDayList(Holiday - 10, Holiday + 10));
    }

    [Fact]
    public void ABusiness252OnAnotherCalendarIsADayCounterRecordOnBusiness252()
    {
        var onTarget = new Business252(new TARGET());
        Assert.Equal([new DayCounterRecord("TARGET252", "Business252") { Calendar = "TARGET" }], ConventionRecords.Describe("TARGET252", onTarget).Records);
        Assert.Equal(onTarget, Resolved("TARGET252", onTarget));

        var onEdited = new Business252(new UnitedKingdom().AddHoliday(Holiday));
        ConventionSnapshot s = ConventionRecords.Describe("UK252", onEdited);
        Assert.Equal(["UK252/1", "UK252"], s.Records.Select(r => r.Name));
        DayCounter resolved = Resolved("UK252", onEdited);
        Assert.Equal(onEdited, resolved);
        Assert.Equal(onEdited.DayCount(Holiday - 10, Holiday + 10), resolved.DayCount(Holiday - 10, Holiday + 10));
    }

    [Fact]
    public void ACurrencyWithTheSetsDataChangedIsARecordOfTheChangedFields()
    {
        ConventionSet edited = ConventionsText.OnQuantLib("<CurrencyPreset name=\"EUR\" base=\"EUR\"><Symbol>€</Symbol><Rounding><Type>Up</Type><Precision>3</Precision><Digit>5</Digit></Rounding></CurrencyPreset>");
        Currency eur = edited.Currency("EUR");
        ConventionSnapshot s = ConventionRecords.Describe("Euro", eur);

        Assert.Equal(
            [new CurrencyPresetRecord("EUR", "EUR") { Symbol = "€", Rounding = new CurrencyRounding(RoundingType.Up, 3, 5) }, new AliasRecord("Euro", "EUR")],
            s.Records);
        Currency resolved = Resolved("Euro", eur);
        Assert.Equal((eur, eur.Symbol, CurrencyRounding.Of(eur.Rounding)), (resolved, resolved.Symbol, CurrencyRounding.Of(resolved.Rounding)));
    }

    [Fact]
    public void ACurrencyQuantLibDoesntHaveIsRefused()
    {
        var own = new OwnCurrency();

        ArgumentException e = Assert.Throws<ArgumentException>(() => ConventionRecords.Describe("Mine", own));
        Assert.Equal("no currency of QuantLib's has the code TST", e.Message[..^" (Parameter 'value')".Length]);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("numeric code")]
    public void ACurrencyWhoseIdentityIsntQuantLibsIsRefused(string field)
    {
        // no record changes a currency's codes or name, so none describes a currency that does
        Currency value = field == "name" ? new RenamedEuro() : new RenumberedEuro();

        ArgumentException e = Assert.Throws<ArgumentException>(() => ConventionRecords.Describe("Mine", value));
        Assert.StartsWith(
            field == "name"
                ? "the currency EUR, 978, \"Euro\" isn't QuantLib's EUR, 978, \"European Euro\""
                : "the currency EUR, 979, \"European Euro\" isn't QuantLib's EUR, 978, \"European Euro\"",
            e.Message,
            StringComparison.Ordinal);
        Assert.Equal("value", e.ParamName);
    }

    [Fact]
    public void ADayCounterCarryingATradesDataIsRefused()
    {
        var schedule = new Schedule(
            new Date(16, Month.October, 2026), new Date(16, Month.October, 2028), new Period(6, TimeUnit.Months), new TARGET(),
            BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, false, Context);

        Assert.Throws<ArgumentException>(() => ConventionRecords.Describe("Isma", new ActualActual(ActualActualConvention.ISMA, schedule)));
        Assert.Throws<ArgumentException>(() => ConventionRecords.Describe("Isda", new Thirty360(Thirty360Convention.ISDA, new Date(16, Month.October, 2028))));
        Assert.Equal([new AliasRecord("Isma", "ActualActual.ISMA")], ConventionRecords.Describe("Isma", new ActualActual(ActualActualConvention.ISMA)).Records);
    }

    [Fact]
    public void ADescribedNameMustBeAName()
    {
        Assert.Throws<ArgumentNullException>(() => ConventionRecords.Describe(null!, new TARGET()));
        Assert.Throws<ArgumentException>(() => ConventionRecords.Describe(" Mine", new TARGET()));
        Assert.Throws<ArgumentNullException>(() => ConventionRecords.Describe("Mine", (Calendar)null!));
    }

    [Fact]
    public void AUsersOwnNameMayNotTakeAGeneratedNamesFormWithoutItsRoot()
    {
        Assert.Throws<ArgumentException>(() => ConventionsText.OnQuantLib("<Calendar name=\"Desk/1\" base=\"TARGET\" />"));
        Assert.Throws<ArgumentException>(() => ConventionsText.OnQuantLib("<Calendar name=\"Desk\" base=\"Desk/01\" /><Calendar name=\"Desk/01\" base=\"TARGET\" />"));
        Assert.Single(ConventionsText.OnQuantLib("<Calendar name=\"Desk\" base=\"Desk/1\" /><Calendar name=\"Desk/1\" base=\"TARGET\" />").Layers.Skip(1));
    }

    // The value a description resolves to after the writer, the reader and the set.
    private static T Resolved<T>(string name, T value)
        where T : class
    {
        ConventionSnapshot s = value switch
        {
            Calendar c => ConventionRecords.Describe(name, c),
            DayCounter d => ConventionRecords.Describe(name, d),
            Currency c => ConventionRecords.Describe(name, c),
            _ => throw new ArgumentException("no kind", nameof(value)),
        };
        ConventionSet set = ConventionSet.QuantLib.With(ConventionsText.Read(ConventionsText.Write(s)));
        return (T)(object)(value switch
        {
            Calendar => set.Calendar(name),
            DayCounter => set.DayCounter(name),
            _ => set.Currency(name),
        });
    }

    private sealed class OwnCurrency() : Currency(new CurrencyData("Test", "TST", 999, "T", "t", 100, new Rounding()));

    private sealed class RenamedEuro() : Currency(new CurrencyData("Euro", "EUR", 978, "", "", 100, new Rounding(2, RoundingType.Closest, 5)));

    private sealed class RenumberedEuro() : Currency(new CurrencyData("European Euro", "EUR", 979, "", "", 100, new Rounding(2, RoundingType.Closest, 5)));
}
