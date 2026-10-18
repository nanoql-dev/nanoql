// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Decillion Pty(Ltd)
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/discountcurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

// InterpolatedDiscountCurve's discount formula and maximum date, which the Discount traits carry, so the interpolated
// and the piecewise discount curves share them.
internal static class DiscountCurveFormula
{
    // InterpolatedDiscountCurve::discountImpl
    public static double DiscountImpl(
        ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t)
    {
        if (t <= times[^1])
        {
            return interpolation.Evaluate(t, true);
        }

        // flat fwd extrapolation
        double tMax = times[^1];
        double dMax = data[^1];
        double instFwdMax = -interpolation.Derivative(tMax) / dMax;
        return dMax * Math.Exp(-instFwdMax * (t - tMax));
    }

    // InterpolatedDiscountCurve::maxDate
    public static Date MaxDate(ReadOnlySpan<Date> dates, Date? maxDate) => maxDate ?? dates[^1];
}
