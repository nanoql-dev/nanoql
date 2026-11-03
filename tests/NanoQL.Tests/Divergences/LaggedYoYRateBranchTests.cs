// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="CPI.LaggedYoYRate"/> on a ratio index under linear interpolation. QuantLib interpolates the underlying's
/// fixings and takes their ratio only when <c>!index-&gt;needsForecast(date)</c>, tested on the unlagged date
/// (<c>inflationindex.cpp:83</c>), though the fixings that branch reads are those of the lagged date's period and the
/// next; otherwise it interpolates the ratios. So a fixing it never reads changes the rate it returns: with UK RPI
/// fixings to June 2025 and the evaluation date 20 August 2025, the rate for 15 July 2025 with a three-month lag is
/// 0.035461466318187446 on the wheel, and 0.03545866305332557 once July's fixing is stored, a change of
/// -2.8032648618780964e-06 (QuantLib's defect). nanoql asks whether the latest fixing the ratio branch reads needs a
/// forecast (the next period's first day, or the lagged period's own when the date starts its period), so the rate is
/// 0.03545866305332557 with or without July's fixing: QuantLib's own answer with that fixing added, and its
/// interpolate-then-divide value from the same April and May fixings. The other face: where the lag is shorter than the
/// index's period, the unlagged date's fixing can be published while the next period's, which the ratio branch reads,
/// isn't. QuantLib then takes the ratio branch and forecasts the underlying index, refusing without its zero curve,
/// against its own comment that "forecasts need to be done through the YoY forecast curve, and not the underlying
/// index" (<c>:84-90</c>): with UK RPI fixings to April 2024, the evaluation date 15 June 2024 and a YoY curve flat at
/// 2%, the rate for 26 April 2024 with no lag throws "empty Handle cannot be dereferenced" on the wheel. nanoql
/// interpolates the ratios through the YoY curve, 0.02218077895395275; on 1 April, which starts its period, both give
/// 0.033084673723716485.
/// </summary>
public class LaggedYoYRateBranchTests
{
    [Fact]
    public void AFixingTheRatioBranchDoesntReadChangesNothing()
    {
        var rpi = new UKRPI();
        var context = new EvaluationContext { EvaluationDate = new Date(20, Month.August, 2025) };
        double v = 300.0;
        var d = new Date(1, Month.January, 2023);
        while (d <= new Date(1, Month.June, 2025))
        {
            context = context with { Fixings = context.Fixings.AddFixing(rpi, d, v) };
            v *= 1.0 + (0.0007 * ((int)d.Month % 5)) + (0.0011 * (d.Year % 3));
            d += new Period(1, TimeUnit.Months);
        }

        var yy = new YoYInflationIndex(rpi);
        var date = new Date(15, Month.July, 2025);
        var lag = new Period(3, TimeUnit.Months);

        double before = CPI.LaggedYoYRate(yy, date, lag, CPIInterpolationType.Linear, context);
        EvaluationContext withJuly = context with { Fixings = context.Fixings.AddFixing(rpi, new Date(1, Month.July, 2025), v) };
        double after = CPI.LaggedYoYRate(yy, date, lag, CPIInterpolationType.Linear, withJuly);

        // QuantLib's: 0.035461466318187446 before July's fixing, 0.03545866305332557 after
        Assert.Equal(0.03545866305332557, before);
        Assert.Equal(0.03545866305332557, after);
        Assert.True(yy.NeedsForecast(date, context));
    }

    [Fact]
    public void ALagShorterThanThePeriodForecastsThroughTheYoYCurve()
    {
        var today = new Date(15, Month.June, 2024);
        var rpi = new UKRPI();
        var context = new EvaluationContext { EvaluationDate = today };
        double v = 300.0;
        var d = new Date(1, Month.January, 2023);
        while (d <= new Date(1, Month.April, 2024))
        {
            context = context with { Fixings = context.Fixings.AddFixing(rpi, d, v) };
            v *= 1.0 + (0.0007 * ((int)d.Month % 5)) + (0.0011 * (d.Year % 3));
            d += new Period(1, TimeUnit.Months);
        }

        var curve = new YoYInflationCurve(
            today,
            [new Date(1, Month.May, 2024), new Date(1, Month.May, 2030)],
            [0.02, 0.02],
            Frequency.Monthly,
            new Actual365Fixed());
        var yy = new YoYInflationIndex(rpi, curve);
        var noLag = new Period(0, TimeUnit.Months);
        var date = new Date(26, Month.April, 2024);

        // QuantLib's: "empty Handle cannot be dereferenced", the underlying UK RPI having no curve
        Assert.False(yy.NeedsForecast(date, context));
        Assert.Equal(0.02218077895395275, CPI.LaggedYoYRate(yy, date, noLag, CPIInterpolationType.Linear, context));

        // QuantLib's too, at the start of the period
        Assert.Equal(
            0.033084673723716485,
            CPI.LaggedYoYRate(yy, new Date(1, Month.April, 2024), noLag, CPIInterpolationType.Linear, context));
    }
}
