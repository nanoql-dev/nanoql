// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/convexmonotoneinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

// QuantLib's detail::ConvexMonotone4Helper: Hagan and West's region (iv), two quadratics meeting at eta.
internal sealed class ConvexMonotone4Helper(
    double xPrev, double xNext, double gPrev, double gNext, double fAverage, double eta4, double prevPrimitive)
    : SectionHelper
{
    private readonly double xScaling = xNext - xPrev;
    private readonly double a = InitialA(gPrev, gNext, eta4);

    // QuantLib's A_ as its constructor sets it, which ConvexMonotone4MinHelper reads before it splits.
    internal static double InitialA(double gPrev, double gNext, double eta4) =>
        -0.5 * ((eta4 * gPrev) + ((1 - eta4) * gNext));

    public override double Value(double x)
    {
        double xVal = (x - xPrev) / xScaling;
        if (xVal <= eta4)
        {
            return fAverage + a + ((gPrev - a) * (eta4 - xVal) * (eta4 - xVal) / (eta4 * eta4));
        }
        else
        {
            return fAverage + a + ((gNext - a) * (xVal - eta4) * (xVal - eta4) / ((1 - eta4) * (1 - eta4)));
        }
    }

    public override double Primitive(double x)
    {
        double xVal = (x - xPrev) / xScaling;
        double retVal;
        if (xVal <= eta4)
        {
            retVal = prevPrimitive + (xScaling * (fAverage + a + ((gPrev - a) / (eta4 * eta4)
                * ((eta4 * eta4) - (eta4 * xVal) + (1.0 / 3.0 * xVal * xVal)))) * xVal);
        }
        else
        {
            retVal = prevPrimitive + (xScaling * ((fAverage * xVal) + (a * xVal) + ((gPrev - a) * (1.0 / 3.0 * eta4))
                + ((gNext - a) / ((1 - eta4) * (1 - eta4))
                    * ((1.0 / 3.0 * xVal * xVal * xVal) - (eta4 * xVal * xVal) + (eta4 * eta4 * xVal)
                        - (1.0 / 3.0 * eta4 * eta4 * eta4)))));
        }

        return retVal;
    }

    public override double FNext() => fAverage + gNext;
}
