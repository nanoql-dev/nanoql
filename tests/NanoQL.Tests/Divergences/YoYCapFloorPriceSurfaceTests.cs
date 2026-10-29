// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.TestSuite;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The year-on-year price surface's swap-rate dates and repricing check (the S-xrt plan's XRR5 (xii) and (xiv), fixed
/// under R8). (xii): QuantLib takes a valid maturity's date as <c>cfMaturities_.at(counter)</c>, counting only the valid
/// maturities (<c>yoycapfloortermpricesurface.hpp:506-508</c>), so once an intersection fails every later date is an
/// earlier maturity's. On QuantLib 1.44 (the run's <c>c2\probes\xd_c2d.py</c> through the harness: test-suite/
/// inflationvolatility.cpp's euro market with the nominal curve's first date on the surface's reference date, observed
/// flat, its 3-year caps ten times their quotes, so the 3-year intersection isn't found) the dates are 40505, 40505,
/// 41236, 41966, 43062, 44888 and 46714, the 5-year rate on the 3-year date. nanoql's are each maturity's option date,
/// 40505 to 50367, at the times QuantLib's time list gives; the rates are QuantLib's. (xiv): QuantLib checks the curve
/// reprices its helpers at the surface's reference date plus whole years, where the helpers sit on the nominal curve's;
/// with the nominal curve's dates 91 days later it refuses, "could not reprice helper 0, data 0.0244147, implied quote
/// 0.0244373" (<c>xd_c2b.py</c>). nanoql checks each helper at its own maturity and builds the surface; where the two
/// reference dates are 4 days apart, as in QuantLib's case, the check passes either way and the values are QuantLib's.
/// </summary>
public class YoYCapFloorPriceSurfaceTests
{
    [Fact]
    public void ASwapRateIsDatedAtItsOwnMaturityAfterAFailedIntersection()
    {
        InflationVolatilityTests.Market m = InflationVolatilityTests.Setup();
        Matrix caps = m.CPriceEU.Clone();
        for (int i = 0; i < caps.Rows; i++)
        {
            caps[i, 0] *= 10.0;
        }

        YieldTermStructure nominal = Moved(m.NominalEUR, 0, firstOnEvaluationDate: true);
        var surf = Surface(m, nominal, caps);
        (var dates, var rates) = surf.AtmYoYSwapDateRates();
        (var times, _) = surf.AtmYoYSwapTimeRates();
        int[] own = [.. m.CfMaturitiesEU.Select(p => surf.YoYOptionDateFromTenor(p).SerialNumber)];
        Assert.Equal(own, dates.Select(d => d.SerialNumber));
        Assert.Equal(times, dates.Select(surf.TimeFromReference));
        Assert.Equal([40505, 40505, 41236, 41966, 43062, 44888, 46714], (int[])[own[0], .. own[..^1]]);
        double[] quantLib =
        [
            0.02014701349747696, 0.02475752056067443, 0.024939600099273256, 0.02525957610999954, 0.02584977670587058,
            0.026288266145827632, 0.0267915254966316,
        ];
        for (int i = 0; i < quantLib.Length; i++)
        {
            Assert.Equal(quantLib[i], rates[i], 1e-12);
        }
    }

    [Fact]
    public void ANominalCurveDatedLaterStillRepricesItsHelpers()
    {
        InflationVolatilityTests.Market m = InflationVolatilityTests.Setup();
        YieldTermStructure nominal = Moved(m.NominalEUR, 91);
        var surf = Surface(m, nominal, m.CPriceEU, CPIInterpolationType.Linear);
        Date maturity = nominal.ReferenceDate + new Period(1, TimeUnit.Years);
        var helper = new YearOnYearInflationSwapHelper(
            surf.AtmYoYSwapRate(maturity), surf.ObservationLag, maturity, surf.Calendar, surf.BusinessDayConvention,
            surf.DayCounter, m.YoYIndexEU, CPIInterpolationType.Linear, nominal, m.Context);
        double implied = helper.SetTermStructure(surf.YoYTS()).ImpliedQuote(m.Context);
        Assert.True(Math.Abs(implied - surf.AtmYoYSwapRate(maturity)) < 1e-10);

        // QuantLib's implied quote of helper 0, and its "data", the rate a year after the surface's reference date
        Assert.True(Math.Abs(implied - 0.0244373) < 5e-8);
        Assert.True(Math.Abs(surf.AtmYoYSwapRate(surf.ReferenceDate + new Period(1, TimeUnit.Years)) - 0.0244147) < 5e-8);
    }

    // The euro nominal curve with its dates moved, its first date on the evaluation date if asked.
    private static YieldTermStructure Moved(YieldTermStructure nominal, int days, bool firstOnEvaluationDate = false)
    {
        var curve = (InterpolatedZeroCurve<Cubic>)nominal;
        Date[] dates = [.. curve.Dates().Select(d => d + days)];
        if (firstOnEvaluationDate)
        {
            dates[0] = new Date(23, Month.November, 2007);
        }

        return new InterpolatedZeroCurve<Cubic>(dates, curve.ZeroRates(), new Actual365Fixed());
    }

    private static InterpolatedYoYCapFloorTermPriceSurface<Bicubic, Cubic> Surface(
        InflationVolatilityTests.Market m,
        YieldTermStructure nominal,
        Matrix caps,
        CPIInterpolationType interpolation = CPIInterpolationType.Flat) =>
        InterpolatedYoYCapFloorTermPriceSurface<Bicubic, Cubic>.Create(
            0, new Period(3, TimeUnit.Months), m.YoYIndexEU, interpolation, nominal, new Actual365Fixed(), new TARGET(),
            BusinessDayConvention.ModifiedFollowing, m.CStrikesEU, m.FStrikesEU, m.CfMaturitiesEU, caps, m.FPriceEU,
            m.Context);
}
