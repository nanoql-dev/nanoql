// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/convexmonotoneinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

// QuantLib's detail::ConvexMonotone2Helper: Hagan and West's region (ii), flat to eta, then quadratic.
internal sealed class ConvexMonotone2Helper(
    double xPrev, double xNext, double gPrev, double gNext, double fAverage, double eta2, double prevPrimitive)
    : SectionHelper
{
    private readonly double xScaling = xNext - xPrev;

    public override double Value(double x)
    {
        double xVal = (x - xPrev) / xScaling;
        if (xVal <= eta2)
        {
            return fAverage + gPrev;
        }
        else
        {
            return fAverage + gPrev + ((gNext - gPrev) / ((1 - eta2) * (1 - eta2)) * (xVal - eta2) * (xVal - eta2));
        }
    }

    public override double Primitive(double x)
    {
        double xVal = (x - xPrev) / xScaling;
        if (xVal <= eta2)
        {
            return prevPrimitive + (xScaling * ((fAverage * xVal) + (gPrev * xVal)));
        }
        else
        {
            return prevPrimitive + (xScaling * ((fAverage * xVal) + (gPrev * xVal)
                + ((gNext - gPrev) / ((1 - eta2) * (1 - eta2))
                    * ((1.0 / 3.0 * ((xVal * xVal * xVal) - (eta2 * eta2 * eta2))) - (eta2 * xVal * xVal)
                        + (eta2 * eta2 * xVal)))));
        }
    }

    public override double FNext() => fAverage + gNext;
}
