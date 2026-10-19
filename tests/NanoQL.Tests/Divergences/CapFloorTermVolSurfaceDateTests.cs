// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A cap/floor term-volatility surface after the evaluation date moves. QuantLib's moving surface recomputes its option
/// dates in <c>update()</c> before its base clears the cached reference date (<c>capfloortermvolsurface.cpp:195-207</c>,
/// <c>termstructure.cpp:39-52</c>), so after a move its dates are those of the reference date the previous evaluation
/// date gave: built on 16 June 2025 with two settlement days and moved through 20, 25 and 30 June, the wheel's surface has
/// the reference date 2 July 2025 and the option dates of 27 June's (29 June 2026, 28 June 2027, 27 June 2028, 27 June
/// 2030), and its volatility at 1.5 years and 3.5% is 0.22743898630625792, at 30 June 2027 0.22282736247815785. nanoql's
/// surfaces have fixed reference dates (R1): the surface for 30 June is built under that date, and gives the values of a
/// surface QuantLib builds on it: the option dates of 2 July (2 July 2026, 2 July 2027, 3 July 2028, 2 July 2030),
/// 0.2273976824337007 and 0.22280134235888954.
/// </summary>
public class CapFloorTermVolSurfaceDateTests
{
    [Fact]
    public void ASurfaceUnderALaterEvaluationDateIsANewSurface()
    {
        Period[] tenors =
        [
            new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(3, TimeUnit.Years), new(5, TimeUnit.Years),
        ];
        double[] strikes = [0.02, 0.03, 0.04, 0.05];
        var vols = new Matrix(new[,]
        {
            { 0.30, 0.25, 0.22, 0.21 }, { 0.28, 0.24, 0.21, 0.20 }, { 0.26, 0.23, 0.20, 0.19 }, { 0.24, 0.21, 0.19, 0.18 },
        });
        CapFloorTermVolSurface SurfaceOn(Date evaluationDate) => CapFloorTermVolSurface.Create(
            2, new TARGET(), BusinessDayConvention.Following, tenors, strikes, vols,
            new EvaluationContext { EvaluationDate = evaluationDate }, new Actual365Fixed());

        // The surface QuantLib built on 16 June: its dates from 18 June.
        CapFloorTermVolSurface first = SurfaceOn(new Date(16, Month.June, 2025));
        Assert.Equal(new Date(18, Month.June, 2025), first.ReferenceDate);
        Assert.Equal([new Date(18, Month.June, 2026), new Date(18, Month.June, 2027), new Date(19, Month.June, 2028), new Date(18, Month.June, 2030)], first.OptionDates);

        // On 30 June: a new surface, whose dates and values are those of a surface QuantLib builds that day, where
        // QuantLib's moved surface keeps 27 June's dates and gives 0.22743898630625792 and 0.22282736247815785.
        CapFloorTermVolSurface moved = SurfaceOn(new Date(30, Month.June, 2025));
        Assert.Equal(new Date(2, Month.July, 2025), moved.ReferenceDate);
        Assert.Equal([new Date(2, Month.July, 2026), new Date(2, Month.July, 2027), new Date(3, Month.July, 2028), new Date(2, Month.July, 2030)], moved.OptionDates);
        Assert.Equal([1.0, 2.0, 3.0054794520547947, 5.002739726027397], moved.OptionTimes);
        Assert.Equal(0.2273976824337007, moved.Volatility(1.5, 0.035));
        Assert.Equal(0.22280134235888954, moved.Volatility(new Date(30, Month.June, 2027), 0.035));
    }
}
