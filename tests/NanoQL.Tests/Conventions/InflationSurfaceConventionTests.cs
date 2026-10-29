// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.TestSuite;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The experimental inflation surfaces' conventions: QuantLib's refusal texts, the generic indexes' names, the
/// observation-lag sentinel, the engine on an expired instrument and on lags it can't subtract, and the stripped set's
/// and the helper's own refusals.
/// </summary>
public class InflationSurfaceConventionTests
{
    [Fact]
    public void TheCPISurfaceRefusesBadDataWithQuantLibsTexts()
    {
        var common = new InflationCPICapFloorTests.CommonVars();
        Matrix rising = common.CPriceUK.Clone();
        rising[1, 0] = rising[0, 0] * 2;
        Matrix negative = common.FPriceUK.Clone();
        negative[0, 1] = -0.0001;
        Assert.Equal("not enough floor strikes", Refusal(() => Cpi(common, fStrikes: [0.0], fPrice: Row(common.FPriceUK, 1))));
        Assert.Equal("non-positive floor price: -0.0001", Refusal(() => Cpi(common, fPrice: negative)));
        Assert.Equal(
            "non-decreasing cap prices: 0.04552 then 0.02276",
            Refusal(() => Cpi(common, cPrice: rising)));
        Assert.Equal(
            "non-increasing maturities",
            Refusal(() => Cpi(common, maturities: [.. common.CfMaturitiesUK[..2], common.CfMaturitiesUK[1], .. common.CfMaturitiesUK[3..]])));
        Assert.Equal("ZITS missing from index", Refusal(() => Cpi(common, index: new UKRPI())));
    }

    [Fact]
    public void TheYoYSurfacesCapPriceTextShowsNoPrices()
    {
        InflationVolatilityTests.Market m = InflationVolatilityTests.Setup();
        Matrix rising = m.CPriceEU.Clone();
        rising[1, 0] = rising[0, 0] * 2;
        Assert.Equal("non-decreasing cap prices", Refusal(() => InterpolatedYoYCapFloorTermPriceSurface<Bicubic, Cubic>.Create(
            0, new Period(3, TimeUnit.Months), m.YoYIndexEU, CPIInterpolationType.Linear, m.NominalEUR,
            new Actual365Fixed(), new TARGET(), BusinessDayConvention.ModifiedFollowing, m.CStrikesEU, m.FStrikesEU,
            m.CfMaturitiesEU, rising, m.FPriceEU, m.Context)));
    }

    [Fact]
    public void QuantLibsMinusOneDayLagIsTheSurfacesLag()
    {
        InflationVolatilityTests.Market m = InflationVolatilityTests.Setup();
        var surf = InflationVolatilityTests.SetupPriceSurface(m);
        Date d = surf.ReferenceDate + new Period(4, TimeUnit.Years);
        Assert.Equal(surf.AtmYoYRate(d), surf.AtmYoYRate(d, new Period(-1, TimeUnit.Days)));
        Assert.Equal(surf.AtmYoYRate(d), surf.AtmYoYRate(d, surf.ObservationLag));
        Assert.NotEqual(surf.AtmYoYRate(d), surf.AtmYoYRate(d, new Period(1, TimeUnit.Months)));
        Assert.Equal(surf.AtmYoYRate(d), surf.AtmYoYRate(new Period(4, TimeUnit.Years)));
        Assert.Equal(surf.Price(d, 0.03), surf.Price(new Period(4, TimeUnit.Years), 0.03));
    }

    [Fact]
    public void TheGenericIndexesAreQuantLibs()
    {
        var region = new GenericRegion();
        Assert.Equal(("Generic", "GENERIC"), (region.Name, region.Code));
        var cpi = new GenericCPI(Frequency.Monthly, false, new Period(3, TimeUnit.Months), new Currencies.EURCurrency());
        var yy = new YYGenericCPI(Frequency.Quarterly, true, new Period(2, TimeUnit.Months), new Currencies.USDCurrency());
        Assert.Equal(("Generic CPI", Frequency.Monthly, false), (cpi.Name, cpi.Frequency, cpi.Revised));
        Assert.Equal(("Generic YY_CPI", Frequency.Quarterly, true), (yy.Name, yy.Frequency, yy.Revised));
        Assert.Equal(new Period(2, TimeUnit.Months), yy.AvailabilityLag);
    }

    [Fact]
    public void TheCPIEngineRefusesLagsItCantSubtractAndPricesAnExpiredOptionAtZero()
    {
        var common = new InflationCPICapFloorTests.CommonVars();
        CPICapFloorTermPriceSurface surf = Cpi(common);
        CPICapFloor weeks = Cap(common, common.Context.EvaluationDate + new Period(3, TimeUnit.Years), new Period(13, TimeUnit.Weeks));
        Assert.Throws<ArgumentException>(() => InterpolatingCPICapFloorEngine.Calculate(weeks, common.Context, surf));
        CPICapFloor shorter = Cap(common, common.Context.EvaluationDate + new Period(3, TimeUnit.Years), new Period(1, TimeUnit.Months));
        Assert.Equal(
            "InterpolatingCPICapFloorEngine: lag difference must be non-negative: -1M",
            Assert.Throws<NanoQLException>(() => InterpolatingCPICapFloorEngine.Calculate(shorter, common.Context, surf)).Message);
        CPICapFloor expired = Cap(common, common.Context.EvaluationDate - 1, common.ObservationLag);
        Assert.Equal(0.0, InterpolatingCPICapFloorEngine.Calculate(expired, common.Context, surf).NPV);
    }

    [Fact]
    public void TheCPISurfacesInspectorsAreItsData()
    {
        var common = new InflationCPICapFloorTests.CommonVars();
        CPICapFloorTermPriceSurface surf = Cpi(common);
        Assert.Equal(common.CStrikesUK, surf.CapStrikes);
        Assert.Equal(common.FStrikesUK, surf.FloorStrikes);
        Assert.Equal(common.CStrikesUK.Concat(common.FStrikesUK).Distinct().Order(), surf.Strikes);
        Assert.Equal((-0.01, 0.06), (surf.MinStrike, surf.MaxStrike));
        Assert.Equal((1.0, common.BaseZeroRate, common.ObservationLag), (surf.Nominal, surf.BaseRate, surf.ObservationLag));
        Assert.Equal((Frequency.Monthly, BusinessDayConvention.ModifiedFollowing), (surf.Frequency, surf.BusinessDayConvention));
        Assert.Same(common.Ii, surf.ZeroInflationIndex);
        Assert.Equal(common.CPriceUK[0, 0], surf.CapPrices()[0, 0]);
        Assert.Equal(common.FPriceUK[1, 2], surf.FloorPrices()[1, 2]);
        Assert.Equal(surf.ReferenceDate + common.CfMaturitiesUK[0], surf.MinDate);
        Assert.Equal(surf.ReferenceDate + common.CfMaturitiesUK[^1], surf.MaxDate);
        Assert.Equal(surf.Price(surf.CpiOptionDateFromTenor(new Period(5, TimeUnit.Years)), 0.045, common.Context),
            surf.Price(new Period(5, TimeUnit.Years), 0.045, common.Context));
        Assert.Equal(surf.CapPrice(surf.CpiOptionDateFromTenor(new Period(5, TimeUnit.Years)), 0.045),
            surf.CapPrice(new Period(5, TimeUnit.Years), 0.045));
        Assert.Equal(surf.FloorPrice(surf.CpiOptionDateFromTenor(new Period(5, TimeUnit.Years)), 0.005),
            surf.FloorPrice(new Period(5, TimeUnit.Years), 0.005));
    }

    [Fact]
    public void TheYoYSurfacesInspectorsAreItsData()
    {
        InflationVolatilityTests.Market m = InflationVolatilityTests.Setup();
        var surf = InflationVolatilityTests.SetupPriceSurface(m);
        Assert.Equal(m.CStrikesEU, surf.CapStrikes);
        Assert.Equal(m.FStrikesEU, surf.FloorStrikes);
        Assert.Equal(m.CfMaturitiesEU, surf.Maturities);
        Assert.Equal((-0.01, 0.05), (surf.MinStrike, surf.MaxStrike));
        Assert.Equal((0, true, Frequency.Monthly), (surf.FixingDays, surf.IndexIsInterpolated, surf.Frequency));
        Assert.Equal(BusinessDayConvention.ModifiedFollowing, surf.BusinessDayConvention);
        Assert.Same(m.YoYIndexEU, surf.YoYIndex);
        Assert.Equal(surf.YoYOptionDateFromTenor(m.CfMaturitiesEU[0]), surf.MinMaturity);
        Assert.Equal(surf.YoYOptionDateFromTenor(m.CfMaturitiesEU[^1]), surf.MaxMaturity);
        Assert.Equal(surf.CapPrice(surf.YoYOptionDateFromTenor(new Period(4, TimeUnit.Years)), 0.03),
            surf.CapPrice(new Period(4, TimeUnit.Years), 0.03));
        Assert.Equal(surf.FloorPrice(surf.YoYOptionDateFromTenor(new Period(4, TimeUnit.Years)), 0.01),
            surf.FloorPrice(new Period(4, TimeUnit.Years), 0.01));
        Assert.Equal(surf.AtmYoYSwapRate(surf.YoYOptionDateFromTenor(new Period(4, TimeUnit.Years))),
            surf.AtmYoYSwapRate(new Period(4, TimeUnit.Years)));
    }

    [Fact]
    public void TheVolatilityCurvesRefuseTooFewOrMismatchedVolatilities()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(23, Month.November, 2007) };
        Date[] dates = [new(1, Month.August, 2007), new(23, Month.November, 2008), new(23, Month.November, 2009)];
        Assert.Equal("must have same number of dates and vols: 3 vs 2", Refusal(() => Curve(dates, [0.01, 0.02], context)));
        Assert.Equal("must have at least two dates: 1", Refusal(() => Curve(dates[..1], [0.01], context)));
        var curve = Curve(dates, [0.01, 0.009, 0.008], context);
        Assert.Equal((-1.0, 3.0), (curve.MinStrike(), curve.MaxStrike()));
        Assert.Equal(curve.Interpolator, default(Linear));
    }

    [Fact]
    public void TheStrippedSetIsAStrippedCurvePerStrike()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(23, Month.November, 2007) };
        Date[] dates = [new(1, Month.August, 2007), new(23, Month.November, 2008), new(23, Month.November, 2009)];
        var curve = Curve(dates, [0.01, 0.009, 0.008], context);
        Assert.Throws<ArgumentException>(() => new StrippedYoYOptionlets([0.01, 0.02], [curve]));
        Assert.Throws<ArgumentException>(() => new StrippedYoYOptionlets([], []));
        Assert.Throws<ArgumentNullException>(() => new StrippedYoYOptionlets([0.01], [null!]));
        var set = new StrippedYoYOptionlets([0.01, 0.02], [curve, curve]);
        Assert.Equal((0.01, 0.02), (set.MinStrike, set.MaxStrike));
        Date d = new(23, Month.May, 2009);
        Assert.Equal(curve.Volatility(d, 0.02), set.Slice(d).Volatilities[1]);
        double t = curve.TimeFromReference(new Date(1, Month.February, 2009));
        Assert.Equal(curve.Volatility(t, 0.02), set.Slice(t).Volatilities[1]);
    }

    [Fact]
    public void AnUnboundHelperRefusesItsImpliedQuote()
    {
        InflationVolatilityTests.Market m = InflationVolatilityTests.Setup();
        var helper = new YoYOptionletHelper(
            1.0, 10000.0, YoYInflationCapFloorType.Cap, new Period(3, TimeUnit.Months), new Actual365Fixed(), new TARGET(),
            0, m.YoYIndexEU, CPIInterpolationType.Flat, 0.03, 3,
            (c, ctx, v) => YoYInflationUnitDisplacedBlackCapFloorEngine.Calculate(c, ctx, m.YoYIndexEU, v, m.NominalEUR),
            m.Context);
        Assert.Equal("term structure not set", Assert.Throws<NanoQLException>(() => helper.ImpliedQuote(m.Context)).Message);
    }

    [Fact]
    public void ThePiecewiseCurvesAreQuantLibsBootstraps()
    {
        InflationVolatilityTests.Market m = InflationVolatilityTests.Setup();
        var surf = InflationVolatilityTests.SetupPriceSurface(m);
        var k = KInterpolatedYoYOptionletVolatilitySurface<Linear>.Create(
            0, new TARGET(), BusinessDayConvention.ModifiedFollowing, new Actual365Fixed(), surf.ObservationLag, surf,
            (c, ctx, v) => YoYInflationUnitDisplacedBlackCapFloorEngine.Calculate(c, ctx, m.YoYIndexEU, v, m.NominalEUR),
            new InterpolatedYoYOptionletStripper<Linear>(), -0.5, m.Context);
        var curve = (PiecewiseYoYOptionletVolatilityCurve<Linear, IterativeBootstrap, YoYInflationVolatilityTraits>)k.Stripped.Curves[0];
        Assert.Equal((default(Linear), default(IterativeBootstrap)), (curve.Interpolator, curve.Bootstrap));
        double eps = Math.Max(-0.01, 0.02) / 1000.0;
        Assert.Equal((-0.01 - eps, -0.01 + eps), (curve.MinStrike(), curve.MaxStrike()));
        Assert.Equal(curve.Times().Length, curve.Dates().Length);
        Assert.Equal(k.MaxStrike(), surf.MaxStrike);
        Assert.Equal(k.MinStrike(), surf.MinStrike);
        Assert.Equal(surf.ReferenceDate + surf.Maturities[^1], k.MaxDate);
    }

    private static string Refusal(Func<object> build) => Assert.Throws<NanoQLException>(build).Message;

    private static Matrix Row(Matrix m, int i)
    {
        var r = new Matrix(1, m.Columns);
        for (int j = 0; j < m.Columns; j++)
        {
            r[0, j] = m[i, j];
        }

        return r;
    }

    private static CPICapFloorTermPriceSurface Cpi(
        InflationCPICapFloorTests.CommonVars common,
        double[]? fStrikes = null,
        Matrix? fPrice = null,
        Matrix? cPrice = null,
        Period[]? maturities = null,
        ZeroInflationIndex? index = null) =>
        InterpolatedCPICapFloorTermPriceSurface<Bilinear>.Create(
            1.0, common.BaseZeroRate, common.ObservationLag, common.Calendar, common.Convention, common.DcZCIIS,
            index ?? common.Ii, CPIInterpolationType.Flat, common.NominalUK, common.CStrikesUK, fStrikes ?? common.FStrikesUK,
            maturities ?? common.CfMaturitiesUK, cPrice ?? common.CPriceUK, fPrice ?? common.FPriceUK, common.Context);

    private static CPICapFloor Cap(InflationCPICapFloorTests.CommonVars common, Date maturity, Period lag) =>
        new(
            OptionType.Call, 1.0, common.Context.EvaluationDate - new Period(1, TimeUnit.Years), 220.0, maturity,
            new UnitedKingdom(), BusinessDayConvention.Unadjusted, new UnitedKingdom(), BusinessDayConvention.ModifiedFollowing,
            0.03, common.Ii, lag, CPIInterpolationType.Flat);

    private static InterpolatedYoYOptionletVolatilityCurve<Linear> Curve(Date[] dates, double[] vols, EvaluationContext context) =>
        InterpolatedYoYOptionletVolatilityCurve<Linear>.Create(
            0, new TARGET(), BusinessDayConvention.ModifiedFollowing, new Actual365Fixed(), new Period(3, TimeUnit.Months),
            Frequency.Monthly, false, dates, vols, -1.0, 3.0, context);
}
