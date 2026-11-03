// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Reflection;
using NanoQL.Extensions.Conventions;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// Field sensitivity on the kinds from calendars to indexes: every field of every kind, built with two values in a
/// setup where it matters, changes what the resolved value does (its holidays, counts or data), or the second value is
/// refused where the field is a preset's identity. The table holds a row for every field the kinds have.
/// </summary>
public class FieldSensitivityTests
{
    private const string TargetAndUk = "<Member>TARGET</Member><Member>UnitedKingdom.Settlement</Member>";

    // kind, field, the layer below, record A, record B, what is looked up (kind:name), and whether B is refused
    private static readonly (string Kind, string Field, string Lower, string A, string B, string Lookup, bool Refused)[] Rows =
    [
        ("CalendarPreset", "QuantLibName", "", "<CalendarPreset name=\"TARGET\"><QuantLibName>TARGET</QuantLibName></CalendarPreset>",
            "<CalendarPreset name=\"TARGET\"><QuantLibName>Target</QuantLibName></CalendarPreset>", "calendar:TARGET", true),
        ("CalendarPreset", "AddedHoliday", "", "<CalendarPreset name=\"TARGET\" base=\"TARGET\" />",
            "<CalendarPreset name=\"TARGET\" base=\"TARGET\"><AddedHoliday>2026-10-16</AddedHoliday></CalendarPreset>", "calendar:TARGET", false),
        ("CalendarPreset", "RemovedHoliday", "", "<CalendarPreset name=\"TARGET\" base=\"TARGET\" />",
            "<CalendarPreset name=\"TARGET\" base=\"TARGET\"><RemovedHoliday>2026-05-01</RemovedHoliday></CalendarPreset>", "calendar:TARGET", false),
        ("CalendarPreset", "@base", "<CalendarPreset name=\"TARGET\" base=\"TARGET\"><AddedHoliday>2026-10-16</AddedHoliday></CalendarPreset>",
            "<CalendarPreset name=\"TARGET\" base=\"TARGET\" />", "<CalendarPreset name=\"TARGET\"><QuantLibName>TARGET</QuantLibName></CalendarPreset>", "calendar:TARGET", false),
        ("Calendar", "@base", "", "<Calendar name=\"X\" base=\"TARGET\" />", "<Calendar name=\"X\" base=\"UnitedKingdom.Settlement\" />", "calendar:X", false),
        ("Calendar", "AddedHoliday", "", "<Calendar name=\"X\" base=\"TARGET\" />",
            "<Calendar name=\"X\" base=\"TARGET\"><AddedHoliday>2026-10-16</AddedHoliday></Calendar>", "calendar:X", false),
        ("Calendar", "RemovedHoliday", "", "<Calendar name=\"X\" base=\"TARGET\" />",
            "<Calendar name=\"X\" base=\"TARGET\"><RemovedHoliday>2026-05-01</RemovedHoliday></Calendar>", "calendar:X", false),
        ("JointCalendar", "Rule", "", $"<JointCalendar name=\"X\"><Rule>JoinHolidays</Rule>{TargetAndUk}</JointCalendar>",
            $"<JointCalendar name=\"X\"><Rule>JoinBusinessDays</Rule>{TargetAndUk}</JointCalendar>", "calendar:X", false),
        ("JointCalendar", "Member", "", $"<JointCalendar name=\"X\">{TargetAndUk}</JointCalendar>",
            "<JointCalendar name=\"X\"><Member>TARGET</Member><Member>UnitedStates.NYSE</Member></JointCalendar>", "calendar:X", false),
        ("JointCalendar", "@base", $"<JointCalendar name=\"J1\">{TargetAndUk}</JointCalendar><JointCalendar name=\"J2\"><Member>TARGET</Member><Member>UnitedStates.NYSE</Member></JointCalendar>",
            "<JointCalendar name=\"X\" base=\"J1\" />", "<JointCalendar name=\"X\" base=\"J2\" />", "calendar:X", false),
        ("BespokeCalendar", "Weekend", "", "<BespokeCalendar name=\"X\"><Weekend>Saturday</Weekend><Weekend>Sunday</Weekend></BespokeCalendar>",
            "<BespokeCalendar name=\"X\"><Weekend>Friday</Weekend></BespokeCalendar>", "calendar:X", false),
        ("BespokeCalendar", "Holiday", "", "<BespokeCalendar name=\"X\" />",
            "<BespokeCalendar name=\"X\"><Holiday>2026-10-16</Holiday></BespokeCalendar>", "calendar:X", false),
        ("BespokeCalendar", "RemovedHoliday", "", "<BespokeCalendar name=\"X\"><Weekend>Sunday</Weekend></BespokeCalendar>",
            "<BespokeCalendar name=\"X\"><Weekend>Sunday</Weekend><RemovedHoliday>2026-10-18</RemovedHoliday></BespokeCalendar>", "calendar:X", false),
        ("BespokeCalendar", "@base", "<BespokeCalendar name=\"B1\"><Weekend>Friday</Weekend></BespokeCalendar><BespokeCalendar name=\"B2\"><Weekend>Sunday</Weekend></BespokeCalendar>",
            "<BespokeCalendar name=\"X\" base=\"B1\" />", "<BespokeCalendar name=\"X\" base=\"B2\" />", "calendar:X", false),
        ("DayCounterPreset", "QuantLibName", "", "<DayCounterPreset name=\"Actual360\"><QuantLibName>Actual/360</QuantLibName></DayCounterPreset>",
            "<DayCounterPreset name=\"Actual360\"><QuantLibName>ACT/360</QuantLibName></DayCounterPreset>", "daycounter:Actual360", true),
        ("DayCounterPreset", "Calendar", "", "<DayCounterPreset name=\"Business252\" base=\"Business252\"><Calendar>TARGET</Calendar></DayCounterPreset>",
            "<DayCounterPreset name=\"Business252\" base=\"Business252\"><Calendar>UnitedKingdom.Settlement</Calendar></DayCounterPreset>", "daycounter:Business252", false),
        ("DayCounterPreset", "@base", "<DayCounterPreset name=\"Business252\" base=\"Business252\"><Calendar>TARGET</Calendar></DayCounterPreset>",
            "<DayCounterPreset name=\"Business252\" base=\"Business252\" />", "<DayCounterPreset name=\"Business252\"><QuantLibName>Business/252(Brazil)</QuantLibName></DayCounterPreset>", "daycounter:Business252", false),
        ("DayCounter", "@base", "", "<DayCounter name=\"X\" base=\"Actual360\" />", "<DayCounter name=\"X\" base=\"Actual365Fixed\" />", "daycounter:X", false),
        ("DayCounter", "Calendar", "", "<DayCounter name=\"X\" base=\"Business252\"><Calendar>TARGET</Calendar></DayCounter>",
            "<DayCounter name=\"X\" base=\"Business252\"><Calendar>UnitedKingdom.Settlement</Calendar></DayCounter>", "daycounter:X", false),
        ("CurrencyPreset", "QuantLibName", "", Currency(""), Currency("<QuantLibName>Euro</QuantLibName>"), "currency:EUR", true),
        ("CurrencyPreset", "NumericCode", "", Currency("<NumericCode>978</NumericCode>"), Currency("<NumericCode>979</NumericCode>"), "currency:EUR", true),
        ("CurrencyPreset", "Symbol", "", Currency(""), Currency("<Symbol>€</Symbol>"), "currency:EUR", false),
        ("CurrencyPreset", "FractionSymbol", "", Currency(""), Currency("<FractionSymbol>ct</FractionSymbol>"), "currency:EUR", false),
        ("CurrencyPreset", "FractionsPerUnit", "", Currency(""), Currency("<FractionsPerUnit>1000</FractionsPerUnit>"), "currency:EUR", false),
        ("CurrencyPreset", "Rounding", "", Currency(""), Currency("<Rounding><Type>Up</Type><Precision>2</Precision><Digit>5</Digit></Rounding>"), "currency:EUR", false),
        ("Rounding", "Type", "", Currency("<Rounding><Type>Closest</Type><Precision>2</Precision><Digit>5</Digit></Rounding>"),
            Currency("<Rounding><Type>Down</Type><Precision>2</Precision><Digit>5</Digit></Rounding>"), "currency:EUR", false),
        ("Rounding", "Precision", "", Currency("<Rounding><Type>Closest</Type><Precision>2</Precision><Digit>5</Digit></Rounding>"),
            Currency("<Rounding><Type>Closest</Type><Precision>4</Precision><Digit>5</Digit></Rounding>"), "currency:EUR", false),
        ("Rounding", "Digit", "", Currency("<Rounding><Type>Closest</Type><Precision>2</Precision><Digit>5</Digit></Rounding>"),
            Currency("<Rounding><Type>Closest</Type><Precision>2</Precision><Digit>9</Digit></Rounding>"), "currency:EUR", false),
        ("CurrencyPreset", "TriangulationCurrency", "", Currency(""), Currency("<TriangulationCurrency>USD</TriangulationCurrency>"), "currency:EUR", false),
        ("CurrencyPreset", "@base", Currency("<Symbol>€</Symbol>"), Currency(""),
            "<CurrencyPreset name=\"EUR\"><QuantLibName>European Euro</QuantLibName><NumericCode>978</NumericCode><Symbol></Symbol><FractionSymbol></FractionSymbol><FractionsPerUnit>100</FractionsPerUnit><Rounding><Type>Closest</Type><Precision>2</Precision><Digit>5</Digit></Rounding></CurrencyPreset>",
            "currency:EUR", false),
        ("Alias", "@base", "", "<Alias name=\"X\" base=\"TARGET\" />", "<Alias name=\"X\" base=\"UnitedKingdom.Settlement\" />", "calendar:X", false),

        // the region and index kinds
        ("Region", "RegionName", "",
            "<Region name=\"X\"><RegionName>Atlantis</RegionName><Code>AT</Code></Region>",
            "<Region name=\"X\"><RegionName>Lemuria</RegionName><Code>AT</Code></Region>", "region:X", false),
        ("Region", "Code", "",
            "<Region name=\"X\"><RegionName>Atlantis</RegionName><Code>AT</Code></Region>",
            "<Region name=\"X\"><RegionName>Atlantis</RegionName><Code>LE</Code></Region>", "region:X", false),
        ("Region", "@base", "<Region name=\"R1\"><RegionName>Atlantis</RegionName><Code>AT</Code></Region><Region name=\"R2\"><RegionName>Lemuria</RegionName><Code>LE</Code></Region>",
            "<Region name=\"X\" base=\"R1\"></Region>",
            "<Region name=\"X\" base=\"R2\"></Region>", "region:X", false),
        ("IborIndex", "Family", "",
            "<IborIndex name=\"X\"><Family>Fam</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex>",
            "<IborIndex name=\"X\"><Family>Other</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex>", "ibor:X", false),
        ("IborIndex", "Tenor", "",
            "<IborIndex name=\"X\"><Family>Fam</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex>",
            "<IborIndex name=\"X\"><Family>Fam</Family><Tenor>3M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex>", "ibor:X", false),
        ("IborIndex", "FixingDays", "",
            "<IborIndex name=\"X\"><Family>Fam</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex>",
            "<IborIndex name=\"X\"><Family>Fam</Family><Tenor>6M</Tenor><FixingDays>1</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex>", "ibor:X", false),
        ("IborIndex", "Currency", "",
            "<IborIndex name=\"X\"><Family>Fam</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex>",
            "<IborIndex name=\"X\"><Family>Fam</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>USD</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex>", "ibor:X", false),
        ("IborIndex", "FixingCalendar", "",
            "<IborIndex name=\"X\"><Family>Fam</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex>",
            "<IborIndex name=\"X\"><Family>Fam</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>UnitedKingdom.Settlement</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex>", "ibor:X", false),
        ("IborIndex", "Convention", "",
            "<IborIndex name=\"X\"><Family>Fam</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex>",
            "<IborIndex name=\"X\"><Family>Fam</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>Following</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex>", "ibor:X", false),
        ("IborIndex", "EndOfMonth", "",
            "<IborIndex name=\"X\"><Family>Fam</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex>",
            "<IborIndex name=\"X\"><Family>Fam</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>false</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex>", "ibor:X", false),
        ("IborIndex", "DayCounter", "",
            "<IborIndex name=\"X\"><Family>Fam</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex>",
            "<IborIndex name=\"X\"><Family>Fam</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual365Fixed</DayCounter></IborIndex>", "ibor:X", false),
        ("IborIndex", "Style", "",
            "<IborIndex name=\"X\"><Family>Cus</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter><Style>Custom</Style><ValueCalendar>TARGET</ValueCalendar><MaturityCalendar>TARGET</MaturityCalendar></IborIndex>",
            "<IborIndex name=\"X\"><Family>Fam</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex>", "ibor:X", false),
        ("IborIndex", "FinancialCenterCalendar", "",
            "<IborIndex name=\"X\"><Family>Lib</Family><Tenor>3M</Tenor><FixingDays>2</FixingDays><Currency>CHF</Currency><DayCounter>Actual360</DayCounter><Style>Libor</Style><FinancialCenterCalendar>Switzerland</FinancialCenterCalendar></IborIndex>",
            "<IborIndex name=\"X\"><Family>Lib</Family><Tenor>3M</Tenor><FixingDays>2</FixingDays><Currency>CHF</Currency><DayCounter>Actual360</DayCounter><Style>Libor</Style><FinancialCenterCalendar>UnitedStates.LiborImpact</FinancialCenterCalendar></IborIndex>", "ibor:X", false),
        ("IborIndex", "ValueCalendar", "",
            "<IborIndex name=\"X\"><Family>Cus</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter><Style>Custom</Style><ValueCalendar>TARGET</ValueCalendar><MaturityCalendar>TARGET</MaturityCalendar></IborIndex>",
            "<IborIndex name=\"X\"><Family>Cus</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter><Style>Custom</Style><ValueCalendar>UnitedKingdom.Settlement</ValueCalendar><MaturityCalendar>TARGET</MaturityCalendar></IborIndex>", "ibor:X", false),
        ("IborIndex", "MaturityCalendar", "",
            "<IborIndex name=\"X\"><Family>Cus</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter><Style>Custom</Style><ValueCalendar>TARGET</ValueCalendar><MaturityCalendar>TARGET</MaturityCalendar></IborIndex>",
            "<IborIndex name=\"X\"><Family>Cus</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter><Style>Custom</Style><ValueCalendar>TARGET</ValueCalendar><MaturityCalendar>UnitedKingdom.Settlement</MaturityCalendar></IborIndex>", "ibor:X", false),
        ("IborIndex", "@base", "<IborIndex name=\"I1\"><Family>Fam</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex><IborIndex name=\"I2\"><Family>Fam</Family><Tenor>3M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex>",
            "<IborIndex name=\"X\" base=\"I1\"></IborIndex>",
            "<IborIndex name=\"X\" base=\"I2\"></IborIndex>", "ibor:X", false),
        ("IborFamily", "Family", "",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>",
            "<IborFamily name=\"X\"><Family>Other</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>", "family:X", false),
        ("IborFamily", "FixingDays", "",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>1</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>", "family:X", false),
        ("IborFamily", "FixingDaysAtOneDay", "",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>1</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>", "family:X@1D", false),
        ("IborFamily", "Currency", "",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>USD</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>", "family:X", false),
        ("IborFamily", "FixingCalendar", "",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>UnitedKingdom.Settlement</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>", "family:X", false),
        ("IborFamily", "FinancialCenterCalendar", "",
            "<IborFamily name=\"X\"><Family>Lib</Family><FixingDays>2</FixingDays><Currency>CHF</Currency><FixingCalendar>UnitedKingdom.Exchange</FixingCalendar><FinancialCenterCalendar>Switzerland</FinancialCenterCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>",
            "<IborFamily name=\"X\"><Family>Lib</Family><FixingDays>2</FixingDays><Currency>CHF</Currency><FixingCalendar>UnitedKingdom.Exchange</FixingCalendar><FinancialCenterCalendar>TARGET</FinancialCenterCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>", "family:X", false),
        ("IborFamily", "DayCounter", "",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual365Fixed</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>", "family:X", false),
        ("IborFamily", "Unit", "",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>", "family:X@1D", false),
        ("IborFamily", "@base", "<IborFamily name=\"F1\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily><IborFamily name=\"F2\"><Family>Fam</Family><FixingDays>1</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>",
            "<IborFamily name=\"X\" base=\"F1\"></IborFamily>",
            "<IborFamily name=\"X\" base=\"F2\"></IborFamily>", "family:X", false),
        ("IborFamilyUnit", "TimeUnit", "",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Weeks</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>", "family:X@1D", false),
        ("IborFamilyUnit", "Convention", "",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>Following</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>", "family:X", false),
        ("IborFamilyUnit", "EndOfMonth", "",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth></Unit></IborFamily>",
            "<IborFamily name=\"X\"><Family>Fam</Family><FixingDays>2</FixingDays><FixingDaysAtOneDay>0</FixingDaysAtOneDay><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter><Unit><TimeUnit>Days</TimeUnit><Convention>Following</Convention><EndOfMonth>false</EndOfMonth></Unit><Unit><TimeUnit>Months</TimeUnit><Convention>ModifiedFollowing</Convention><EndOfMonth>false</EndOfMonth></Unit></IborFamily>", "family:X", false),
        ("OvernightIndex", "Family", "",
            "<OvernightIndex name=\"X\"><Family>On</Family><FixingDays>0</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter></OvernightIndex>",
            "<OvernightIndex name=\"X\"><Family>Other</Family><FixingDays>0</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter></OvernightIndex>", "ibor:X", false),
        ("OvernightIndex", "FixingDays", "",
            "<OvernightIndex name=\"X\"><Family>On</Family><FixingDays>0</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter></OvernightIndex>",
            "<OvernightIndex name=\"X\"><Family>On</Family><FixingDays>1</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter></OvernightIndex>", "ibor:X", false),
        ("OvernightIndex", "Currency", "",
            "<OvernightIndex name=\"X\"><Family>On</Family><FixingDays>0</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter></OvernightIndex>",
            "<OvernightIndex name=\"X\"><Family>On</Family><FixingDays>0</FixingDays><Currency>USD</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter></OvernightIndex>", "ibor:X", false),
        ("OvernightIndex", "FixingCalendar", "",
            "<OvernightIndex name=\"X\"><Family>On</Family><FixingDays>0</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter></OvernightIndex>",
            "<OvernightIndex name=\"X\"><Family>On</Family><FixingDays>0</FixingDays><Currency>EUR</Currency><FixingCalendar>UnitedKingdom.Settlement</FixingCalendar><DayCounter>Actual360</DayCounter></OvernightIndex>", "ibor:X", false),
        ("OvernightIndex", "DayCounter", "",
            "<OvernightIndex name=\"X\"><Family>On</Family><FixingDays>0</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter></OvernightIndex>",
            "<OvernightIndex name=\"X\"><Family>On</Family><FixingDays>0</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual365Fixed</DayCounter></OvernightIndex>", "ibor:X", false),
        ("OvernightIndex", "@base", "<OvernightIndex name=\"O1\"><Family>On</Family><FixingDays>0</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter></OvernightIndex><OvernightIndex name=\"O2\"><Family>On</Family><FixingDays>1</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter></OvernightIndex>",
            "<OvernightIndex name=\"X\" base=\"O1\"></OvernightIndex>",
            "<OvernightIndex name=\"X\" base=\"O2\"></OvernightIndex>", "ibor:X", false),
        ("SwapIndex", "Family", "",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>",
            "<SwapIndex name=\"X\"><Family>Other</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>", "swap:X@10Y", false),
        ("SwapIndex", "SettlementDays", "",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>1</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>", "swap:X@10Y", false),
        ("SwapIndex", "Currency", "",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>USD</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>", "swap:X@10Y", false),
        ("SwapIndex", "FixingCalendar", "",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>UnitedKingdom.Settlement</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>", "swap:X@10Y", false),
        ("SwapIndex", "FixedLegTenor", "",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>6M</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>", "swap:X@10Y", false),
        ("SwapIndex", "FixedLegConvention", "",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>Following</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>", "swap:X@10Y", false),
        ("SwapIndex", "FixedLegDayCounter", "",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Actual360</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>", "swap:X@10Y", false),
        ("SwapIndex", "IborIndex", "",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>EURLibor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>", "swap:X@10Y", false),
        ("SwapIndex", "IborIndexTenor", "",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>3M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>", "swap:X@10Y", false),
        ("SwapIndex", "ShortIborIndex", "",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>EURLibor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>", "swap:X@1Y", false),
        ("SwapIndex", "ShortIborIndexTenor", "",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>1M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>", "swap:X@1Y", false),
        ("SwapIndex", "ShortFixedLegTenor", "",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>",
            "<SwapIndex name=\"X\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>3M</ShortFixedLegTenor></SwapIndex>", "swap:X@1Y", false),
        ("SwapIndex", "@base", "<SwapIndex name=\"S1\"><Family>Sw</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex><SwapIndex name=\"S2\"><Family>Sw</Family><SettlementDays>1</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><IborIndex>Euribor</IborIndex><IborIndexTenor>6M</IborIndexTenor><ShortIborIndex>Euribor</ShortIborIndex><ShortIborIndexTenor>3M</ShortIborIndexTenor><ShortFixedLegTenor>6M</ShortFixedLegTenor></SwapIndex>",
            "<SwapIndex name=\"X\" base=\"S1\"></SwapIndex>",
            "<SwapIndex name=\"X\" base=\"S2\"></SwapIndex>", "swap:X@10Y", false),
        ("OvernightIndexedSwapIndex", "Family", "",
            "<OvernightIndexedSwapIndex name=\"X\"><Family>Ois</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><OvernightIndex>Estr</OvernightIndex><TelescopicValueDates>false</TelescopicValueDates><Averaging>Compound</Averaging></OvernightIndexedSwapIndex>",
            "<OvernightIndexedSwapIndex name=\"X\"><Family>Other</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><OvernightIndex>Estr</OvernightIndex><TelescopicValueDates>false</TelescopicValueDates><Averaging>Compound</Averaging></OvernightIndexedSwapIndex>", "ois:X", false),
        ("OvernightIndexedSwapIndex", "SettlementDays", "",
            "<OvernightIndexedSwapIndex name=\"X\"><Family>Ois</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><OvernightIndex>Estr</OvernightIndex><TelescopicValueDates>false</TelescopicValueDates><Averaging>Compound</Averaging></OvernightIndexedSwapIndex>",
            "<OvernightIndexedSwapIndex name=\"X\"><Family>Ois</Family><SettlementDays>1</SettlementDays><Currency>EUR</Currency><OvernightIndex>Estr</OvernightIndex><TelescopicValueDates>false</TelescopicValueDates><Averaging>Compound</Averaging></OvernightIndexedSwapIndex>", "ois:X", false),
        ("OvernightIndexedSwapIndex", "Currency", "",
            "<OvernightIndexedSwapIndex name=\"X\"><Family>Ois</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><OvernightIndex>Estr</OvernightIndex><TelescopicValueDates>false</TelescopicValueDates><Averaging>Compound</Averaging></OvernightIndexedSwapIndex>",
            "<OvernightIndexedSwapIndex name=\"X\"><Family>Ois</Family><SettlementDays>2</SettlementDays><Currency>USD</Currency><OvernightIndex>Estr</OvernightIndex><TelescopicValueDates>false</TelescopicValueDates><Averaging>Compound</Averaging></OvernightIndexedSwapIndex>", "ois:X", false),
        ("OvernightIndexedSwapIndex", "OvernightIndex", "",
            "<OvernightIndexedSwapIndex name=\"X\"><Family>Ois</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><OvernightIndex>Estr</OvernightIndex><TelescopicValueDates>false</TelescopicValueDates><Averaging>Compound</Averaging></OvernightIndexedSwapIndex>",
            "<OvernightIndexedSwapIndex name=\"X\"><Family>Ois</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><OvernightIndex>Eonia</OvernightIndex><TelescopicValueDates>false</TelescopicValueDates><Averaging>Compound</Averaging></OvernightIndexedSwapIndex>", "ois:X", false),
        ("OvernightIndexedSwapIndex", "TelescopicValueDates", "",
            "<OvernightIndexedSwapIndex name=\"X\"><Family>Ois</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><OvernightIndex>Estr</OvernightIndex><TelescopicValueDates>false</TelescopicValueDates><Averaging>Simple</Averaging></OvernightIndexedSwapIndex>",
            "<OvernightIndexedSwapIndex name=\"X\"><Family>Ois</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><OvernightIndex>Estr</OvernightIndex><TelescopicValueDates>true</TelescopicValueDates><Averaging>Simple</Averaging></OvernightIndexedSwapIndex>", "ois:X", false),
        ("OvernightIndexedSwapIndex", "Averaging", "",
            "<OvernightIndexedSwapIndex name=\"X\"><Family>Ois</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><OvernightIndex>Estr</OvernightIndex><TelescopicValueDates>false</TelescopicValueDates><Averaging>Compound</Averaging></OvernightIndexedSwapIndex>",
            "<OvernightIndexedSwapIndex name=\"X\"><Family>Ois</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><OvernightIndex>Estr</OvernightIndex><TelescopicValueDates>false</TelescopicValueDates><Averaging>Simple</Averaging></OvernightIndexedSwapIndex>", "ois:X", false),
        ("OvernightIndexedSwapIndex", "@base", "<OvernightIndexedSwapIndex name=\"W1\"><Family>Ois</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><OvernightIndex>Estr</OvernightIndex><TelescopicValueDates>false</TelescopicValueDates><Averaging>Compound</Averaging></OvernightIndexedSwapIndex><OvernightIndexedSwapIndex name=\"W2\"><Family>Ois</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><OvernightIndex>Estr</OvernightIndex><TelescopicValueDates>false</TelescopicValueDates><Averaging>Simple</Averaging></OvernightIndexedSwapIndex>",
            "<OvernightIndexedSwapIndex name=\"X\" base=\"W1\"></OvernightIndexedSwapIndex>",
            "<OvernightIndexedSwapIndex name=\"X\" base=\"W2\"></OvernightIndexedSwapIndex>", "ois:X", false),
        ("ZeroInflationIndex", "Family", "",
            "<ZeroInflationIndex name=\"X\"><Family>Z</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></ZeroInflationIndex>",
            "<ZeroInflationIndex name=\"X\"><Family>Other</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></ZeroInflationIndex>", "zero:X", false),
        ("ZeroInflationIndex", "Region", "",
            "<ZeroInflationIndex name=\"X\"><Family>Z</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></ZeroInflationIndex>",
            "<ZeroInflationIndex name=\"X\"><Family>Z</Family><Region>UKRegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></ZeroInflationIndex>", "zero:X", false),
        ("ZeroInflationIndex", "Revised", "",
            "<ZeroInflationIndex name=\"X\"><Family>Z</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></ZeroInflationIndex>",
            "<ZeroInflationIndex name=\"X\"><Family>Z</Family><Region>EURegion</Region><Revised>true</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></ZeroInflationIndex>", "zero:X", false),
        ("ZeroInflationIndex", "Frequency", "",
            "<ZeroInflationIndex name=\"X\"><Family>Z</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></ZeroInflationIndex>",
            "<ZeroInflationIndex name=\"X\"><Family>Z</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Quarterly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></ZeroInflationIndex>", "zero:X", false),
        ("ZeroInflationIndex", "AvailabilityLag", "",
            "<ZeroInflationIndex name=\"X\"><Family>Z</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></ZeroInflationIndex>",
            "<ZeroInflationIndex name=\"X\"><Family>Z</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>2M</AvailabilityLag><Currency>EUR</Currency></ZeroInflationIndex>", "zero:X", false),
        ("ZeroInflationIndex", "Currency", "",
            "<ZeroInflationIndex name=\"X\"><Family>Z</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></ZeroInflationIndex>",
            "<ZeroInflationIndex name=\"X\"><Family>Z</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>USD</Currency></ZeroInflationIndex>", "zero:X", false),
        ("ZeroInflationIndex", "@base", "<ZeroInflationIndex name=\"Z1\"><Family>Z</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></ZeroInflationIndex><ZeroInflationIndex name=\"Z2\"><Family>Z</Family><Region>EURegion</Region><Revised>true</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></ZeroInflationIndex>",
            "<ZeroInflationIndex name=\"X\" base=\"Z1\"></ZeroInflationIndex>",
            "<ZeroInflationIndex name=\"X\" base=\"Z2\"></ZeroInflationIndex>", "zero:X", false),
        ("YoYInflationIndex", "UnderlyingIndex", "",
            "<YoYInflationIndex name=\"X\"><UnderlyingIndex>EUHICP</UnderlyingIndex></YoYInflationIndex>",
            "<YoYInflationIndex name=\"X\"><UnderlyingIndex>UKRPI</UnderlyingIndex></YoYInflationIndex>", "yoy:X", false),
        ("YoYInflationIndex", "Family", "",
            "<YoYInflationIndex name=\"X\"><Family>Y</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></YoYInflationIndex>",
            "<YoYInflationIndex name=\"X\"><Family>Other</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></YoYInflationIndex>", "yoy:X", false),
        ("YoYInflationIndex", "Region", "",
            "<YoYInflationIndex name=\"X\"><Family>Y</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></YoYInflationIndex>",
            "<YoYInflationIndex name=\"X\"><Family>Y</Family><Region>UKRegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></YoYInflationIndex>", "yoy:X", false),
        ("YoYInflationIndex", "Revised", "",
            "<YoYInflationIndex name=\"X\"><Family>Y</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></YoYInflationIndex>",
            "<YoYInflationIndex name=\"X\"><Family>Y</Family><Region>EURegion</Region><Revised>true</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></YoYInflationIndex>", "yoy:X", false),
        ("YoYInflationIndex", "Frequency", "",
            "<YoYInflationIndex name=\"X\"><Family>Y</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></YoYInflationIndex>",
            "<YoYInflationIndex name=\"X\"><Family>Y</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Quarterly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></YoYInflationIndex>", "yoy:X", false),
        ("YoYInflationIndex", "AvailabilityLag", "",
            "<YoYInflationIndex name=\"X\"><Family>Y</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></YoYInflationIndex>",
            "<YoYInflationIndex name=\"X\"><Family>Y</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>2M</AvailabilityLag><Currency>EUR</Currency></YoYInflationIndex>", "yoy:X", false),
        ("YoYInflationIndex", "Currency", "",
            "<YoYInflationIndex name=\"X\"><Family>Y</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></YoYInflationIndex>",
            "<YoYInflationIndex name=\"X\"><Family>Y</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>USD</Currency></YoYInflationIndex>", "yoy:X", false),
        ("YoYInflationIndex", "@base", "<YoYInflationIndex name=\"Y1\"><Family>Y</Family><Region>EURegion</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></YoYInflationIndex><YoYInflationIndex name=\"Y2\"><Family>Y</Family><Region>EURegion</Region><Revised>true</Revised><Frequency>Monthly</Frequency><AvailabilityLag>1M</AvailabilityLag><Currency>EUR</Currency></YoYInflationIndex>",
            "<YoYInflationIndex name=\"X\" base=\"Y1\"></YoYInflationIndex>",
            "<YoYInflationIndex name=\"X\" base=\"Y2\"></YoYInflationIndex>", "yoy:X", false),
    ];

    public static TheoryData<string, string> Fields() => [.. Rows.Select(r => (r.Kind, r.Field))];

    [Fact]
    public void TheTableHoldsEveryFieldOfEveryKind()
    {
        var expected = new List<(string, string)>();
        // the instrument convention kinds are InstrumentFieldSensitivityTests', the experimental
        // CommodityFieldSensitivityTests'
        foreach (Type kind in typeof(ConventionRecord).Assembly.GetExportedTypes()
            .Where(t => t.IsSubclassOf(typeof(ConventionRecord)) && !t.Name.EndsWith("ConventionRecord", StringComparison.Ordinal)
                && !CommodityFieldSensitivityTests.Kinds.Contains(t)))
        {
            string element = kind.Name[..^"Record".Length];
            expected.AddRange(Properties(kind).Select(p => (element, p)));
        }

        expected.AddRange(Properties(typeof(CurrencyRounding)).Select(p => ("Rounding", p)));
        expected.AddRange(Properties(typeof(IborFamilyUnit)).Select(p => ("IborFamilyUnit", p)));

        Assert.Equal(expected.Order(), Rows.Select(r => (r.Kind, r.Field)).Order());

        // a record's fields as the schema names them, a list by its item; its name, which is no field, and its foreign
        // content apart
        static IEnumerable<string> Properties(Type kind) => kind.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name is not ("Name" or "Foreign" or "EqualityContract"))
            .Select(p => p.Name switch
            {
                "Base" => "@base",
                _ when p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(ImmutableArray<>) => p.Name[..^1],
                _ => p.Name,
            });
    }

    [Theory]
    [MemberData(nameof(Fields))]
    public void EachFieldChangesWhatItsKindResolvesToOrIsRefused(string kind, string field)
    {
        var row = Rows.Single(r => r.Kind == kind && r.Field == field);
        ConventionSet Set(string records) => ConventionsText.OnQuantLib(row.Lower).With(ConventionsText.Records(records));

        // a lookup the second value refuses is a change too
        string a = Outcome(() => Observe(Set(row.A), row.Lookup));
        if (row.Refused)
        {
            Assert.Throws<ArgumentException>(() => Set(row.B));
            return;
        }

        Assert.NotEqual(a, Outcome(() => Observe(Set(row.B), row.Lookup)));
    }

    private static readonly EvaluationContext ProbeContext = new() { EvaluationDate = new Date(15, Month.October, 2026) };

    // An averaged coupon on a curve, for a pricer's rate.
    private static readonly Lazy<Coupons.OvernightIndexedCoupon> ProbeCoupon = new(() =>
    {
        var curve = new TermStructures.FlatForward(ProbeContext.EvaluationDate, 0.03, new Actual365Fixed());
        var schedule = new Schedule(
            new Date(16, Month.October, 2026), new Date(16, Month.October, 2027), new Period(1, TimeUnit.Years), new TARGET(),
            BusinessDayConvention.Following, BusinessDayConvention.Following, DateGenerationRule.Forward, false, ProbeContext);
        return (Coupons.OvernightIndexedCoupon)new Coupons.OvernightLeg(schedule, new Estr(curve)).WithNotionals(1.0)
            .WithAveragingMethod(Coupons.RateAveragingType.Simple).ToLeg()[0];
    });

    // A lookup's name and tenor, the tenor given after "@" or the default.
    private static (string Name, Period Tenor) AtTenor(string name, string tenor) =>
        name.Split('@') is [string n, string t] ? (n, Utilities.PeriodParser.Parse(t)) : (name, Utilities.PeriodParser.Parse(tenor));

    private static string Outcome(Func<string> observe)
    {
        try
        {
            return observe();
        }
        catch (ArgumentException e)
        {
            return "refused: " + e.Message;
        }
    }

    private static string Currency(string fields) => $"<CurrencyPreset name=\"EUR\" base=\"EUR\">{fields}</CurrencyPreset>";

    // What a resolved value does: a calendar's holidays and weekend days over 2026, a day counter's counts over three
    // periods, a currency's data and a rounded amount.
    private static string Observe(ConventionSet set, string lookup)
    {
        string name = lookup[(lookup.IndexOf(':') + 1)..];
        Date from = new(1, Month.January, 2026), to = new(31, Month.December, 2026);
        switch (lookup[..lookup.IndexOf(':')])
        {
            case "calendar":
                Calendar c = set.Calendar(name);
                return $"{c.Name}: {string.Join(",", c.HolidayList(from, to, true))}";
            case "daycounter":
                DayCounter d = set.DayCounter(name);
                return $"{d.Name}: {d.DayCount(from, to)} {d.YearFraction(from, to)} {d.DayCount(new Date(1, Month.October, 2026), new Date(1, Month.November, 2026))}"
                    + $" {d.YearFraction(new Date(31, Month.January, 2026), new Date(28, Month.February, 2026))}";
            case "region":
                Region r = set.Region(name);
                return $"{r.GetType().Name} {r.Name} {r.Code}";
            case "ibor":
                IborIndex i = set.IborIndex(name);
                return $"{IndexFields.Of(i)} {string.Join(",", IndexFields.Dates(i, 2026))}" + (i is OvernightIndex o ? string.Join(",", IndexFields.Accruals(o)) : "");
            case "family":
            {
                (string family, Period tenor) = AtTenor(name, "6M");
                IborIndex f = set.IborIndex(family, tenor);
                return $"{IndexFields.Of(f)} {string.Join(",", IndexFields.Dates(f, 2026))}";
            }

            case "swap":
            {
                (string family, Period tenor) = AtTenor(name, "10Y");
                SwapIndex s = set.SwapIndex(family, tenor);
                return $"{IndexFields.Of(s)} {s.DayCounter.Name} {string.Join(",", IndexFields.Dates(s, 2026))}";
            }

            case "ois":
                OvernightIndexedSwapIndex w = set.OvernightIndexedSwapIndex(name, new Period(10, TimeUnit.Years));
                // the swap index's own fields, and its underlying swap's averaging and overnight coupons' dates
                Instruments.OvernightIndexedSwap swap = w.UnderlyingSwap(new Date(16, Month.October, 2026), new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) });
                Coupons.OvernightIndexedCoupon[] coupons = [.. swap.OvernightLeg.OfType<Coupons.OvernightIndexedCoupon>()];
                // the telescopic flag selects an averaging pricer's approximation, which shows in a priced rate
                return $"{IndexFields.Of(w)} {w.OvernightIndex.Name} {swap.AveragingMethod} "
                    + string.Join(",", coupons.Take(1).Select(c => BitConverter.DoubleToInt64Bits(c.Pricer!.SwapletRate(ProbeCoupon.Value, ProbeContext))))
                    + string.Join(",", coupons.SelectMany(c => c.ValueDates()).Select(d => d.SerialNumber));
            case "zero":
                ZeroInflationIndex z = set.ZeroInflationIndex(name);
                return $"{z.Name} {z.Region.Name} {z.Revised} {z.Frequency} {z.AvailabilityLag} {z.Currency.Code}";
            case "yoy":
                YoYInflationIndex y = set.YoYInflationIndex(name);
                return $"{y.Name} {y.Region.Name} {y.Revised} {y.Frequency} {y.AvailabilityLag} {y.Currency.Code} {y.Ratio} {y.UnderlyingIndex?.Name}";
            default:
                Currency x = set.Currency(name);
                return FormattableString.Invariant(
                    $"{x.Name} {x.NumericCode} {x.Symbol} {x.FractionSymbol} {x.FractionsPerUnit} {x.Rounding.Evaluate(1.23456789)} {x.Rounding.Evaluate(1.2369)} {x.Rounding.Evaluate(1.2341)} {x.TriangulationCurrency?.Code}");
        }
    }
}
