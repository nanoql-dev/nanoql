// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CommodityMarkets;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The commodities' conventions (the S-xrt plan's XRR1 and XRR5 (v)): the context's three fields and their defaults,
/// <see cref="Quantity"/>'s operators without a context, the delivery schedules refused, and the refusals' types and
/// texts.
/// </summary>
public class CommodityConventionTests
{
    private static readonly Quantity TenBarrels = new(None, new BarrelUnitOfMeasure(), 10.0);
    private static readonly Quantity TwoMB = new(None, new MBUnitOfMeasure(), 2.0);

    [Fact]
    public void TheContextsFieldsDefaultToQuantLibsSettings()
    {
        EvaluationContext context = Context();
        Assert.Equal(new USDCurrency(), context.CommoditySettings.Currency);
        Assert.Equal(new BarrelUnitOfMeasure(), context.CommoditySettings.UnitOfMeasure);
        Assert.Same(UnitOfMeasureConversions.Default, context.UnitOfMeasureConversions);
        Assert.Equal(7, context.UnitOfMeasureConversions.Conversions.Count);
        Assert.Equal(QuantityConversionType.NoConversion, context.QuantitySettings.ConversionType);
        Assert.Null(context.QuantitySettings.BaseUnitOfMeasure);
    }

    [Fact]
    public void TwoContextsWithEqualFieldsAreEqual()
    {
        var factor = new UnitOfMeasureConversion(None, new MTUnitOfMeasure(), new GallonUnitOfMeasure(), 300.0);
        EvaluationContext a = Context() with
        {
            CommoditySettings = new CommoditySettings { Currency = new EURCurrency(), UnitOfMeasure = new GallonUnitOfMeasure() },
            UnitOfMeasureConversions = UnitOfMeasureConversions.Default.Add(factor),
            QuantitySettings = new QuantitySettings { ConversionType = QuantityConversionType.BaseUnitOfMeasureConversion },
        };
        EvaluationContext b = Context() with
        {
            CommoditySettings = new CommoditySettings { Currency = new EURCurrency(), UnitOfMeasure = new GallonUnitOfMeasure() },
            UnitOfMeasureConversions = UnitOfMeasureConversions.Default.Add(
                new UnitOfMeasureConversion(None, new MTUnitOfMeasure(), new GallonUnitOfMeasure(), 300.0)),
            QuantitySettings = new QuantitySettings { ConversionType = QuantityConversionType.BaseUnitOfMeasureConversion },
        };
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, b with { UnitOfMeasureConversions = UnitOfMeasureConversions.Default });
        Assert.Same(UnitOfMeasureConversions.Default, a.UnitOfMeasureConversions.Clear());
    }

    [Fact]
    public void AddReplacesAFactorBetweenTheSameUnitsEitherWayRoundAndPutsItLast()
    {
        UnitOfMeasureConversions r = UnitOfMeasureConversions.Default.Add(
            new UnitOfMeasureConversion(None, new GallonUnitOfMeasure(), new BarrelUnitOfMeasure(), 1.0 / 42.5));
        Assert.Equal(7, r.Conversions.Count);
        Assert.Equal(new GallonUnitOfMeasure(), r.Conversions[^1].Source);
        Assert.Equal(7, UnitOfMeasureConversions.Default.Conversions.Count);
        Assert.Equal(new GallonUnitOfMeasure(), UnitOfMeasureConversions.Default.Conversions[1].Target);
    }

    [Fact]
    public void TheContextsFieldsRefuseNull()
    {
        Assert.Throws<ArgumentNullException>(() => Context() with { CommoditySettings = null! });
        Assert.Throws<ArgumentNullException>(() => Context() with { UnitOfMeasureConversions = null! });
        Assert.Throws<ArgumentNullException>(() => Context() with { QuantitySettings = null! });
        Assert.Throws<ArgumentNullException>(() => new CommoditySettings { Currency = null! });
        Assert.Throws<ArgumentNullException>(() => new CommoditySettings { UnitOfMeasure = null! });
    }

    [Fact]
    public void QuantitysOperatorsRefuseAUnitMismatchAsUnderNoConversion()
    {
        var e = Assert.Throws<NanoQLException>(() => TenBarrels + TwoMB);
        Assert.Equal("unitOfMeasure mismatch and no conversion specified", e.Message);
        Assert.Throws<NanoQLException>(() => TenBarrels - TwoMB);
        Assert.Throws<NanoQLException>(() => TenBarrels / TwoMB);
        Assert.Throws<NanoQLException>(() => TenBarrels < TwoMB);
        Assert.Throws<NanoQLException>(() => TenBarrels >= TwoMB);
        Assert.False(TenBarrels == TwoMB);
        Assert.True(TenBarrels != TwoMB);
        Assert.False(TenBarrels.Equals(TwoMB, Context()));
    }

    [Fact]
    public void QuantitysOperatorsCombineOneUnitsAmounts()
    {
        var four = new Quantity(None, new BarrelUnitOfMeasure(), 4.0);
        Assert.Equal(new Quantity(None, new BarrelUnitOfMeasure(), 14.0), TenBarrels + four);
        Assert.Equal(new Quantity(None, new BarrelUnitOfMeasure(), 6.0), TenBarrels - four);
        Assert.Equal(2.5, TenBarrels / four);
        Assert.True(four < TenBarrels && four <= TenBarrels && TenBarrels > four && TenBarrels >= four);
        Assert.True(TenBarrels == new Quantity(new CommodityType("Heating Oil", "HO"), new BarrelUnitOfMeasure(), 10.0));
        Assert.Equal("<NULL> 10 BBL", TenBarrels.ToString());
    }

    [Fact]
    public void ABaseUnitConversionWithNoBaseUnitIsRefused()
    {
        EvaluationContext context = Context() with
        {
            QuantitySettings = new QuantitySettings { ConversionType = QuantityConversionType.BaseUnitOfMeasureConversion },
        };
        var e = Assert.Throws<NanoQLException>(() => TenBarrels.Add(TwoMB, context));
        Assert.Equal("no base unitOfMeasure set", e.Message);
    }

    [Theory]
    [InlineData(EnergyCommodityDeliverySchedule.Constant)]
    [InlineData(EnergyCommodityDeliverySchedule.Window)]
    [InlineData(EnergyCommodityDeliverySchedule.Hourly)]
    [InlineData(EnergyCommodityDeliverySchedule.Weekly)]
    [InlineData(EnergyCommodityDeliverySchedule.Quarterly)]
    [InlineData(EnergyCommodityDeliverySchedule.Yearly)]
    public void ADeliveryScheduleOtherThanMonthlyOrDailyIsRefused(EnergyCommodityDeliverySchedule schedule)
    {
        // QuantLib returns no period, and its swaps then refuse "no pricing periods" or price as expired (XRR5 (v))
        var e = Assert.Throws<NanoQLException>(() => CommodityPricingHelperFunctions.CreatePricingPeriods(
            Today, Today + 90, TenBarrels, schedule, EnergyCommodityQuantityPeriodicity.PerWeek,
            new PaymentTerm("Pricing end", PaymentTermEventType.PricingDate, 0, new NullCalendar())));
        Assert.Equal(
            $"Invalid period quantity/pricing period combination: a {schedule} delivery schedule yields no pricing periods.",
            e.Message);
    }

    [Fact]
    public void APeriodicityThatDoesntFitItsScheduleIsRefused()
    {
        var term = new PaymentTerm("Pricing end", PaymentTermEventType.PricingDate, 0, new NullCalendar());
        foreach ((EnergyCommodityDeliverySchedule s, EnergyCommodityQuantityPeriodicity p) in new[]
        {
            (EnergyCommodityDeliverySchedule.Monthly, EnergyCommodityQuantityPeriodicity.PerDay),
            (EnergyCommodityDeliverySchedule.Daily, EnergyCommodityQuantityPeriodicity.PerMonth),
        })
        {
            var e = Assert.Throws<NanoQLException>(
                () => CommodityPricingHelperFunctions.CreatePricingPeriods(Today, Today + 90, TenBarrels, s, p, term));
            Assert.Equal("Invalid period quantity/pricing period combination.", e.Message);
        }
    }

    [Fact]
    public void AMonthlyPeriodFromAMonthsLastDayEndsTheDayBeforeTheSameDayNextMonth()
    {
        // QuantLib's design, kept: 31 January to 27 February, then 28 February to 27 March
        var periods = CommodityPricingHelperFunctions.CreatePricingPeriods(
            new Date(31, Month.January, 2025), new Date(30, Month.April, 2025), TenBarrels,
            EnergyCommodityDeliverySchedule.Monthly, EnergyCommodityQuantityPeriodicity.PerMonth,
            new PaymentTerm("Pricing end", PaymentTermEventType.PricingDate, 0, new NullCalendar()));
        Assert.Equal(new Date(27, Month.February, 2025), periods[0].EndDate);
        Assert.Equal(new Date(28, Month.February, 2025), periods[1].StartDate);
        Assert.Equal(new Date(27, Month.March, 2025), periods[1].EndDate);
    }

    [Fact]
    public void TheEnumArgumentsOutsideTheirTypesAreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UnitOfMeasure("X", "X", (UnitOfMeasureType)9));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PaymentTerm("X", (PaymentTermEventType)9, 0, new NullCalendar()));
        Assert.Throws<ArgumentOutOfRangeException>(() => UnitOfMeasureConversions.Default.Lookup(
            None, new BarrelUnitOfMeasure(), new MBUnitOfMeasure(), (UnitOfMeasureConversionType)9));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new QuantitySettings { ConversionType = (QuantityConversionType)9 });
        var term = new PaymentTerm("Pricing end", PaymentTermEventType.PricingDate, 0, new NullCalendar());
        Assert.Throws<ArgumentOutOfRangeException>(() => CommodityPricingHelperFunctions.CreatePricingPeriods(
            Today, Today + 90, TenBarrels, (EnergyCommodityDeliverySchedule)9, EnergyCommodityQuantityPeriodicity.PerDay, term));
        Assert.Throws<ArgumentOutOfRangeException>(() => CommodityPricingHelperFunctions.CreatePricingPeriods(
            Today, Today + 90, TenBarrels, EnergyCommodityDeliverySchedule.Daily, (EnergyCommodityQuantityPeriodicity)9, term));
    }

    [Fact]
    public void TheConversionsRefusalsKeepQuantLibsTexts()
    {
        var bblGal = new UnitOfMeasureConversion(None, new BarrelUnitOfMeasure(), new GallonUnitOfMeasure(), 42);
        var klBbl = new UnitOfMeasureConversion(None, new KilolitreUnitOfMeasure(), new BarrelUnitOfMeasure(), 6.28981);
        var mbGal = new UnitOfMeasureConversion(None, new MBUnitOfMeasure(), new GallonUnitOfMeasure(), 42000);
        var mbLitre = new UnitOfMeasureConversion(None, new MBUnitOfMeasure(), new LitreUnitOfMeasure(), 1.0);
        Assert.Equal(
            "conversion factors not chainable",
            Assert.Throws<NanoQLException>(() => UnitOfMeasureConversion.Chain(klBbl, mbLitre)).Message);
        Assert.Equal("direct conversion not applicable", Assert.Throws<NanoQLException>(() => bblGal.Convert(TwoMB)).Message);
        var tonne = new Quantity(None, new MTUnitOfMeasure(), 1.0);
        Assert.Equal(
            "derived conversion factor not applicable",
            Assert.Throws<NanoQLException>(() => UnitOfMeasureConversion.Chain(mbGal, bblGal).Convert(tonne)).Message);
        Assert.Equal(
            "no direct conversion available from <NULL> BBL to MT",
            Assert.Throws<NanoQLException>(() => UnitOfMeasureConversions.Default.Lookup(
                None, new BarrelUnitOfMeasure(), new MTUnitOfMeasure(), UnitOfMeasureConversionType.Direct)).Message);
        Assert.Throws<ArgumentException>(
            () => UnitOfMeasureConversions.Default.Add(UnitOfMeasureConversion.Chain(bblGal, klBbl)));
        Assert.Equal(
            "end date must be >= start date",
            Assert.Throws<NanoQLException>(() => new DateInterval(Today, Today - 1)).Message);
    }

    [Fact]
    public void EnergyCommodityResultsCompareByValueButTheirCashFlows()
    {
        CommodityIndex index = Index(new BarrelUnitOfMeasure(), 2.0);
        EvaluationContext context = WithFixing(Context(), index, 2.0);
        var costs = new Dictionary<string, object> { ["fee"] = new Money(new USDCurrency(), 3.0) };
        EnergyVanillaSwap swap = Vanilla(
            true, 1.5, new BarrelUnitOfMeasure(), index, MarchPeriods(TenBarrels), Flat(0.02), costs);
        EnergyCommodityResults r = swap.Calculate(context);
        EnergyCommodityResults copy = r with { };
        Assert.Equal(r, copy);
        Assert.Equal(r.GetHashCode(), copy.GetHashCode());
        Assert.NotEqual(r, r with { NPV = r.NPV + 1.0 });
        Assert.NotEqual(r, r with { PricingErrors = [] });
        Assert.NotEqual(r, r with { SecondaryCostAmounts = r.SecondaryCostAmounts.Clear() });
        Assert.NotEqual(r, r with { DailyPositions = r.DailyPositions.Clear() });
        Assert.NotEqual(r, swap.Calculate(context));
        Assert.Equal(r.DailyPositions, swap.Calculate(context).DailyPositions);
        var future = new EnergyFuture(
            1, TenBarrels, new CommodityUnitCost(new Money(new USDCurrency(), 1.5), new BarrelUnitOfMeasure()), index,
            None, costs);
        Assert.Equal(future.Calculate(context), future.Calculate(context));
    }

    [Fact]
    public void TheValuesDotNetEqualityFollowsQuantLibsOperators()
    {
        object bbl = new BarrelUnitOfMeasure();
        Assert.True(bbl.Equals(new UnitOfMeasure("Barrels, mine", "BBL", UnitOfMeasureType.Mass)));
        Assert.False(bbl.Equals("BBL"));
        Assert.Equal(new BarrelUnitOfMeasure().GetHashCode(), new UnitOfMeasure("x", "BBL", UnitOfMeasureType.Volume).GetHashCode());

        object ho = new CommodityType("Heating Oil", "HO");
        Assert.True(ho.Equals(new CommodityType("Gasoil", "HO")));
        Assert.False(ho.Equals(None));

        var term = new PaymentTerm("Pricing end", PaymentTermEventType.PricingDate, 0, new NullCalendar());
        object sameName = new PaymentTerm("Pricing end", PaymentTermEventType.TradeDate, 3, new TARGET());
        Assert.True(term.Equals(sameName));
        Assert.Equal(term.GetHashCode(), sameName.GetHashCode());
        Assert.False(term.Equals((object)"Pricing end"));
        Assert.Equal("Pricing end", term.ToString());

        var january = new DateInterval(Today, Today + 30);
        object same = new DateInterval(Today, Today + 30);
        Assert.True(january.Equals(same));
        Assert.True(january.Equals((DateInterval)same));
        Assert.Equal(january.GetHashCode(), same.GetHashCode());
        Assert.False(january.Equals(new DateInterval(Today, Today + 31)));
        Assert.False(january.Equals((DateInterval?)null));
        Assert.True(january.Equals(new PricingPeriod(Today, Today + 30, Today + 35, TenBarrels)));

        object ten = TenBarrels;
        Assert.True(ten.Equals(new Quantity(None, new BarrelUnitOfMeasure(), 10.0)));
        Assert.False(ten.Equals(new Quantity(new CommodityType("Heating Oil", "HO"), new BarrelUnitOfMeasure(), 10.0)));
        Assert.Equal(TenBarrels.GetHashCode(), new Quantity(None, new BarrelUnitOfMeasure(), 10.0).GetHashCode());
        Assert.True(new Quantity(None, null, double.NaN).Equals((object)new Quantity(None, null, double.NaN)));

        object factor = new UnitOfMeasureConversion(None, new BarrelUnitOfMeasure(), new GallonUnitOfMeasure(), 42);
        Assert.True(factor.Equals(new UnitOfMeasureConversion(None, new BarrelUnitOfMeasure(), new GallonUnitOfMeasure(), 42)));
        Assert.False(factor.Equals(new UnitOfMeasureConversion(None, new GallonUnitOfMeasure(), new BarrelUnitOfMeasure(), 42)));
        var tonnes = new UnitOfMeasureConversion(None, new MTUnitOfMeasure(), new BarrelUnitOfMeasure(), 7.45);
        object registry = UnitOfMeasureConversions.Default.Add(tonnes);
        Assert.True(registry.Equals(UnitOfMeasureConversions.Default.Add(
            new UnitOfMeasureConversion(None, new MTUnitOfMeasure(), new BarrelUnitOfMeasure(), 7.45))));
        Assert.Equal(registry.GetHashCode(), UnitOfMeasureConversions.Default.Add(tonnes).GetHashCode());

        // the same factors in another order differ: thousand-barrels to barrels moved last
        Assert.False(UnitOfMeasureConversions.Default.Equals((object)UnitOfMeasureConversions.Default.Add(
            new UnitOfMeasureConversion(None, new MBUnitOfMeasure(), new BarrelUnitOfMeasure(), 1000))));
        Assert.False(registry.Equals("registry"));
    }

    [Fact]
    public void APricingErrorReadsAsQuantLibWritesIt()
    {
        Assert.Equal(
            "warning: index [X] has last quote date of 2025-01-07",
            new PricingError(PricingErrorLevel.Warning, "index [X] has last quote date of 2025-01-07").ToString());
        Assert.Equal("*** error: missing quote: detail", new PricingError(PricingErrorLevel.Error, "missing quote", "detail").ToString());
        Assert.Equal("info: x", new PricingError(PricingErrorLevel.Info, "x").ToString());
        Assert.Equal("*** fatal: x", new PricingError(PricingErrorLevel.Fatal, "x").ToString());
    }

    [Fact]
    public void ADerivedConversionHasNoCommodityTypeAndAnEmptyCode()
    {
        var bblGal = new UnitOfMeasureConversion(None, new BarrelUnitOfMeasure(), new GallonUnitOfMeasure(), 42);
        var klBbl = new UnitOfMeasureConversion(None, new KilolitreUnitOfMeasure(), new BarrelUnitOfMeasure(), 6.28981);
        UnitOfMeasureConversion chained = UnitOfMeasureConversion.Chain(bblGal, klBbl);
        Assert.Null(chained.CommodityType);
        Assert.Equal(string.Empty, chained.Code);
        Assert.Equal("<NULL>BBLGAL", bblGal.Code);
    }

    [Fact]
    public void ASwapsCommodityTypeIsItsFirstPeriodsAndTheFuturesItsOwn()
    {
        CommodityIndex index = Index(new BarrelUnitOfMeasure(), 2.0);
        var ho = new CommodityType("Heating Oil", "HO");
        var swap = new EnergyVanillaSwap(
            true, new NullCalendar(), new Money(new USDCurrency(), 1.5), new BarrelUnitOfMeasure(), index, new USDCurrency(),
            new USDCurrency(), MarchPeriods(new Quantity(ho, new BarrelUnitOfMeasure(), 1000.0)), None, null,
            Flat(0.0), Flat(0.0), Flat(0.0));
        Assert.Equal(ho, swap.CommodityType);
        Assert.Equal(new Quantity(ho, new BarrelUnitOfMeasure(), 1000.0), swap.Quantity);
        var future = new EnergyFuture(
            1, TenBarrels, new CommodityUnitCost(new Money(new USDCurrency(), 1.5), new BarrelUnitOfMeasure()), index, ho, null);
        Assert.Equal(ho, future.CommodityType);
        Assert.False(future.IsExpired(Context() with { EvaluationDate = new Date(1, Month.January, 2100) }));
        Assert.Equal(
            "no pricing periods",
            Assert.Throws<NanoQLException>(() => Vanilla(true, 1.5, new BarrelUnitOfMeasure(), index, [], Flat(0.0))).Message);
    }
}
