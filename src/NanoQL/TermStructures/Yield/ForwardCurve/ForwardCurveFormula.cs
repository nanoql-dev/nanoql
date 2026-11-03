// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/forwardcurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

// InterpolatedForwardCurve's zero-rate formula and maximum date, which the ForwardRate traits carry, so the
// interpolated and the piecewise forward curves share them.
internal static class ForwardCurveFormula
{
    // InterpolatedForwardCurve::zeroYieldImpl
    public static double ZeroYieldImpl(
        ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t)
    {
        if (t == 0.0)
        {
            return interpolation.Evaluate(t, true);
        }

        double maxTime = CurveData.Last(times, nameof(times));
        double integral;
        if (t <= maxTime)
        {
            integral = interpolation.Primitive(t, true);
        }
        else
        {
            // flat fwd extrapolation
            integral = interpolation.Primitive(maxTime, true) + (CurveData.Last(data, nameof(data)) * (t - maxTime));
        }

        return integral / t;
    }

    // InterpolatedForwardCurve::maxDate
    public static Date MaxDate(ReadOnlySpan<Date> dates, Date? maxDate) =>
        maxDate ?? CurveData.Last(dates, nameof(dates));
}
