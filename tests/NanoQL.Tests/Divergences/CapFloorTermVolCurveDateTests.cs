// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A cap/floor term-volatility curve after the evaluation date moves. QuantLib's moving curve recomputes its option dates
/// and times in <c>update()</c> before its base clears the cached reference date (<c>capfloortermvolcurve.cpp:155-167</c>,
/// <c>termstructure.cpp:48-52</c>), so after a move its times are those of the reference date the previous evaluation
/// date gave, and its spline is over them: built on 16 June 2025 with two settlement days, tenors 1Y, 2Y and 3Y at 0.20,
/// 0.21 and 0.22, and moved to 20 June, the wheel's curve has the reference date 24 June 2025 and the maximum date 26 June
/// 2028, and its volatility at 1.5 years is 0.20500509503283154, its value before the move. nanoql's curves have fixed
/// reference dates (R1): the curve for 20 June is built under that date, and gives the value of a curve QuantLib builds
/// on it, 0.20500761138338572.
/// </summary>
public class CapFloorTermVolCurveDateTests
{
    [Fact]
    public void ACurveUnderALaterEvaluationDateIsANewCurve()
    {
        Period[] tenors = [new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(3, TimeUnit.Years)];
        double[] vols = [0.20, 0.21, 0.22];
        CapFloorTermVolCurve CurveOn(Date evaluationDate) => CapFloorTermVolCurve.Create(
            2, new TARGET(), BusinessDayConvention.Following, tenors, vols,
            new EvaluationContext { EvaluationDate = evaluationDate }, new Actual365Fixed());

        // The curve QuantLib built on 16 June: its dates from 18 June.
        CapFloorTermVolCurve first = CurveOn(new Date(16, Month.June, 2025));
        Assert.Equal(new Date(18, Month.June, 2025), first.ReferenceDate);
        Assert.Equal(new Date(19, Month.June, 2028), first.MaxDate);
        Assert.Equal(0.20500509503283154, first.Volatility(1.5, 0.03));

        // On 20 June: a new curve, whose dates and value are those of a curve QuantLib builds that day, where QuantLib's
        // moved curve keeps its first times and gives 0.20500509503283154.
        CapFloorTermVolCurve moved = CurveOn(new Date(20, Month.June, 2025));
        Assert.Equal(new Date(24, Month.June, 2025), moved.ReferenceDate);
        Assert.Equal(new Date(26, Month.June, 2028), moved.MaxDate);
        Assert.Equal(0.20500761138338572, moved.Volatility(1.5, 0.03));
        Assert.Equal(0.21, moved.Volatility(new Period(2, TimeUnit.Years), 0.03));
    }
}
