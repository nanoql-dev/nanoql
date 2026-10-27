// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/convexmonotoneinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

// QuantLib's detail::ConvexMonotone3Helper: Hagan and West's region (iii), quadratic to eta, then flat.
internal sealed class ConvexMonotone3Helper(
    double xPrev, double xNext, double gPrev, double gNext, double fAverage, double eta3, double prevPrimitive)
    : SectionHelper
{
    private readonly double xScaling = xNext - xPrev;

    public override double Value(double x)
    {
        double xVal = (x - xPrev) / xScaling;
        if (xVal <= eta3)
        {
            return fAverage + gNext + ((gPrev - gNext) / (eta3 * eta3) * (eta3 - xVal) * (eta3 - xVal));
        }
        else
        {
            return fAverage + gNext;
        }
    }

    public override double Primitive(double x)
    {
        double xVal = (x - xPrev) / xScaling;
        if (xVal <= eta3)
        {
            return prevPrimitive + (xScaling * ((fAverage * xVal) + (gNext * xVal)
                + ((gPrev - gNext) / (eta3 * eta3)
                    * ((1.0 / 3.0 * xVal * xVal * xVal) - (eta3 * xVal * xVal) + (eta3 * eta3 * xVal)))));
        }
        else
        {
            return prevPrimitive + (xScaling * ((fAverage * xVal) + (gNext * xVal)
                + ((gPrev - gNext) / (eta3 * eta3) * (1.0 / 3.0 * eta3 * eta3 * eta3))));
        }
    }

    public override double FNext() => fAverage + gNext;
}
