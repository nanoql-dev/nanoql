// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text;
using NanoQL.Extensions.Conventions;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// Field sensitivity on the instrument convention kinds: every field of every kind, given two values, changes what each
/// of its routes builds, or the route refuses the second value, naming the route, the kind, the record and the field.
/// Each kind's base record gives a value to every field its routes take, on a test calendar, TARGET with a holiday on
/// every 30th, so that each schedule option shows in the dates; a second, with a holiday on every 29th, pays where the
/// first moves the dates.
/// </summary>
public class InstrumentFieldSensitivityTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(26, Month.June, 2026) };
    private static readonly FlatForward Curve = new(new Date(26, Month.June, 2026), 0.03, new Actual365Fixed());
    private static readonly FlatForward Other = new(new Date(26, Month.June, 2026), 0.02, new Actual365Fixed());

    // a spot at a month's last business day on TARGET, where an end-of-month flag shows
    private static readonly EvaluationContext MonthEnd = new() { EvaluationDate = new Date(29, Month.July, 2026) };
    private static readonly FlatHazardRate Hazard = new(new Date(26, Month.June, 2026), 0.02, new Actual365Fixed());
    private static readonly string Calendars = Holidays("Desk", 30) + Holidays("Desk29", 29);
    private static readonly Date[] Coupons = [new(30, Month.September, 2031)];

    private enum Expect
    {
        // per route: the route's effect, or its refusal where the route can't take the field
        ByRoute,

        // the set refuses the second value when it is layered on
        RefusedAtWith,

        // the second value builds something else or is refused: a field that chooses the route's instrument
        Differs,
    }

    public static TheoryData<string, string> Rows() =>
        [.. Cases.SelectMany(c => c.Rows.Select(r => (c.Name, r.Field)).Append((c.Name, "@base")))];

    [Fact]
    public void TheTableHoldsEveryFieldOfEveryInstrumentKind()
    {
        var expected = new HashSet<(string, string)>();
        foreach (Type kind in typeof(ConventionRecord).Assembly.GetExportedTypes()
            .Where(t => t.IsSubclassOf(typeof(ConventionRecord)) && t.Name.EndsWith("ConventionRecord", StringComparison.Ordinal)))
        {
            string element = kind.Name[..^"Record".Length];
            expected.Add((element, "@base"));
            foreach (PropertyInfo p in Properties(kind))
            {
                expected.Add((element, p.Name));
            }
        }

        var covered = new HashSet<(string, string)>(Cases.SelectMany(c => c.Rows.Select(r => (c.Element, r.Field)).Append((c.Element, "@base"))));
        Assert.Empty(expected.Except(covered));
        Assert.Empty(covered.Except(expected));
        Assert.Equal(15, Cases.Select(c => c.Element).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void EachFieldChangesWhatEachRouteBuildsOrTheRouteRefusesIt(string kindCase, string field)
    {
        Case c = Cases.Single(x => x.Name == kindCase);
        if (field == "@base")
        {
            // a base chain: the same record on two bases, which differ in the first field every route takes
            Row first = c.Rows.First(r => r.Expect == Expect.ByRoute && c.Routes.All(route => !route.Untaken.Contains(r.Field)));
            string lower = Calendars + c.Record("X1", c.Base) + c.Record("X2", c.With(first));
            foreach (Route route in c.Routes)
            {
                string a = Outcome(() => ConventionsText.OnQuantLib(lower + $"<{c.Element} name=\"X\" base=\"X1\" />"), route);
                string b = Outcome(() => ConventionsText.OnQuantLib(lower + $"<{c.Element} name=\"X\" base=\"X2\" />"), route);
                Assert.False(a.StartsWith("refused", StringComparison.Ordinal), $"{route.Name}: {a}");
                Assert.NotEqual(a, b);
            }

            return;
        }

        Row row = c.Rows.Single(r => r.Field == field);
        ConventionSet A() => ConventionsText.OnQuantLib(Calendars + c.Record("X", c.WithA(row)));
        ConventionSet B() => ConventionsText.OnQuantLib(Calendars + c.Record("X", c.With(row)));
        if (row.Expect == Expect.RefusedAtWith)
        {
            Assert.Contains(field, Assert.Throws<ArgumentException>(B).Message, StringComparison.Ordinal);
            return;
        }

        foreach (Route route in c.Routes)
        {
            string a = Outcome(A, route), b = Outcome(B, route);
            Assert.False(a.StartsWith("refused", StringComparison.Ordinal), $"{route.Name}: the base refused: {a}");
            if (row.Expect == Expect.Differs)
            {
                Assert.NotEqual(a, b);
            }
            else if (route.Untaken.Contains(field))
            {
                Assert.StartsWith($"refused: {route.Name} can't take the {c.Element} X's ", b, StringComparison.Ordinal);
                Assert.Contains(field, b, StringComparison.Ordinal);
            }
            else
            {
                Assert.False(b.StartsWith("refused", StringComparison.Ordinal), $"{route.Name}: {b}");
                Assert.NotEqual(a, b);
            }
        }
    }

    [Theory]
    [InlineData("Comparison", "<Comparison>AtLeast</Comparison><SwapTenor>2Y</SwapTenor><FixedLegTenor>6M</FixedLegTenor>")]
    [InlineData("SwapTenor", "<Comparison>AtMost</Comparison><SwapTenor>6M</SwapTenor><FixedLegTenor>6M</FixedLegTenor>")]
    [InlineData("FixedLegTenor", "<Comparison>AtMost</Comparison><SwapTenor>2Y</SwapTenor><FixedLegTenor>3M</FixedLegTenor>")]
    public void EachFieldOfACurrencysThresholdChangesTheSwapsItsBuilderBuilds(string field, string changed)
    {
        // a 1Y EUR swap: at most 2Y, the threshold's 6M fixed leg; another threshold leaves it 1Y or makes it 3M
        static string Swap(string threshold)
        {
            ConventionSet set = ConventionsText.OnQuantLib(
                $"<SwapConvention name=\"MakeVanillaSwap.EUR\" base=\"MakeVanillaSwap.EUR\"><FixedLegTenor>1Y</FixedLegTenor><FixedLegTenorThreshold>{threshold}</FixedLegTenorThreshold></SwapConvention>");
            VanillaSwap swap = new MakeVanillaSwap(new Period(1, TimeUnit.Years), set.IborIndex("Euribor6M", Curve)).WithFixedRate(0.03).ToVanillaSwap(Context);
            return Fingerprint.Of(swap.FixedLeg, Context);
        }

        Assert.NotNull(typeof(SwapTenorThreshold).GetProperty(field));
        Assert.NotEqual(Swap("<Comparison>AtMost</Comparison><SwapTenor>2Y</SwapTenor><FixedLegTenor>6M</FixedLegTenor>"), Swap(changed));
    }

    private static string Outcome(Func<ConventionSet> set, Route route)
    {
        try
        {
            return route.Observe(set());
        }
        catch (ArgumentException e)
        {
            return "refused: " + e.Message;
        }
        catch (NanoQLException e)
        {
            return "failed: " + e.Message;
        }
    }

    private static string Holidays(string name, int day)
    {
        var sb = new StringBuilder($"<Calendar name=\"{name}\" base=\"TARGET\">");
        for (int year = 2026; year <= 2032; ++year)
        {
            for (int month = 1; month <= 12; ++month)
            {
                if (day <= DateTime.DaysInMonth(year, month))
                {
                    sb.Append(FormattableString.Invariant($"<AddedHoliday>{year:D4}-{month:D2}-{day:D2}</AddedHoliday>"));
                }
            }
        }

        return sb.Append("</Calendar>").ToString();
    }

    private static IEnumerable<PropertyInfo> Properties(Type kind) =>
        kind.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(p => p.Name != "EqualityContract");

    private static string Seen(params object?[] values) => Fingerprint.Of(values, Context);

    private static Route R(string name, Func<ConventionSet, string> observe, params string[] untaken) => new(name, observe, untaken);

    private static Row Of(string field, string? a, string b, Expect expect = Expect.ByRoute, params string[] without) => new(field, a, b, expect, without);

    private sealed record Route(string Name, Func<ConventionSet, string> Observe, string[] Untaken);

    // A field's two values: A null for the base's, "" for none; B the other; Without the base's fields left out of both.
    private sealed record Row(string Field, string? A, string B, Expect Expect, string[] Without);

    private sealed record Case(string Name, string Element, (string Field, string Value)[] Base, Route[] Routes, Row[] Rows)
    {
        public (string Field, string Value)[] With(Row r) => Set(r, r.B);

        public (string Field, string Value)[] WithA(Row r) => r.A is null ? Set(r, null) : Set(r, r.A);

        public string Record(string name, (string Field, string Value)[] fields)
        {
            Type kind = typeof(ConventionRecord).Assembly.GetType($"NanoQL.Extensions.Conventions.{Element}Record")!;
            var sb = new StringBuilder($"<{Element} name=\"{name}\">");
            foreach (PropertyInfo p in Properties(kind))
            {
                if (fields.FirstOrDefault(f => f.Field == p.Name) is { Value: { Length: > 0 } value })
                {
                    sb.Append($"<{p.Name}>{value}</{p.Name}>");
                }
            }

            return sb.Append($"</{Element}>").ToString();
        }

        private (string Field, string Value)[] Set(Row r, string? value)
        {
            var fields = Base.Where(f => !r.Without.Contains(f.Field) && f.Field != r.Field).ToList();
            string? kept = Base.FirstOrDefault(f => f.Field == r.Field).Value;
            if ((value ?? kept) is string v)
            {
                fields.Add((r.Field, v));
            }

            return [.. fields];
        }
    }

    private static readonly Case[] Cases =
    [
        new("Schedule", "ScheduleConvention",
            [("Tenor", "3M"), ("Calendar", "Desk"), ("Convention", "Preceding"), ("TerminationDateConvention", "Preceding"), ("Rule", "Backward"), ("EndOfMonth", "false")],
            [R("MakeSchedule", s => Seen(s.Schedule("X").Builder().From(new Date(15, Month.June, 2026)).To(new Date(30, Month.September, 2028)).ToSchedule()))],
            [Of("Tenor", null, "6M"), Of("Frequency", "Quarterly", "Monthly", Expect.ByRoute, "Tenor"), Of("Calendar", null, "TARGET"), Of("Convention", null, "Following"),
                Of("TerminationDateConvention", null, "Following"), Of("Rule", null, "Forward"), Of("EndOfMonth", null, "true")]),

        new("Swap", "SwapConvention",
            [("SettlementDays", "2"), ("SettlementCalendar", "Desk"), ("FixedLegTenor", "6M"), ("FixedLegCalendar", "Desk"), ("FixedLegConvention", "Preceding"),
                ("FixedLegTerminationDateConvention", "Preceding"), ("FixedLegRule", "Backward"), ("FixedLegEndOfMonth", "false"), ("FixedLegDayCounter", "Actual360"),
                ("Index", "Euribor"), ("IndexTenor", "6M"), ("FloatingLegCalendar", "Desk"), ("FloatingLegConvention", "Unadjusted"),
                ("FloatingLegTerminationDateConvention", "Unadjusted"), ("FloatingLegRule", "Backward"), ("FloatingLegEndOfMonth", "false")],
            [
                R("MakeVanillaSwap", s => Seen(s.Swap("X").Builder(new Period(2, TimeUnit.Years), Curve).WithTerminationDate(new Date(30, Month.September, 2028)).WithFixedRate(0.03).ToVanillaSwap(Context))),
                R("SwapRateHelper", s => Helper(s.Swap("X").RateHelper(0.03, new Period(2, TimeUnit.Years), Context, forwardingCurve: Curve)),
                    "SettlementCalendar", "FixedLegCalendar", "FixedLegConvention", "FixedLegTerminationDateConvention", "FixedLegRule", "FixedLegEndOfMonth", "FloatingLegTenor",
                    "FloatingLegCalendar", "FloatingLegConvention", "FloatingLegTerminationDateConvention", "FloatingLegRule", "FloatingLegEndOfMonth", "FloatingLegDayCounter",
                    "PaymentConvention"),
            ],
            [Of("SettlementDays", null, "0"), Of("SettlementCalendar", null, "TARGET"), Of("FixedLegTenor", null, "1Y"),
                Of("FixedLegTenorThreshold", "", "<Comparison>AtMost</Comparison><SwapTenor>1Y</SwapTenor><FixedLegTenor>1Y</FixedLegTenor>", Expect.RefusedAtWith),
                Of("FixedLegCalendar", null, "TARGET"), Of("FixedLegConvention", null, "Following"), Of("FixedLegTerminationDateConvention", null, "Following"),
                Of("FixedLegRule", null, "Forward"), Of("FixedLegEndOfMonth", null, "true"), Of("FixedLegDayCounter", null, "Actual365Fixed"), Of("Index", null, "Euribor365"),
                Of("IndexTenor", null, "3M"), Of("FloatingLegTenor", "", "3M"), Of("FloatingLegCalendar", null, "TARGET"), Of("FloatingLegConvention", null, "Following"),
                Of("FloatingLegTerminationDateConvention", null, "Following"), Of("FloatingLegRule", null, "Forward"), Of("FloatingLegEndOfMonth", null, "true"),
                Of("FloatingLegDayCounter", "", "Actual365Fixed"), Of("PaymentConvention", "", "Following")]),

        new("Ois", "OisConvention",
            [("Index", "Estr"), ("SettlementDays", "3"), ("PaymentLag", "0"), ("PaymentConvention", "Following"), ("PaymentFrequency", "Semiannual"), ("PaymentCalendar", "Desk29"),
                ("FixedLegFrequency", "Semiannual"), ("FixedLegCalendar", "Desk"), ("FixedLegConvention", "Preceding"), ("FixedLegTerminationDateConvention", "Preceding"),
                ("FixedLegRule", "Backward"), ("FixedLegEndOfMonth", "false"), ("FixedLegDayCounter", "Actual360"), ("OvernightLegCalendar", "Desk"),
                ("OvernightLegConvention", "Preceding"), ("OvernightLegTerminationDateConvention", "Preceding"), ("OvernightLegRule", "Backward"),
                ("OvernightLegEndOfMonth", "false"), ("Averaging", "Compound"), ("LookbackDays", "2"), ("LockoutDays", "0"), ("ObservationShift", "false")],
            [
                R("MakeOIS", s => Seen(s.Ois("X").Builder(new Period(2, TimeUnit.Years), Curve).WithTerminationDate(new Date(30, Month.September, 2028)).WithFixedRate(0.03).ToOvernightIndexedSwap(Context))),
                R("OISRateHelper", s => Helper(s.Ois("X").RateHelper(0.03, new Period(2, TimeUnit.Years), Context, forwardingCurve: Curve)),
                    "SettlementCalendar", "FixedLegConvention", "FixedLegTerminationDateConvention", "OvernightLegConvention", "OvernightLegTerminationDateConvention",
                    "FixedLegRule", "OvernightLegRule", "FixedLegEndOfMonth", "OvernightLegEndOfMonth"),
            ],
            [Of("Index", null, "Sonia"), Of("SettlementDays", null, "0"), Of("SettlementCalendar", "", "TARGET"), Of("PaymentLag", null, "2"),
                Of("PaymentConvention", null, "Unadjusted"), Of("PaymentFrequency", null, "Quarterly"), Of("PaymentCalendar", null, "TARGET"),
                Of("FixedLegFrequency", null, "Annual"), Of("FixedLegCalendar", null, "TARGET"), Of("FixedLegConvention", null, "Following"),
                Of("FixedLegTerminationDateConvention", null, "Following"), Of("FixedLegRule", null, "Forward"), Of("FixedLegEndOfMonth", null, "true"),
                Of("FixedLegDayCounter", null, "Actual365Fixed"), Of("OvernightLegCalendar", null, "TARGET"), Of("OvernightLegConvention", null, "Following"),
                Of("OvernightLegTerminationDateConvention", null, "Following"), Of("OvernightLegRule", null, "Forward"), Of("OvernightLegEndOfMonth", null, "true"),
                Of("Averaging", null, "Simple", Expect.Differs), Of("LookbackDays", null, "3"), Of("LockoutDays", null, "2"), Of("ObservationShift", null, "true")]),

        new("OisAveraged", "OisConvention",
            [("Index", "Estr"), ("PaymentFrequency", "Semiannual"), ("OvernightLegCalendar", "Desk"), ("TelescopicValueDates", "false"), ("Averaging", "Simple")],
            [
                R("MakeOIS", s => Seen(s.Ois("X").Builder(new Period(2, TimeUnit.Years), Curve).WithTerminationDate(new Date(30, Month.September, 2028)).WithFixedRate(0.03).ToOvernightIndexedSwap(Context))),
                R("OISRateHelper", s => Helper(s.Ois("X").RateHelper(0.03, new Period(2, TimeUnit.Years), Context, forwardingCurve: Curve))),
            ],
            [Of("TelescopicValueDates", null, "true"), Of("Averaging", null, "Compound")]),

        new("CdsBuilder", "CdsConvention",
            [("Calendar", "WeekendsOnly"), ("CouponFrequency", "Quarterly"), ("PaymentConvention", "Following"), ("Rule", "CDS"), ("DayCounter", "Actual360"),
                ("LastPeriodDayCounter", "Actual360.IncludeLastDay"), ("SettlesAccrual", "true"), ("PaysAtDefaultTime", "true"), ("RebatesAccrual", "true"), ("CashSettlementDays", "3")],
            [R("MakeCreditDefaultSwap", s => Seen(s.Cds("X").Builder(new Period(5, TimeUnit.Years), 0.01).ToCreditDefaultSwap(Context)), "SettlementDays", "PricingModel", "Calendar")],
            [Of("SettlementDays", "", "1"), Of("Calendar", null, "TARGET"), Of("CouponFrequency", null, "Semiannual"), Of("PaymentConvention", null, "Preceding"),
                Of("Rule", null, "OldCDS"), Of("DayCounter", null, "Actual365Fixed"), Of("LastPeriodDayCounter", null, "Actual360"), Of("SettlesAccrual", null, "false"),
                Of("PaysAtDefaultTime", null, "false"), Of("RebatesAccrual", null, "false"), Of("CashSettlementDays", null, "1"), Of("PricingModel", "", "ISDA")]),

        new("CdsHelpers", "CdsConvention",
            [("SettlementDays", "1"), ("Calendar", "Desk"), ("CouponFrequency", "Quarterly"), ("PaymentConvention", "Following"), ("Rule", "CDS"), ("DayCounter", "Actual360"),
                ("LastPeriodDayCounter", "Actual360.IncludeLastDay"), ("SettlesAccrual", "true"), ("PaysAtDefaultTime", "true"), ("RebatesAccrual", "true"), ("PricingModel", "Midpoint")],
            [
                R("SpreadCdsHelper", s => Seen(s.Cds("X").SpreadHelper(0.01, new Period(5, TimeUnit.Years), 0.4, Curve, Context).SetTermStructure(Hazard)), "CashSettlementDays"),
                R("UpfrontCdsHelper", s => Seen(s.Cds("X").UpfrontHelper(0.02, 0.01, new Period(5, TimeUnit.Years), 0.4, Curve, Context).SetTermStructure(Hazard))),
            ],
            [Of("SettlementDays", null, "0"), Of("Calendar", null, "TARGET"), Of("CouponFrequency", null, "Semiannual"), Of("PaymentConvention", null, "Preceding"),
                Of("Rule", null, "OldCDS"), Of("DayCounter", null, "Actual365Fixed"), Of("LastPeriodDayCounter", null, "Actual360"), Of("SettlesAccrual", null, "false"),
                Of("PaysAtDefaultTime", null, "false"), Of("RebatesAccrual", null, "false"), Of("CashSettlementDays", "", "1"), Of("PricingModel", null, "ISDA")]),

        new("ZeroCouponInflationSwap", "ZeroCouponInflationSwapConvention",
            [("Index", "EUHICP"), ("ObservationLag", "3M"), ("Interpolation", "Flat"), ("Calendar", "Desk"), ("Convention", "Preceding"), ("DayCounter", "Actual360")],
            [
                R("ZeroCouponInflationSwap", s => Seen(s.ZeroCouponInflationSwap("X").Swap(SwapType.Payer, 1e6, new Date(30, Month.June, 2026), new Date(30, Month.September, 2031), 0.02))),
                R("ZeroCouponInflationSwapHelper", s => Seen(s.ZeroCouponInflationSwap("X").RateHelper(0.02, new Date(30, Month.September, 2031), Context)),
                    "InflationCalendar", "InflationConvention", "AdjustObservationDates"),
            ],
            [Of("Index", null, "UKRPI"), Of("ObservationLag", null, "2M"), Of("Interpolation", null, "Linear"), Of("Calendar", null, "TARGET"),
                Of("Convention", null, "Following"), Of("DayCounter", null, "Actual365Fixed"), Of("InflationCalendar", "", "TARGET"),
                Of("InflationConvention", "", "Following"), Of("AdjustObservationDates", "", "true")]),

        new("YearOnYearInflationSwap", "YearOnYearInflationSwapConvention",
            [("Index", "YYEUHICP"), ("ObservationLag", "3M"), ("Interpolation", "Flat"), ("Calendar", "Desk"), ("PaymentConvention", "Preceding"), ("DayCounter", "Actual360")],
            [R("YearOnYearInflationSwapHelper", s => Seen(s.YearOnYearInflationSwap("X").RateHelper(0.02, new Date(30, Month.September, 2031), Curve, Context)))],
            [Of("Index", null, "YYUKRPI"), Of("ObservationLag", null, "2M"), Of("Interpolation", null, "Linear"), Of("Calendar", null, "TARGET"),
                Of("PaymentConvention", null, "Following"), Of("DayCounter", null, "Actual365Fixed")]),

        new("CrossCurrencyBasisSwap", "CrossCurrencyBasisSwapConvention",
            [("FixingDays", "2"), ("Calendar", "TARGET"), ("Convention", "Following"), ("EndOfMonth", "false"), ("BaseCurrencyIndex", "Euribor"), ("BaseCurrencyIndexTenor", "3M"),
                ("QuoteCurrencyIndex", "USDLibor"), ("QuoteCurrencyIndexTenor", "3M"), ("CollateralOnBaseCurrency", "false"), ("BasisOnBaseCurrencyLeg", "true"),
                ("PaymentFrequency", "Quarterly"), ("QuoteCurrencyPaymentFrequency", "Quarterly"), ("PaymentLag", "1"), ("PaymentLagOnNotionalExchanges", "false"),
                ("MarkToMarket", "false")],
            [R("ConstNotionalCrossCurrencyBasisSwapRateHelper", s => Helper(s.CrossCurrencyBasisSwap("X").RateHelper(0.001, new Period(3, TimeUnit.Years), Other, Context, Other, Curve), Context),
                "BaseCurrencyLegResettable", "FxResetFixingDays", "FxResetFixingCalendar")],
            [Of("FixingDays", null, "0"), Of("Calendar", null, "Desk"), Of("Convention", null, "Preceding"), Of("EndOfMonth", null, "true"),
                Of("BaseCurrencyIndex", null, "Euribor365"), Of("BaseCurrencyIndexTenor", null, "6M"), Of("QuoteCurrencyIndex", null, "GBPLibor"),
                Of("QuoteCurrencyIndexTenor", null, "6M"), Of("CollateralOnBaseCurrency", null, "true"), Of("BasisOnBaseCurrencyLeg", null, "false"),
                Of("PaymentFrequency", null, "Semiannual"), Of("QuoteCurrencyPaymentFrequency", null, "Semiannual"), Of("PaymentLag", null, "2"),
                Of("IndexedCoupons", "", "true"), Of("PaymentLagOnNotionalExchanges", null, "true"), Of("MarkToMarket", null, "true", Expect.Differs),
                Of("BaseCurrencyLegResettable", "", "true"), Of("FxResetFixingDays", "", "1"), Of("FxResetFixingCalendar", "", "TARGET")]),

        new("CrossCurrencyBasisSwapMarkedToMarket", "CrossCurrencyBasisSwapConvention",
            [("FixingDays", "2"), ("Calendar", "TARGET"), ("Convention", "Following"), ("EndOfMonth", "false"), ("BaseCurrencyIndex", "Euribor"), ("BaseCurrencyIndexTenor", "3M"),
                ("QuoteCurrencyIndex", "USDLibor"), ("QuoteCurrencyIndexTenor", "3M"), ("CollateralOnBaseCurrency", "false"), ("BasisOnBaseCurrencyLeg", "true"),
                ("PaymentFrequency", "Quarterly"), ("QuoteCurrencyPaymentFrequency", "Quarterly"), ("PaymentLag", "1"), ("MarkToMarket", "true"),
                ("BaseCurrencyLegResettable", "true"), ("FxResetFixingDays", "2"), ("FxResetFixingCalendar", "Desk")],
            [R("MtMCrossCurrencyBasisSwapRateHelper", s => Helper(s.CrossCurrencyBasisSwap("X").RateHelper(0.001, new Period(3, TimeUnit.Years), Other, Context, Other, Curve), Context),
                "PaymentLagOnNotionalExchanges")],
            [Of("MarkToMarket", null, "false", Expect.Differs), Of("BaseCurrencyLegResettable", null, "false"), Of("FxResetFixingDays", null, "0"),
                Of("FxResetFixingCalendar", null, "TARGET"), Of("PaymentLagOnNotionalExchanges", "", "true")]),

        new("CrossCurrencySwap", "CrossCurrencySwapConvention",
            [("FixingDays", "2"), ("Calendar", "TARGET"), ("Convention", "Following"), ("EndOfMonth", "false"), ("FixedFrequency", "Annual"), ("FixedDayCounter", "Actual360"),
                ("FloatingIndex", "Euribor"), ("FloatingIndexTenor", "3M"), ("CollateralOnFixedLeg", "false"), ("PaymentLag", "1"), ("FloatingPaymentFrequency", "Quarterly")],
            [R("ConstNotionalCrossCurrencySwapRateHelper", s => Helper(s.CrossCurrencySwap("X").RateHelper(0.02, new Period(3, TimeUnit.Years), Other, Context, Curve), Context))],
            [Of("FixingDays", null, "0"), Of("Calendar", null, "Desk"), Of("Convention", null, "Preceding"), Of("EndOfMonth", null, "true"),
                Of("FixedFrequency", null, "Semiannual"), Of("FixedDayCounter", null, "Actual365Fixed"), Of("FloatingIndex", null, "Euribor365"),
                Of("FloatingIndexTenor", null, "6M"), Of("CollateralOnFixedLeg", null, "true"), Of("PaymentLag", null, "2"),
                Of("FloatingPaymentFrequency", null, "Semiannual"), Of("IndexedCoupons", "", "true")]),

        new("FxSwap", "FxSwapConvention",
            [("FixingDays", "2"), ("Calendar", "TARGET"), ("Convention", "Following"), ("EndOfMonth", "false"), ("CollateralOnBaseCurrency", "false"), ("TradingCalendar", "UnitedStates.NYSE")],
            [R("FxSwapRateHelper", s => Seen(
                Helper(s.FxSwap("X").RateHelper(0.001, 1.1, new Period(3, TimeUnit.Months), Curve, MonthEnd), MonthEnd),
                Helper(s.FxSwap("X").RateHelper(0.001, 1.1, new Period(2, TimeUnit.Months), Curve, MonthEnd), MonthEnd)), "BaseCurrency", "QuoteCurrency", "PaymentCalendar")],
            [Of("BaseCurrency", "", "EUR"), Of("QuoteCurrency", "", "USD"), Of("FixingDays", null, "0"), Of("Calendar", null, "Desk"), Of("Convention", null, "Preceding"),
                Of("EndOfMonth", null, "true"), Of("CollateralOnBaseCurrency", null, "true"), Of("TradingCalendar", null, "TARGET"), Of("PaymentCalendar", "", "TARGET")]),

        new("FxForward", "FxSwapConvention",
            [("BaseCurrency", "EUR"), ("QuoteCurrency", "USD"), ("FixingDays", "2"), ("PaymentCalendar", "Desk")],
            [R("FxForward", s => Seen(s.FxSwap("X").Forward(1e6, 1.1e6, new Date(30, Month.September, 2026), true)), "Calendar", "Convention", "EndOfMonth", "CollateralOnBaseCurrency", "TradingCalendar")],
            [Of("BaseCurrency", null, "GBP"), Of("QuoteCurrency", null, "JPY"), Of("FixingDays", null, "0"), Of("PaymentCalendar", null, "TARGET"),
                Of("Calendar", "", "TARGET"), Of("Convention", "", "Following"), Of("EndOfMonth", "", "true"), Of("CollateralOnBaseCurrency", "", "true"),
                Of("TradingCalendar", "", "TARGET")]),

        new("CapFloor", "CapFloorConvention",
            [("Index", "Euribor"), ("IndexTenor", "6M"), ("Calendar", "Desk"), ("Convention", "Preceding"), ("TerminationDateConvention", "Preceding"), ("Rule", "Backward"),
                ("EndOfMonth", "false"), ("DayCounter", "Actual360")],
            [R("MakeCapFloor", s => Seen(s.CapFloor("X").Builder(CapFloorType.Cap, new Period(27, TimeUnit.Months), Curve).WithStrike(0.03).ToCapFloor(Context)), "FirstCapletExcluded")],
            [Of("Index", null, "Euribor365"), Of("IndexTenor", null, "3M"), Of("Tenor", "", "3M"), Of("Calendar", null, "TARGET"), Of("Convention", null, "Following"),
                Of("TerminationDateConvention", null, "Following"), Of("Rule", null, "Forward"), Of("EndOfMonth", null, "true"), Of("DayCounter", null, "Actual365Fixed"),
                Of("FirstCapletExcluded", "", "false")]),

        new("Swaption", "SwaptionConvention",
            [("SwapIndex", "EuriborSwapIsdaFixA"), ("SettlementType", "Physical"), ("SettlementMethod", "PhysicalOTC"), ("OptionConvention", "Preceding"), ("ExerciseCalendar", "Desk")],
            [R("MakeSwaption", s => Seen(s.Swaption("X").Builder(new Period(6, TimeUnit.Months), 0.03, new Period(5, TimeUnit.Years), Curve)
                .ToSwaption(new EvaluationContext { EvaluationDate = new Date(30, Month.March, 2026) })))],
            [Of("SwapIndex", null, "EurLiborSwapIsdaFixA"), Of("SettlementType", null, "Cash", Expect.Differs), Of("SettlementMethod", null, "PhysicalCleared"),
                Of("OptionConvention", null, "Following"), Of("ExerciseCalendar", null, "TARGET")]),

        new("Cms", "CmsConvention",
            [("SwapIndex", "EuriborSwapIsdaFixA"), ("SwapIndexTenor", "10Y"), ("CmsLegTenor", "3M"), ("CmsLegCalendar", "Desk"), ("CmsLegConvention", "Preceding"),
                ("CmsLegTerminationDateConvention", "Preceding"), ("CmsLegRule", "Backward"), ("CmsLegEndOfMonth", "false"), ("CmsLegDayCounter", "Actual360"),
                ("FloatingLegIndex", "Euribor"), ("FloatingLegIndexTenor", "3M"), ("FloatingLegCalendar", "Desk"), ("FloatingLegConvention", "Preceding"),
                ("FloatingLegTerminationDateConvention", "Preceding"), ("FloatingLegRule", "Backward"), ("FloatingLegEndOfMonth", "false"), ("FloatingLegDayCounter", "Actual360")],
            [R("MakeCms", s => Seen(s.Cms("X").Builder(new Period(29, TimeUnit.Months), Curve).WithEffectiveDate(new Date(30, Month.April, 2026)).ToSwap(Context)))],
            [Of("SwapIndex", null, "EurLiborSwapIsdaFixA"), Of("SwapIndexTenor", null, "5Y"), Of("CmsLegTenor", null, "6M"), Of("CmsLegCalendar", null, "TARGET"),
                Of("CmsLegConvention", null, "Following"), Of("CmsLegTerminationDateConvention", null, "Following"), Of("CmsLegRule", null, "Forward"),
                Of("CmsLegEndOfMonth", null, "true"), Of("CmsLegDayCounter", null, "Actual365Fixed"), Of("FloatingLegIndex", null, "Euribor365"),
                Of("FloatingLegIndexTenor", null, "6M"), Of("FloatingLegTenor", "", "6M"), Of("FloatingLegCalendar", null, "TARGET"),
                Of("FloatingLegConvention", null, "Following"), Of("FloatingLegTerminationDateConvention", null, "Following"), Of("FloatingLegRule", null, "Forward"),
                Of("FloatingLegEndOfMonth", null, "true"), Of("FloatingLegDayCounter", null, "Actual365Fixed")]),

        new("MultipleResetsSwap", "MultipleResetsSwapConvention",
            [("Index", "Euribor"), ("IndexTenor", "3M"), ("ResetsPerCoupon", "2"), ("SettlementDays", "2"), ("FixedLegFrequency", "Semiannual"), ("FixedLegDayCounter", "Actual360"),
                ("FixedLegConvention", "Preceding"), ("Averaging", "Compound")],
            [R("MakeMultipleResetsSwap", s => Seen(s.MultipleResetsSwap("X").Builder(new Period(36, TimeUnit.Months), Curve).WithFixedRate(0.03).ToSwap(Context)))],
            [Of("Index", null, "Euribor365"), Of("IndexTenor", null, "6M"), Of("ResetsPerCoupon", null, "3"), Of("SettlementDays", null, "0"),
                Of("FixedLegFrequency", null, "Annual"), Of("FixedLegDayCounter", null, "Actual365Fixed"), Of("FixedLegConvention", null, "Following"),
                Of("Averaging", null, "Simple")]),

        new("YoYInflationCapFloor", "YoYInflationCapFloorConvention",
            [("Index", "YYEUHICP"), ("ObservationLag", "3M"), ("Interpolation", "Flat"), ("Calendar", "Desk"), ("PaymentDayCounter", "Actual360"), ("PaymentAdjustment", "Preceding"),
                ("FixingDays", "0")],
            [R("MakeYoYInflationCapFloor", s => Seen(s.YoYInflationCapFloor("X").Builder(YoYInflationCapFloorType.Cap, 5).WithStrike(0.02)
                .ToYoYInflationCapFloor(new EvaluationContext { EvaluationDate = new Date(30, Month.June, 2026) })))],
            [Of("Index", null, "YYUKRPI"), Of("ObservationLag", null, "2M"), Of("Interpolation", null, "Linear"), Of("Calendar", null, "TARGET"),
                Of("PaymentDayCounter", null, "Actual365Fixed"), Of("PaymentAdjustment", null, "Following"), Of("FixingDays", null, "2")]),

        new("Bond", "BondConvention",
            [("SettlementDays", "2"), ("Frequency", "Semiannual"), ("Calendar", "Desk"), ("Convention", "Unadjusted"), ("TerminationDateConvention", "Preceding"), ("Rule", "Backward"),
                ("EndOfMonth", "false"), ("AccrualDayCounter", "Actual360"), ("PaymentConvention", "Following"), ("PaymentCalendar", "TARGET"), ("Redemption", "100"),
                ("ExCouponPeriod", "1M"), ("ExCouponCalendar", "TARGET"), ("ExCouponConvention", "Preceding"), ("ExCouponEndOfMonth", "false")],
            [R("FixedRateBond", s => Seen(s.Bond("X").Bond(100.0, new Date(15, Month.June, 2026), Coupons[0], [0.04])), "PriceType")],
            [Of("SettlementDays", null, "1"), Of("Frequency", null, "Annual"), Of("Calendar", null, "TARGET"), Of("Convention", null, "Following"),
                Of("TerminationDateConvention", null, "Following"), Of("Rule", null, "Forward"), Of("EndOfMonth", null, "true"), Of("AccrualDayCounter", null, "Actual365Fixed"),
                Of("PaymentConvention", null, "Preceding"), Of("PaymentCalendar", null, "Desk"), Of("Redemption", null, "101"), Of("ExCouponPeriod", null, "2M"),
                Of("ExCouponCalendar", null, "Desk"), Of("ExCouponConvention", null, "Following"), Of("ExCouponEndOfMonth", null, "true"), Of("PriceType", "", "Dirty")]),

        new("BondHelper", "BondConvention",
            [("SettlementDays", "2"), ("Frequency", "Semiannual"), ("Calendar", "Desk"), ("Convention", "Unadjusted"), ("TerminationDateConvention", "Preceding"), ("Rule", "Backward"),
                ("EndOfMonth", "false"), ("AccrualDayCounter", "Actual360"), ("PaymentConvention", "Following"), ("PaymentCalendar", "TARGET"), ("Redemption", "100"),
                ("PriceType", "Clean")],
            [R("FixedRateBondHelper", s => Helper(s.Bond("X").RateHelper(100.0, new Date(15, Month.June, 2026), Coupons[0], [0.04], Context)))],
            [Of("PriceType", null, "Dirty"), Of("SettlementDays", null, "1"), Of("Redemption", null, "101"), Of("AccrualDayCounter", null, "Actual365Fixed")]),
    ];

    // A yield helper's built instrument and dates, and its implied quote on the flat curve.
    private static string Helper(RateHelper helper) => Helper(helper, Context);

    private static string Helper(RateHelper helper, EvaluationContext context) => Seen(helper, helper.SetTermStructure(Curve).ImpliedQuote(context));
}
