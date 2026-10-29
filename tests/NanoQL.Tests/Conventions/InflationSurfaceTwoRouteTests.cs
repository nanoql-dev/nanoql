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
/// Two routes to the inflation surfaces' values (the S-xrt plan's §2.5): the CPI engine against its price surface read
/// at the effective maturity's inflation periods; and each strike's stripped year-on-year curve repricing the price
/// surface's caps or floors, through the stripper's own helpers.
/// </summary>
public class InflationSurfaceTwoRouteTests
{
    [Theory]
    [InlineData(CPIInterpolationType.Flat, 3)]
    [InlineData(CPIInterpolationType.Flat, 10)]
    [InlineData(CPIInterpolationType.Linear, 5)]
    [InlineData(CPIInterpolationType.Linear, 7)]
    public void TheCPIEnginePricesItsSurfaceAtTheEffectiveMaturity(CPIInterpolationType interpolation, int years)
    {
        var common = new InflationCPICapFloorTests.CommonVars();
        CPICapFloorTermPriceSurface surf = InterpolatedCPICapFloorTermPriceSurface<Bilinear>.Create(
            1.0, common.BaseZeroRate, common.ObservationLag, common.Calendar, common.Convention, common.DcZCIIS,
            common.Ii, CPIInterpolationType.Flat, common.NominalUK, common.CStrikesUK, common.FStrikesUK,
            common.CfMaturitiesUK, common.CPriceUK, common.FPriceUK, common.Context);
        Date start = common.Context.EvaluationDate;
        var lag = new Period(3, TimeUnit.Months);
        var cap = new CPICapFloor(
            OptionType.Call, 1.0, start, 220.0, start + new Period(years, TimeUnit.Years) + 10, new UnitedKingdom(),
            BusinessDayConvention.Unadjusted, new UnitedKingdom(), BusinessDayConvention.ModifiedFollowing, 0.035,
            common.Ii, lag, interpolation);
        double npv = InterpolatingCPICapFloorEngine.Calculate(cap, common.Context, surf).NPV!.Value;

        Date effective = cap.PayDate() - (lag - surf.ObservationLag);
        (Date first, Date last) = InflationTermStructureFunctions.InflationPeriod(effective, common.Ii.Frequency);
        double atStart = surf.CapPrice(first, 0.035);
        double expected = interpolation == CPIInterpolationType.Flat
            ? atStart
            : atStart + ((surf.CapPrice(last + 1, 0.035) - atStart) * (effective - first) / (last + 1 - first));
        Assert.Equal(expected, npv, 1e-15);
    }

    [Fact]
    public void EachStrippedCurveRepricesTheSurfacesQuotes()
    {
        InflationVolatilityTests.Market m = InflationVolatilityTests.Setup();
        var surf = InflationVolatilityTests.SetupPriceSurface(m);
        Func<YoYInflationCapFloor, EvaluationContext, YoYOptionletVolatilitySurface, InstrumentResults> engine =
            (c, ctx, v) => YoYInflationUnitDisplacedBlackCapFloorEngine.Calculate(c, ctx, m.YoYIndexEU, v, m.NominalEUR);
        StrippedYoYOptionlets stripped = new InterpolatedYoYOptionletStripper<Linear>().Strip(surf, engine, -0.5, m.Context);

        // the stripper's generic index and helpers, on each strike's curve
        var anIndex = new YYGenericCPI(surf.Frequency, false, surf.ObservationLag, m.YoYIndexEU.Currency, surf.YoYTS());
        double maxFloor = surf.FloorStrikes[^1];
        for (int i = 0; i < stripped.Strikes.Length; i++)
        {
            double k = stripped.Strikes[i];
            YoYInflationCapFloorType type = k > maxFloor ? YoYInflationCapFloorType.Cap : YoYInflationCapFloorType.Floor;
            foreach (Period tp in surf.Maturities)
            {
                double quote = type == YoYInflationCapFloorType.Cap ? surf.CapPrice(tp, k) : surf.FloorPrice(tp, k);
                int nT = (int)Math.Floor(surf.TimeFromReference(surf.YoYOptionDateFromTenor(tp)) + 0.5);
                var helper = new YoYOptionletHelper(
                    quote, 10000, type, surf.ObservationLag, surf.DayCounter, surf.Calendar!, surf.FixingDays, anIndex,
                    CPIInterpolationType.Flat, k, nT, engine, m.Context);
                double repriced = helper.SetTermStructure(stripped.Curves[i]).ImpliedQuote(m.Context);
                Assert.True(Math.Abs(repriced - quote) <= 1e-8 * Math.Max(1.0, quote), $"{k} at {tp}: {repriced} against {quote}");
            }
        }
    }
}
