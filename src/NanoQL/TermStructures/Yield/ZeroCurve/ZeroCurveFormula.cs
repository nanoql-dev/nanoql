// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/zerocurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

// InterpolatedZeroCurve's zero-rate formula and maximum date, which the ZeroYield traits carry, so the interpolated and
// the piecewise zero curves share them.
internal static class ZeroCurveFormula
{
    // InterpolatedZeroCurve::zeroYieldImpl
    public static double ZeroYieldImpl(
        ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t)
    {
        if (t <= times[^1])
        {
            return interpolation.Evaluate(t, true);
        }

        // flat fwd extrapolation
        double tMax = times[^1];
        double zMax = data[^1];
        double instFwdMax = zMax + (tMax * interpolation.Derivative(tMax));
        return ((zMax * tMax) + (instFwdMax * (t - tMax))) / t;
    }

    // InterpolatedZeroCurve::maxDate
    public static Date MaxDate(ReadOnlySpan<Date> dates, Date? maxDate) => maxDate ?? dates[^1];
}
