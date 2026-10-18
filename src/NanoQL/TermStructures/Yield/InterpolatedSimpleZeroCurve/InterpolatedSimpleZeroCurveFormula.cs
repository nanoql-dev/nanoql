// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2019 SoftSolutions! S.r.l.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/interpolatedsimplezerocurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: InterpolatedSimpleZeroCurve, its constructors and inspectors, and the named curve on it, which S5's second commit ports

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

// InterpolatedSimpleZeroCurve's discount formula and maximum date, which the SimpleZeroYield traits carry, so the
// interpolated and the piecewise simple zero curves share them.
internal static class InterpolatedSimpleZeroCurveFormula
{
    // InterpolatedSimpleZeroCurve::discountImpl
    public static double DiscountImpl(
        ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t)
    {
        double r;
        if (t <= times[^1])
        {
            r = interpolation.Evaluate(t, true);
        }
        else
        {
            // flat fwd extrapolation after last pillar,
            // Notice that bbg uses flat extrapolation of non-annualized zero instead
            double tMax = times[^1];
            double zMax = data[^1];
            double instFwdMax = zMax + (tMax * interpolation.Derivative(tMax));
            r = ((zMax * tMax) + (instFwdMax * (t - tMax))) / t;
        }

        return 1.0 / (1.0 + (r * t));
    }

    // InterpolatedSimpleZeroCurve::maxDate: the last date, whatever a bootstrap's helpers need
    public static Date MaxDate(ReadOnlySpan<Date> dates, Date? maxDate) => dates[^1];
}
