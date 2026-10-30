// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Extensions.Conventions;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Tests.Extensions;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The shipped default records against QuantLib's coded builder defaults: each builder on an index built in
/// <see cref="ConventionSet.QuantLib"/> builds, bit for bit, what the bare builder builds on the coded index (every
/// date and amount of its legs, a swaption's exercise and settlement, a bond's cash flows and settlement date), the
/// swap builder's by currency at tenors and dates either side of GBP's and AUD's thresholds, the OIS builder's on every
/// overnight preset with its settlement days; a bond convention's bonds what QuantLib's constructor builds with its
/// defaults; and <see cref="BTP"/> built in the set its coded class. Each default record gives every option its builder
/// fixes, the ones it derives listed apart (CVD40).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InstrumentPresetTests
{
    private static readonly ConventionSet Set = ConventionSet.QuantLib;
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.October, 2026) };
    private static readonly FlatForward Curve = new(new Date(15, Month.October, 2026), 0.03, new Actual365Fixed());

    // Each default record's kind, and the options of its kind its builder derives or doesn't fix, with the builder line
    // each comes from (CVD40).
    private static readonly Dictionary<string, (Type Kind, string[] Derived)> Defaults = new(StringComparer.Ordinal)
    {
        // makevanillaswap.cpp:51-70, the index's calendar, tenor, conventions and day counter; :85, the index's value
        // date; :160, the floating schedule's payment convention; the index the builder's argument
        ["MakeVanillaSwap"] = (typeof(SwapConventionRecord), [
            "SettlementDays", "SettlementCalendar", "FixedLegCalendar", "Index", "IndexTenor", "FloatingLegTenor", "FloatingLegCalendar",
            "FloatingLegConvention", "FloatingLegTerminationDateConvention", "FloatingLegDayCounter", "PaymentConvention"]),
        // makeois.cpp:36-52, the index's calendar and day counter; :120, the start date's end of month; the lookback the
        // index's fixing days; the payment calendar each leg's
        ["MakeOIS"] = (typeof(OisConventionRecord), [
            "Index", "SettlementCalendar", "PaymentCalendar", "FixedLegCalendar", "FixedLegEndOfMonth", "FixedLegDayCounter", "OvernightLegCalendar",
            "OvernightLegEndOfMonth", "LookbackDays"]),
        // makecds.hpp: no settlement days or pricing model, the helpers' only
        ["MakeCreditDefaultSwap"] = (typeof(CdsConventionRecord), ["SettlementDays", "PricingModel"]),
        // makecapfloor.cpp:31-40, the leg the swap builder's on its index
        ["MakeCapFloor"] = (typeof(CapFloorConventionRecord), [
            "Index", "IndexTenor", "Tenor", "Calendar", "Convention", "TerminationDateConvention", "Rule", "EndOfMonth", "DayCounter"]),
        // makeswaption.cpp:41, the swap index's fixing calendar; the swap index the builder's argument
        ["MakeSwaption"] = (typeof(SwaptionConventionRecord), ["SwapIndex", "ExerciseCalendar"]),
        // makecms.cpp:36-56, the swap index's calendar and the Ibor index's leg; both indexes the builder's arguments
        ["MakeCms"] = (typeof(CmsConventionRecord), [
            "SwapIndex", "SwapIndexTenor", "CmsLegCalendar", "FloatingLegIndex", "FloatingLegIndexTenor", "FloatingLegTenor", "FloatingLegCalendar",
            "FloatingLegConvention", "FloatingLegTerminationDateConvention", "FloatingLegDayCounter"]),
        // makemultipleresetsswap.cpp: the index and resets the builder's arguments, the settlement days and day counter
        // the index's, the fixed frequency the resets times the index's tenor
        ["MakeMultipleResetsSwap"] = (typeof(MultipleResetsSwapConventionRecord), [
            "Index", "IndexTenor", "ResetsPerCoupon", "SettlementDays", "FixedLegFrequency", "FixedLegDayCounter"]),
        // makeyoyinflationcapfloor.cpp:21-30, the builder's arguments
        ["MakeYoYInflationCapFloor"] = (typeof(YoYInflationCapFloorConventionRecord), ["Index", "ObservationLag", "Interpolation", "Calendar"]),
        // fixedratebond.hpp: a market's schedule, settlement days, day counter and payment calendar, no ex-coupon period
        ["FixedRateBond"] = (typeof(BondConventionRecord), [
            "SettlementDays", "Frequency", "Calendar", "Convention", "TerminationDateConvention", "Rule", "EndOfMonth", "AccrualDayCounter",
            "PaymentCalendar", "ExCouponPeriod", "ExCouponCalendar"]),
        // btp.cpp:68-85: the redemption an argument, no ex-coupon period, no helper's price type
        ["BTP"] = (typeof(BondConventionRecord), ["Redemption", "ExCouponPeriod", "ExCouponCalendar", "ExCouponConvention", "ExCouponEndOfMonth", "PriceType"]),
    };

    private static readonly string[] SwapCurrencies = ["AUD", "CHF", "EUR", "GBP", "HKD", "JPY", "SEK", "THB", "USD"];

    public static TheoryData<string> Currencies() => [.. SwapCurrencies];

    public static TheoryData<string> OvernightPresets() => [.. CodedPresets.OvernightIndexes.Keys.Order(StringComparer.Ordinal)];

    public static TheoryData<string> DefaultRecords() =>
        [.. Set.Layers[0].Records.Where(r => r.GetType().Name.EndsWith("ConventionRecord", StringComparison.Ordinal)).Select(r => r.Name)];

    [Fact]
    public void TheShippedInstrumentRecordsAreTheBuildersDefaultRecords()
    {
        string[] names = [.. Set.Layers[0].Records.Where(r => r.GetType().Name.EndsWith("ConventionRecord", StringComparison.Ordinal)).Select(r => r.Name).Order(StringComparer.Ordinal)];
        Assert.Equal(
            SwapCurrencies.Select(c => "MakeVanillaSwap." + c).Concat(new[] { "BTP", "FixedRateBond", "MakeCapFloor", "MakeCms", "MakeCreditDefaultSwap",
                "MakeMultipleResetsSwap", "MakeOIS", "MakeOIS.Corra", "MakeOIS.Sonia", "MakeSwaption", "MakeYoYInflationCapFloor" })
                .Order(StringComparer.Ordinal),
            names);
        Assert.Equal(20, names.Length);
    }

    [Theory]
    [MemberData(nameof(DefaultRecords))]
    public void EachDefaultRecordGivesEveryOptionItsBuilderFixes(string name)
    {
        string builder = name.StartsWith("MakeVanillaSwap.", StringComparison.Ordinal) ? "MakeVanillaSwap"
            : name.StartsWith("MakeOIS", StringComparison.Ordinal) ? "MakeOIS" : name;
        (Type kind, string[] derived) = Defaults[builder];
        ConventionRecord record = Merged(name);
        Assert.IsType(kind, record);

        string[] fields = [.. kind.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(p => p.Name).Where(p => !derived.Contains(p))];

        // QuantLib's tenor rule has a threshold for GBP and AUD only, and no tenor for THB
        string[] absent = name switch
        {
            "MakeVanillaSwap.THB" => ["FixedLegTenor", "FixedLegTenorThreshold"],
            "MakeVanillaSwap.GBP" or "MakeVanillaSwap.AUD" => [],
            _ when builder == "MakeVanillaSwap" => ["FixedLegTenorThreshold"],
            _ => [],
        };
        Assert.Equal(fields.Except(absent).Order(StringComparer.Ordinal), Given(record).Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Currencies))]
    public void ACurrencysSwapDefaultsBuildWhatTheBareBuilderBuildsEitherSideOfItsThreshold(string currency)
    {
        // a plain index of the currency, built in the set and coded
        IborIndex Index() => new IborIndex("Desk", new Period(6, TimeUnit.Months), 2, Set.Currency(currency), new TARGET(), BusinessDayConvention.ModifiedFollowing, false, new Actual360(), Curve);
        IborIndex inSet;
        using (ConventionProvider.Use(Set))
        {
            inSet = Index();
        }

        IborIndex coded = Index();
        Date start = new(19, Month.October, 2026);
        foreach (Func<IborIndex, MakeVanillaSwap> swap in new Func<IborIndex, MakeVanillaSwap>[]
        {
            i => new MakeVanillaSwap(new Period(1, TimeUnit.Years), i),
            i => new MakeVanillaSwap(new Period(2, TimeUnit.Years), i),
            i => new MakeVanillaSwap(new Period(4, TimeUnit.Years), i),
            i => new MakeVanillaSwap(new Period(5, TimeUnit.Years), i),

            // termination dates either side of GBP's 1Y and AUD's 4Y, read on QuantLib's approximate months
            i => new MakeVanillaSwap(new Period(1, TimeUnit.Years), i).WithEffectiveDate(start).WithTerminationDate(start + new Period(12, TimeUnit.Months)),
            i => new MakeVanillaSwap(new Period(1, TimeUnit.Years), i).WithEffectiveDate(start).WithTerminationDate(start + new Period(13, TimeUnit.Months)),
            i => new MakeVanillaSwap(new Period(1, TimeUnit.Years), i).WithEffectiveDate(start).WithTerminationDate(start + new Period(47, TimeUnit.Months)),
            i => new MakeVanillaSwap(new Period(1, TimeUnit.Years), i).WithEffectiveDate(start).WithTerminationDate(start + new Period(48, TimeUnit.Months)),
        })
        {
            Assert.Equal(Built(() => swap(coded).WithFixedRate(0.03).ToVanillaSwap(Context)), Built(() => swap(inSet).WithFixedRate(0.03).ToVanillaSwap(Context)));
        }

        // QuantLib's fixed leg tenors on the termination dates, read on the approximate months (the wheel's)
        if (currency is "GBP" or "AUD")
        {
            Period[] expected = currency == "GBP"
                ? [new(1, TimeUnit.Years), new(6, TimeUnit.Months), new(6, TimeUnit.Months), new(6, TimeUnit.Months)]
                : [new(3, TimeUnit.Months), new(3, TimeUnit.Months), new(3, TimeUnit.Months), new(6, TimeUnit.Months)];
            Period[] tenors =
            [
                .. new[] { 12, 13, 47, 48 }.Select(m => new MakeVanillaSwap(new Period(1, TimeUnit.Years), inSet).WithEffectiveDate(start)
                    .WithTerminationDate(start + new Period(m, TimeUnit.Months)).WithFixedRate(0.03).ToVanillaSwap(Context).FixedSchedule.Tenor()),
            ];
            Assert.Equal(expected, tenors);
        }

        // THB has no tenor rule: given one, its day counter
        Assert.Equal(
            Built(() => new MakeVanillaSwap(new Period(5, TimeUnit.Years), coded).WithFixedLegTenor(new Period(1, TimeUnit.Years)).WithFixedRate(0.03).ToVanillaSwap(Context)),
            Built(() => new MakeVanillaSwap(new Period(5, TimeUnit.Years), inSet).WithFixedLegTenor(new Period(1, TimeUnit.Years)).WithFixedRate(0.03).ToVanillaSwap(Context)));
    }

    [Theory]
    [MemberData(nameof(OvernightPresets))]
    public void TheOisDefaultsBuildWhatTheBareBuilderBuildsOnEveryOvernightPreset(string name)
    {
        OvernightIndex inSet = Set.OvernightIndex(name);
        var coded = (OvernightIndex)CodedPresets.OvernightIndexes[name]();
        OvernightIndexedSwap swap = new MakeOIS(new Period(5, TimeUnit.Years), inSet).WithFixedRate(0.03).ToOvernightIndexedSwap(Context);

        Assert.Equal(Built(() => new MakeOIS(new Period(5, TimeUnit.Years), coded).WithFixedRate(0.03).ToOvernightIndexedSwap(Context)), Built(() => swap));

        // SONIA settles T+0, CORRA T+1, the others T+2
        int days = name switch { "Sonia" => 0, "Corra" => 1, _ => 2 };
        Assert.Equal(inSet.FixingCalendar.Advance(Context.EvaluationDate, days, TimeUnit.Days), swap.StartDate());
    }

    [Fact]
    public void TheOtherBuildersDefaultsBuildWhatTheBareBuildersBuild()
    {
        var tenor = new Period(5, TimeUnit.Years);
        Assert.Equal(
            Built(() => new MakeCreditDefaultSwap(tenor, 0.01).ToCreditDefaultSwap(Context)),
            Built(() => InSet(() => new MakeCreditDefaultSwap(tenor, 0.01)).ToCreditDefaultSwap(Context)));
        Assert.Equal(
            Built(() => new MakeCapFloor(CapFloorType.Cap, tenor, new Euribor6M(Curve)).WithStrike(0.03).ToCapFloor(Context)),
            Built(() => new MakeCapFloor(CapFloorType.Cap, tenor, Set.IborIndex("Euribor6M", Curve)).WithStrike(0.03).ToCapFloor(Context)));
        Assert.Equal(
            Built(() => new MakeSwaption(new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), Curve), new Period(1, TimeUnit.Years), 0.03).ToSwaption(Context)),
            Built(() => new MakeSwaption(Set.SwapIndex("EuriborSwapIsdaFixA", new Period(10, TimeUnit.Years), Curve), new Period(1, TimeUnit.Years), 0.03).ToSwaption(Context)));
        Assert.Equal(
            Built(() => new MakeCms(tenor, new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), Curve)).ToSwap(Context)),
            Built(() => new MakeCms(tenor, Set.SwapIndex("EuriborSwapIsdaFixA", new Period(10, TimeUnit.Years), Curve)).ToSwap(Context)));
        Assert.Equal(
            Built(() => new MakeMultipleResetsSwap(tenor, new Euribor3M(Curve), 2).WithFixedRate(0.03).ToSwap(Context)),
            Built(() => new MakeMultipleResetsSwap(tenor, Set.IborIndex("Euribor3M", Curve), 2).WithFixedRate(0.03).ToSwap(Context)));
        Assert.Equal(
            Built(() => new MakeYoYInflationCapFloor(YoYInflationCapFloorType.Cap, new YYEUHICP(), 5, new TARGET(), new Period(3, TimeUnit.Months), CPIInterpolationType.Flat)
                .WithStrike(0.02).ToYoYInflationCapFloor(Context)),
            Built(() => new MakeYoYInflationCapFloor(YoYInflationCapFloorType.Cap, Set.YoYInflationIndex("YYEUHICP"), 5, new TARGET(), new Period(3, TimeUnit.Months), CPIInterpolationType.Flat)
                .WithStrike(0.02).ToYoYInflationCapFloor(Context)));
    }

    [Theory]
    [InlineData("Semiannual", "UnitedStates.GovernmentBond")]
    [InlineData("Annual", "TARGET")]
    public void ABondConventionsBondIsWhatQuantLibsConstructorBuildsWithItsDefaults(string frequency, string calendar)
    {
        // a market's record giving what the constructor has no default for; the rest the set's FixedRateBond record
        ConventionSet set = ConventionsText.OnQuantLib(
            $"<BondConvention name=\"Govt\"><SettlementDays>1</SettlementDays><Frequency>{frequency}</Frequency><Calendar>{calendar}</Calendar>"
            + "<Convention>Unadjusted</Convention><AccrualDayCounter>ActualActual.ISMA</AccrualDayCounter></BondConvention>");
        Date issue = new(15, Month.May, 2025), maturity = new(15, Month.May, 2035);
        FixedRateBond bond = set.Bond("Govt").Bond(100.0, issue, maturity, [0.04]);
        var expected = new FixedRateBond(
            1,
            100.0,
            new MakeSchedule().From(issue).To(maturity).WithFrequency(Enum.Parse<Frequency>(frequency)).WithCalendar(set.Calendar(calendar))
                .WithConvention(BusinessDayConvention.Unadjusted).ToSchedule(),
            [0.04],
            new ActualActual(ActualActualConvention.ISMA),
            issueDate: issue);

        Assert.Equal(Built(() => expected), Built(() => bond));
        Assert.Equal(expected.SettlementDate(Context), bond.SettlementDate(Context));
    }

    [Fact]
    public void BtpBuiltInTheSetIsItsCodedClass()
    {
        Date maturity = new(1, Month.March, 2034), start = new(1, Month.September, 2024);
        BTP coded = new(maturity, 0.0395, Context, start);
        BTP inSet = InSet(() => new BTP(maturity, 0.0395, Context, start));

        Assert.Equal(Built(() => coded), Built(() => inSet));
        Assert.Equal(coded.SettlementDate(Context), inSet.SettlementDate(Context));
        Assert.Equal(coded.AccruedAmount(Context), inSet.AccruedAmount(Context));
    }

    private static ConventionRecord Merged(string name) => name switch
    {
        _ when name.StartsWith("MakeVanillaSwap.", StringComparison.Ordinal) => Set.Swap(name).Record,
        _ when name.StartsWith("MakeOIS", StringComparison.Ordinal) => Set.Ois(name).Record,
        "MakeCreditDefaultSwap" => Set.Cds(name).Record,
        "MakeCapFloor" => Set.CapFloor(name).Record,
        "MakeSwaption" => Set.Swaption(name).Record,
        "MakeCms" => Set.Cms(name).Record,
        "MakeMultipleResetsSwap" => Set.MultipleResetsSwap(name).Record,
        "MakeYoYInflationCapFloor" => Set.YoYInflationCapFloor(name).Record,
        _ => Set.Bond(name).Record,
    };

    private static IEnumerable<string> Given(ConventionRecord r) =>
        r.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(p => p.GetValue(r) is not null).Select(p => p.Name);

    private static T InSet<T>(Func<T> build)
    {
        using (ConventionProvider.Use(Set))
        {
            return build();
        }
    }

    // What a build makes, or what it refuses, as text.
    private static string Built(Func<object?> build)
    {
        try
        {
            return Fingerprint.Of(build(), Context);
        }
        catch (NanoQLException e)
        {
            return "refused: " + e.Message;
        }
    }
}
