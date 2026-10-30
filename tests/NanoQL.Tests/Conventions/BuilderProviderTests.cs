// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A market builder takes the provider of its index and builds inside it, wherever it is built; an object that builds
/// a builder inside passes its own provider, so a swap index's underlying swap, a swaption builder's swap and a CMS
/// builder's floating leg take the outer object's, even on an index built under another; and a CDS builder, which has
/// no index, takes the one current when it is created. A probe calendar on the index records the provider ambient while
/// the build computes its dates.
/// </summary>
public class BuilderProviderTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());

    [Fact]
    public void AVanillaSwapBuilderBuildsInItsIndexsProvider()
    {
        var probe = new ProviderProbeCalendar();
        var built = new TestConventions("built");
        var building = new TestConventions("building");
        IborIndex index = Under(built, () => ProbeIbor(probe));

        probe.Clear();
        VanillaSwap swap = Under(building, () => new MakeVanillaSwap(new Period(5, TimeUnit.Years), index)
            .WithFixedRate(0.03).ToVanillaSwap(Context));

        Assert.NotEmpty(swap.FixedLeg);
        Assert.True(probe.Saw(built));
    }

    [Fact]
    public void AVanillaSwapBuildersCurrencyChainIsBuiltInItsIndexsProvider()
    {
        // The chain's currencies, built in the index's provider, resolve there, not in the one current at the build.
        var marking = TestConventions.MarkingCurrencies("marking", "EUR");
        var building = new TestConventions("building");
        IborIndex euribor = Under(marking, () => new Euribor6M(Curve));
        marking.Clear();

        VanillaSwap swap = Under(building, () => new MakeVanillaSwap(new Period(5, TimeUnit.Years), euribor)
            .WithFixedRate(0.03).ToVanillaSwap(Context));

        Assert.Equal("marking", TestConventions.MarkOf(euribor.Currency!));
        Assert.Equal(new Thirty360(Thirty360Convention.BondBasis), swap.FixedDayCount);
        Assert.Equal(new Period(1, TimeUnit.Years), swap.FixedSchedule.Tenor());
        Assert.True(marking.HasResolved("currency:EUR"));
        Assert.DoesNotContain("currency:EUR", building.Resolved);
    }

    [Fact]
    public void TheOtherBuildersBuildInTheirIndexsProvider()
    {
        var probe = new ProviderProbeCalendar();
        var built = new TestConventions("built");
        var building = new TestConventions("building");
        IborIndex ibor = Under(built, () => ProbeIbor(probe));
        OvernightIndex overnight = Under(built, () => new OvernightIndex("ProbeON", 0, new EURCurrency(), probe, new Actual360(), Curve));
        SwapIndex swapIndex = Under(built, () => ProbeSwapIndex(probe, ibor));

        void Check(string builder, Action build)
        {
            probe.Clear();
            Under(building, () =>
            {
                build();
                return 0;
            });
            Assert.True(probe.Saw(built), $"{builder} built outside its index's provider");
        }

        Check("MakeOIS", () => new MakeOIS(new Period(5, TimeUnit.Years), overnight).WithFixedRate(0.03).ToOvernightIndexedSwap(Context));
        Check("MakeCapFloor", () => new MakeCapFloor(CapFloorType.Cap, new Period(5, TimeUnit.Years), ibor).WithStrike(0.03).ToCapFloor(Context));
        Check("MakeMultipleResetsSwap", () => new MakeMultipleResetsSwap(new Period(5, TimeUnit.Years), ibor, 2)
            .WithFixedRate(0.03).ToSwap(Context));
        Check("MakeSwaption", () => new MakeSwaption(swapIndex, new Period(1, TimeUnit.Years), 0.03).ToSwaption(Context));
        Check("MakeCms", () => new MakeCms(new Period(5, TimeUnit.Years), swapIndex).ToSwap(Context));

        // the YoY cap builder's schedule and payments on the calendar it is given, the probe
        YoYInflationIndex yoy = Under(built, () => new YYEUHICP());
        Check("MakeYoYInflationCapFloor", () => new MakeYoYInflationCapFloor(
                YoYInflationCapFloorType.Cap, yoy, 5, probe, new Period(3, TimeUnit.Months), CPIInterpolationType.Flat)
            .WithStrike(0.02).ToYoYInflationCapFloor(Context));
    }

    [Fact]
    public void ACdsBuilderTakesTheProviderCurrentWhenItIsCreated()
    {
        // The provider adds a holiday to the builder's weekends-only calendar on the day before the upfront date.
        Date upfront = new WeekendsOnly().Advance(Today, 3, TimeUnit.Days);
        var provider = TestConventions.AddingHoliday("p", "WeekendsOnly", upfront - 1);
        MakeCreditDefaultSwap builder = Under(provider, () => new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01));

        CreditDefaultSwap cds = Under(new TestConventions("building"), () => builder.ToCreditDefaultSwap(Context));
        CreditDefaultSwap quantLibs = new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01).ToCreditDefaultSwap(Context);

        Assert.Equal(upfront, quantLibs.UpfrontPayment.Date);
        Assert.Equal(upfront + 1, cds.UpfrontPayment.Date);
    }

    [Fact]
    public void ASwapIndexsUnderlyingSwapTakesTheSwapIndexsProviderOnAnIborIndexBuiltUnderAnother()
    {
        var probe = new ProviderProbeCalendar();
        var iborProvider = new TestConventions("ibor");
        var swapProvider = new TestConventions("swap");
        IborIndex ibor = Under(iborProvider, () => ProbeIbor(probe));
        SwapIndex swapIndex = Under(swapProvider, () => ProbeSwapIndex(probe, ibor));
        Date fixing = new(19, Month.October, 2026);

        foreach (Action use in new Action[]
        {
            () => swapIndex.UnderlyingSwap(fixing, Context),
            () => swapIndex.Fixing(fixing, Context),
            () => swapIndex.MaturityDate(swapIndex.ValueDate(fixing)),
        })
        {
            probe.Clear();
            Under(new TestConventions("pricing"), () =>
            {
                use();
                return 0;
            });
            Assert.True(probe.Saw(swapProvider));
            Assert.False(probe.Saw(iborProvider));
        }
    }

    [Fact]
    public void AnOvernightIndexedSwapIndexsSwapTakesItsOwnProvider()
    {
        var probe = new ProviderProbeCalendar();
        var overnightProvider = new TestConventions("overnight");
        var swapProvider = new TestConventions("swap");
        OvernightIndex overnight = Under(
            overnightProvider, () => new OvernightIndex("ProbeON", 0, new EURCurrency(), probe, new Actual360(), Curve));
        var swapIndex = Under(
            swapProvider, () => new OvernightIndexedSwapIndex("ProbeOIS", new Period(5, TimeUnit.Years), 2, new EURCurrency(), overnight));

        probe.Clear();
        Under(new TestConventions("pricing"), () => swapIndex.UnderlyingSwap(new Date(19, Month.October, 2026), Context));

        Assert.True(probe.Saw(swapProvider));
        Assert.False(probe.Saw(overnightProvider));
    }

    [Fact]
    public void ASwaptionBuildersAndACmsBuildersSwapsTakeTheirSwapIndexsProvider()
    {
        // The swaption builder's inner builders take its swap index's provider, and so does the CMS builder's floating
        // leg on an Ibor index of its own, built under another provider.
        var probe = new ProviderProbeCalendar();
        var iborProvider = new TestConventions("ibor");
        var swapProvider = new TestConventions("swap");
        IborIndex ibor = Under(iborProvider, () => ProbeIbor(probe));
        SwapIndex swapIndex = Under(swapProvider, () => ProbeSwapIndex(probe, ibor));
        IborIndex other = Under(iborProvider, () => ProbeIbor(probe));

        foreach (Action use in new Action[]
        {
            () => new MakeSwaption(swapIndex, new Period(1, TimeUnit.Years), 0.03).ToSwaption(Context),
            () => new MakeCms(new Period(5, TimeUnit.Years), swapIndex, other).ToSwap(Context),
        })
        {
            probe.Clear();
            Under(new TestConventions("pricing"), () =>
            {
                use();
                return 0;
            });
            Assert.True(probe.Saw(swapProvider));
            Assert.False(probe.Saw(iborProvider));
        }
    }

    private static T Under<T>(ConventionProvider provider, Func<T> build)
    {
        using (ConventionProvider.Use(provider))
        {
            return build();
        }
    }

    private static IborIndex ProbeIbor(ProviderProbeCalendar probe) => new(
        "Probe", new Period(6, TimeUnit.Months), 2, new EURCurrency(), probe, BusinessDayConvention.ModifiedFollowing,
        true, new Actual360(), Curve);

    private static SwapIndex ProbeSwapIndex(ProviderProbeCalendar probe, IborIndex ibor) => new(
        "ProbeSwap", new Period(10, TimeUnit.Years), 2, new EURCurrency(), probe, new Period(1, TimeUnit.Years),
        BusinessDayConvention.ModifiedFollowing, new Thirty360(Thirty360Convention.BondBasis), ibor, Curve);
}
