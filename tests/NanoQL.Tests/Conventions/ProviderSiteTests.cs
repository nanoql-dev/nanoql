// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.TestSuite;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// What is built after a construction follows a provider fixed at that construction, never the one ambient when it is
/// built: each object built under one provider and used under another. A leg's fallback calendar and a BMA helper's
/// fresh indexes follow their index; a rate helper's swap re-dated by a bootstrap, an engine's auxiliary swaps, a smile
/// section's swaption and caplet and a stub coupon's weighted index follow the indexes they are built on; a CDS's
/// implied hazard rate, a CDS helper's bound swap and a YoY stripper's strip follow the provider of their own
/// construction.
/// </summary>
public class ProviderSiteTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());

    [Fact]
    public void AnOvernightLegsFallbackCalendarIsBuiltInItsIndexsProvider()
    {
        // A schedule on QuantLib's empty calendar and no payment calendar: the leg falls back on a weekends-only
        // calendar, whose holiday from the index's provider moves the first payment date.
        Date firstEnd = new(16, Month.November, 2026);
        var provider = TestConventions.AddingHoliday("built", "WeekendsOnly", firstEnd);
        var using_ = new TestConventions("using");
        OvernightIndex index = Under(provider, () => new OvernightIndex("ON", 0, new EURCurrency(), new TARGET(), new Actual360(), Curve));
        var schedule = new Schedule(
            new Date(16, Month.October, 2026), new Date(16, Month.December, 2026), new Period(1, TimeUnit.Months), null,
            BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted, DateGenerationRule.Forward, false, Context);

        ImmutableArray<CashFlow> leg = Under(using_, () => new OvernightLeg(schedule, index).WithNotionals(1.0).ToLeg());
        ImmutableArray<CashFlow> quantLibs = new OvernightLeg(schedule, new Estr(Curve)).WithNotionals(1.0).ToLeg();

        Assert.Equal(firstEnd, quantLibs[0].Date);
        Assert.Equal(firstEnd + 1, leg[0].Date);
        Assert.True(provider.HasResolved("calendar:WeekendsOnly"));
        Assert.Empty(using_.Resolved);
    }

    [Fact]
    public void ARateHelpersSwapRedatedByABootstrapIsBuiltInItsIndexsProvider()
    {
        // Each helper is built on the probe's index the day before the curve's evaluation date, so the bootstrap
        // re-dates it, rebuilding its swap.
        var probe = new ProviderProbeCalendar();
        var built = new TestConventions("built");
        var dayBefore = new EvaluationContext { EvaluationDate = Today - 1 };
        IborIndex ibor = Under(built, () => ProbeIbor(probe, null));
        OvernightIndex overnight = Under(built, () => new OvernightIndex("ProbeON", 0, new EURCurrency(), probe, new Actual360()));
        Func<BootstrapHelper<YieldTermStructure>>[] helpers =
        [
            () => new SwapRateHelper(0.03, new Period(5, TimeUnit.Years), probe, Frequency.Annual,
                BusinessDayConvention.ModifiedFollowing, new Thirty360(Thirty360Convention.BondBasis), ibor, dayBefore,
                discountingCurve: Curve),
            () => new OISRateHelper(2, new Period(5, TimeUnit.Years), 0.03, overnight, dayBefore, Curve),
            () => new MultipleResetsSwapRateHelper(
                2, new Period(5, TimeUnit.Years), 0.03, ibor, 2, dayBefore, Curve, fixedFrequency: Frequency.Annual,
                fixedDayCount: new Actual360()),
        ];

        foreach (Func<BootstrapHelper<YieldTermStructure>> helper in helpers)
        {
            BootstrapHelper<YieldTermStructure> h = helper();
            probe.Clear();
            Under(new TestConventions("bootstrapping"), () => PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>
                .Create(0, new TARGET(), [h], new Actual365Fixed(), Context));
            Assert.True(probe.Saw(built), $"{h.GetType().Name}'s re-dated swap saw {string.Join(", ", probe.Seen)}");
        }
    }

    [Fact]
    public void ABmaHelpersFreshIndexesAreBuiltInItsBmaIndexsProvider()
    {
        var built = new TestConventions("built");
        var bootstrapping = new TestConventions("bootstrapping");
        var dayBefore = new EvaluationContext { EvaluationDate = Today - 1 };
        BMAIndex bma = Under(built, () => new BMAIndex());
        var helper = new BMASwapRateHelper(
            0.7, new Period(5, TimeUnit.Years), 2, new UnitedStates(UnitedStatesMarket.GovernmentBond),
            new Period(1, TimeUnit.Weeks), BusinessDayConvention.Following, new ActualActual(ActualActualConvention.ISDA),
            bma, new USDLibor(new Period(3, TimeUnit.Months), Curve), dayBefore);
        built.Clear();

        // the BMA leg's fixings before the evaluation date
        EvaluationContext context = Context with
        {
            Fixings = IndexFixings.Empty.AddFixings(bma, [new Date(7, Month.October, 2026), new Date(14, Month.October, 2026)], [0.02, 0.02]),
        };
        Under(bootstrapping, () => PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>
            .Create(0, new TARGET(), [helper], new Actual365Fixed(), context));

        Assert.True(built.HasResolved("calendar:UnitedStates.GovernmentBond"));
        Assert.True(built.HasResolved("currency:USD"));
        Assert.DoesNotContain("calendar:UnitedStates.GovernmentBond", bootstrapping.Resolved);
    }

    [Fact]
    public void TheEnginesAuxiliarySwapsAreBuiltInTheirInstrumentsIndexsProvider()
    {
        var probe = new ProviderProbeCalendar();
        var built = new TestConventions("built");
        IborIndex ibor = Under(built, () => ProbeIbor(probe, Curve));
        Date start = new(15, Month.October, 2027);
        VanillaSwap vanilla = new MakeVanillaSwap(new Period(5, TimeUnit.Years), ibor)
            .WithFixedRate(0.03).WithEffectiveDate(start).WithFixedLegTenor(new Period(1, TimeUnit.Years))
            .WithFixedLegDayCount(new Actual360()).ToVanillaSwap(Context);
        var vol = new ConstantSwaptionVolatility(
            Today, new TARGET(), BusinessDayConvention.ModifiedFollowing, 0.01, new Actual365Fixed(), VolatilityType.Normal);

        probe.Clear();
        Under(new TestConventions("pricing"), () => HaganIrregularSwaptionEngine.Calculate(
            new IrregularSwaption(new IrregularSwap(SwapType.Payer, vanilla.FixedLeg, vanilla.FloatingLeg), new EuropeanExercise(start)),
            Context, vol, Curve));
        Assert.True(probe.Saw(built), "Hagan's basket");

        VanillaSwap spot = new MakeVanillaSwap(new Period(5, TimeUnit.Years), ibor)
            .WithFixedRate(0.03).WithFixedLegTenor(new Period(1, TimeUnit.Years)).WithFixedLegDayCount(new Actual360())
            .ToVanillaSwap(Context);
        probe.Clear();
        Under(new TestConventions("pricing"), () => CounterpartyAdjSwapEngine.Calculate(
            spot, Context, Curve, 0.15, new FlatHazardRate(Today, 0.02, new Actual365Fixed()), 0.4));
        Assert.True(probe.Saw(built), "the counterparty engine's swaptionlets");
    }

    [Fact]
    public void ASmileSectionsSwaptionAndCapletAreBuiltInTheirIndexsProvider()
    {
        var probe = new ProviderProbeCalendar();
        var built = new TestConventions("built");
        IborIndex ibor = Under(built, () => ProbeIbor(probe, Curve));
        SwapIndex swapIndex = Under(built, () => new SwapIndex(
            "ProbeSwap", new Period(5, TimeUnit.Years), 2, new EURCurrency(), probe, new Period(1, TimeUnit.Years),
            BusinessDayConvention.ModifiedFollowing, new Thirty360(Thirty360Convention.BondBasis), ibor, Curve));
        var model = new Gsr(Curve, [Today + new Period(1, TimeUnit.Years)], [0.008, 0.01], [0.02, 0.03]);
        Date fixing = new(15, Month.October, 2027);
        var swaptionSection = new Gaussian1dSmileSection(fixing, swapIndex, model, new Actual365Fixed(), Context);
        var capletSection = new Gaussian1dSmileSection(fixing, ibor, model, new Actual365Fixed(), Context);

        foreach (Gaussian1dSmileSection section in new[] { swaptionSection, capletSection })
        {
            probe.Clear();
            Under(new TestConventions("pricing"), () => section.OptionPrice(0.03));
            Assert.True(probe.Saw(built));
        }
    }

    [Fact]
    public void AStubCouponsWeightedIndexKeepsItsFirstComponentsProvider()
    {
        var built = new TestConventions("built");
        StubIndexSelection selection = Under(built, () => new StubIndexSelection(
            StubIndexSelectionConvention.Interpolated,
            [new Euribor(new Period(1, TimeUnit.Months), Curve), new Euribor(new Period(3, TimeUnit.Months), Curve)]));
        var coupon = Under(new TestConventions("coupon"), () => new StubIborCoupon(
            new Date(15, Month.December, 2026), 1.0, Today, new Date(15, Month.December, 2026), 2, selection));

        Assert.IsType<StubIborCouponWeightedIndex>(coupon.Index);
        Assert.Same(built, TestConventions.ProviderOf(coupon.Index));
    }

    [Fact]
    public void ACdsImpliedHazardRateIsBuiltInTheProviderOfItsConstruction()
    {
        // The provider makes the evaluation date a weekends-only holiday, which moves the flat curve's reference date.
        var built = TestConventions.AddingHoliday("built", "WeekendsOnly", Today);
        var schedule = new Schedule(
            Today, Today + new Period(5, TimeUnit.Years), new Period(3, TimeUnit.Months), new WeekendsOnly(),
            BusinessDayConvention.Following, BusinessDayConvention.Unadjusted, DateGenerationRule.Forward, false, Context);
        CreditDefaultSwap Cds() => new(ProtectionSide.Buyer, 1e6, 0.01, schedule, BusinessDayConvention.Following, new Actual360());
        CreditDefaultSwap inProvider = Under(built, Cds);
        CreditDefaultSwap quantLibs = Cds();

        double here = Under(new TestConventions("pricing"), () => inProvider.ImpliedHazardRate(10000.0, Curve, new Actual365Fixed(), Context));
        double there = Under(built, () => inProvider.ImpliedHazardRate(10000.0, Curve, new Actual365Fixed(), Context));
        double plain = quantLibs.ImpliedHazardRate(10000.0, Curve, new Actual365Fixed(), Context);

        Assert.Equal(there, here);
        Assert.NotEqual(plain, here);
    }

    [Fact]
    public void ACdsHelpersBoundSwapIsBuiltInTheProviderOfItsConstruction()
    {
        var built = new TestConventions("built");
        var binding = new TestConventions("binding");
        SpreadCdsHelper helper = Under(built, () => new SpreadCdsHelper(
            0.01, new Period(5, TimeUnit.Years), 0, new WeekendsOnly(), Frequency.Quarterly, BusinessDayConvention.Following,
            DateGenerationRule.CDS, new Actual360(), 0.4, Curve, Context));
        var curve = new FlatHazardRate(Today, 0.02, new Actual365Fixed());

        SpreadCdsHelper bound = Under(binding, () => helper.SetTermStructure(curve));
        built.Clear();
        Under(binding, () => bound.Swap!.ImpliedHazardRate(0.0, Curve, new Actual365Fixed(), Context));

        Assert.True(built.HasResolved("calendar:WeekendsOnly"));
        Assert.Empty(binding.Resolved);
    }

    [Fact]
    public void AStrippersStripIsBuiltInTheProviderOfItsConstruction()
    {
        var built = new TestConventions("built");
        var stripping = new TestConventions("stripping");
        var stripper = Under(built, () => new InterpolatedYoYOptionletStripper<Linear>());
        InflationVolatilityTests.Market m = InflationVolatilityTests.Setup();
        var surface = InflationVolatilityTests.SetupPriceSurface(m);
        built.Clear();

        Under(stripping, () => stripper.Strip(
            surface,
            (c, ctx, v) => YoYInflationUnitDisplacedBlackCapFloorEngine.Calculate(c, ctx, m.YoYIndexEU, v, m.NominalEUR),
            -0.5,
            m.Context));

        Assert.True(built.HasResolved("calendar:TARGET"));
        Assert.DoesNotContain("calendar:TARGET", stripping.Resolved);
    }

    [Fact]
    public void AHistoricalAnalysissDepositHelpersAreBuiltInTheirIndexsProvider()
    {
        var probe = new ProviderProbeCalendar();
        var built = new TestConventions("built");
        IborIndex ibor = Under(built, () => ProbeIbor(probe, null));
        Date start = new(1, Month.September, 2026), end = new(4, Month.September, 2026);
        Date[] days = [start, start + 1, start + 2, end];
        var context = Context with { Fixings = IndexFixings.Empty.AddFixings(ibor, days, [0.02, 0.021, 0.022, 0.023]) };

        probe.Clear();
        Under(new TestConventions("analysing"), () => HistoricalForwardRatesAnalysisFunctions
            .HistoricalForwardRatesAnalysis<Discount, LogLinear>(
                new SequenceStatistics(), start, end, new Period(1, TimeUnit.Days), ibor, new Period(1, TimeUnit.Months),
                new Period(2, TimeUnit.Years), [ibor], [], new Actual365Fixed(), context));

        Assert.True(probe.Saw(built), $"the deposit helpers saw {string.Join(", ", probe.Seen)}");
    }

    private static T Under<T>(ConventionProvider provider, Func<T> build)
    {
        using (ConventionProvider.Use(provider))
        {
            return build();
        }
    }

    private static IborIndex ProbeIbor(ProviderProbeCalendar probe, YieldTermStructure? curve) => new(
        "Probe", new Period(6, TimeUnit.Months), 2, new EURCurrency(), probe, BusinessDayConvention.ModifiedFollowing,
        true, new Actual360(), curve);
}
