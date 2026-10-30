// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Conventions;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// The no-bypass walk (CVD34) on the instrument conventions: what each kind's builder builds, and each helper, through
/// the marker set reaches only the set's calendars and currencies, so no default record or route builds a convention
/// outside the set; and <see cref="BTP"/> built in it.
/// </summary>
public class InstrumentWalkTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.October, 2026) };
    private static readonly FlatForward Curve = new(new Date(15, Month.October, 2026), 0.03, new Actual365Fixed());
    private static readonly FlatHazardRate Hazard = new(new Date(15, Month.October, 2026), 0.02, new Actual365Fixed());
    private static readonly ConventionSet Set = ConventionWalk.Set.With(ConventionsText.Records(InstrumentSetTests.Conventions));

    public static TheoryData<string> Builds() => [.. Built.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Builds))]
    public void EachKindsBuildReachesOnlyTheMarkerSetsCalendarsAndCurrencies(string build)
    {
        Assert.Empty(ConventionWalk.Unmarked(Built[build]()));
        Assert.NotEmpty(ConventionWalk.Unmarked(new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(Curve)).WithFixedRate(0.03).ToVanillaSwap(Context)));
    }

    private static readonly Dictionary<string, Func<object>> Built = new(StringComparer.Ordinal)
    {
        ["Schedule"] = () => Set.Schedule("S").Builder().From(new Date(15, Month.October, 2026)).To(new Date(15, Month.October, 2031)).ToSchedule(),
        ["Swap"] = () => Set.Swap("EUR-SWAP").Builder(new Period(5, TimeUnit.Years), Curve).WithFixedRate(0.03).ToVanillaSwap(Context),
        ["SwapRateHelper"] = () => Set.Swap("EUR-SWAP").RateHelper(0.03, new Period(5, TimeUnit.Years), Context, forwardingCurve: Curve),
        ["OIS"] = () => Set.Ois("USD-SOFR").Builder(new Period(5, TimeUnit.Years), Curve).WithFixedRate(0.03).ToOvernightIndexedSwap(Context),
        ["OISRateHelper"] = () => Set.Ois("USD-SOFR").RateHelper(0.03, new Period(5, TimeUnit.Years), Context, forwardingCurve: Curve),
        ["CDS"] = () => Set.Cds("CDS").Builder(new Period(5, TimeUnit.Years), 0.01).ToCreditDefaultSwap(Context),
        ["SpreadCdsHelper"] = () => Set.Cds("CDSH").SpreadHelper(0.01, new Period(5, TimeUnit.Years), 0.4, Curve, Context).SetTermStructure(Hazard),
        ["UpfrontCdsHelper"] = () => Set.Cds("CDSH").UpfrontHelper(0.02, 0.01, new Period(5, TimeUnit.Years), 0.4, Curve, Context).SetTermStructure(Hazard),
        ["ZeroCouponInflationSwap"] = () => Set.ZeroCouponInflationSwap("ZC").Swap(SwapType.Payer, 1e6, new Date(19, Month.October, 2026), new Date(19, Month.October, 2031), 0.02),
        ["ZeroCouponInflationSwapHelper"] = () => Set.ZeroCouponInflationSwap("ZC").RateHelper(0.02, new Date(19, Month.October, 2031), Context),
        ["YearOnYearInflationSwapHelper"] = () => Set.YearOnYearInflationSwap("YY").RateHelper(0.02, new Date(19, Month.October, 2031), Curve, Context),
        ["CrossCurrencyBasisSwapHelper"] = () => Set.CrossCurrencyBasisSwap("XB").RateHelper(0.001, new Period(5, TimeUnit.Years), Curve, Context, Curve, Curve),
        ["CrossCurrencySwapHelper"] = () => Set.CrossCurrencySwap("XS").RateHelper(0.02, new Period(5, TimeUnit.Years), Curve, Context, Curve),
        ["FxSwapRateHelper"] = () => Set.FxSwap("FX").RateHelper(0.001, 1.1, new Period(3, TimeUnit.Months), Curve, Context),
        ["FxForward"] = () => Set.FxSwap("FXF").Forward(1e6, 1.1e6, new Date(15, Month.January, 2027), true),
        ["Cap"] = () => Set.CapFloor("CAP").Builder(CapFloorType.Cap, new Period(5, TimeUnit.Years), Curve).WithStrike(0.03).ToCapFloor(Context),
        ["Swaption"] = () => Set.Swaption("SWO").Builder(new Period(1, TimeUnit.Years), 0.03, new Period(5, TimeUnit.Years), Curve).ToSwaption(Context),
        ["CMS swap"] = () => Set.Cms("CMS").Builder(new Period(5, TimeUnit.Years), Curve).ToSwap(Context),
        ["Multiple-resets swap"] = () => Set.MultipleResetsSwap("MRS").Builder(new Period(5, TimeUnit.Years), Curve).WithFixedRate(0.03).ToSwap(Context),
        ["Year-on-year inflation cap"] = () => Set.YoYInflationCapFloor("YYCAP").Builder(YoYInflationCapFloorType.Cap, 5).WithStrike(0.02).ToYoYInflationCapFloor(Context),
        ["Bond"] = () => Set.Bond("UST").Bond(100.0, new Date(15, Month.May, 2025), new Date(15, Month.May, 2035), [0.04]),
        ["FixedRateBondHelper"] = () => Set.Bond("USTH").RateHelper(100.0, new Date(15, Month.May, 2025), new Date(15, Month.May, 2035), [0.04], Context),
        ["BTP"] = () =>
        {
            using (ConventionProvider.Use(Set))
            {
                return new BTP(new Date(1, Month.March, 2034), 0.0395, Context, new Date(1, Month.September, 2024));
            }
        },
    };
}
