// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/convexmonotoneinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

// QuantLib's detail::ConvexMonotone4MinHelper: region (iv) kept non-negative. Where the two quadratics would dip to 0
// or below, the section is split: its average shifted up, the quadratics squeezed to its ends and 0 between them.
// QuantLib derives it from ConvexMonotone4Helper and overwrites the base's members in its constructor; here it holds
// the base form for an unsplit section and its own shifted values for a split one.
internal sealed class ConvexMonotone4MinHelper : SectionHelper
{
    private readonly ConvexMonotone4Helper? unsplit;
    private readonly double xPrev;
    private readonly double xScaling;
    private readonly double gPrev;
    private readonly double gNext;
    private readonly double fAverage;
    private readonly double eta4;
    private readonly double prevPrimitive;
    private readonly double a;
    private readonly double xRatio;
    private readonly double x2;
    private readonly double x3;

    public ConvexMonotone4MinHelper(
        double xPrev, double xNext, double gPrev, double gNext, double fAverage, double eta4, double prevPrimitive)
    {
        this.xPrev = xPrev;
        xScaling = xNext - xPrev;
        this.eta4 = eta4;
        this.prevPrimitive = prevPrimitive;
        double a0 = ConvexMonotone4Helper.InitialA(gPrev, gNext, eta4);
        if (a0 + fAverage <= 0.0)
        {
            double fPrev = gPrev + fAverage;
            double fNext = gNext + fAverage;
            double reqdShift = (((eta4 * fPrev) + ((1 - eta4) * fNext)) / 3.0) - fAverage;
            double reqdPeriod = reqdShift * xScaling / (fAverage + reqdShift);
            double xAdjust = xScaling - reqdPeriod;
            xRatio = xAdjust / xScaling;

            this.fAverage = fAverage + reqdShift;
            this.gNext = fNext - this.fAverage;
            this.gPrev = fPrev - this.fAverage;
            a = -((eta4 * this.gPrev) + ((1.0 - eta4) * this.gNext)) / 2.0;
            x2 = xPrev + (xAdjust * eta4);
            x3 = xPrev + xScaling - (xAdjust * (1.0 - eta4));
        }
        else
        {
            unsplit = new ConvexMonotone4Helper(xPrev, xNext, gPrev, gNext, fAverage, eta4, prevPrimitive);
            this.fAverage = fAverage;
            this.gNext = gNext;
        }
    }

    public override double Value(double x)
    {
        if (unsplit is not null)
        {
            return unsplit.Value(x);
        }

        double xVal = (x - xPrev) / xScaling;
        if (x <= x2)
        {
            xVal /= xRatio;
            return fAverage + a + ((gPrev - a) * (eta4 - xVal) * (eta4 - xVal) / (eta4 * eta4));
        }
        else if (x < x3)
        {
            return 0.0;
        }
        else
        {
            xVal = 1.0 - ((1.0 - xVal) / xRatio);
            return fAverage + a + ((gNext - a) * (xVal - eta4) * (xVal - eta4) / ((1 - eta4) * (1 - eta4)));
        }
    }

    public override double Primitive(double x)
    {
        if (unsplit is not null)
        {
            return unsplit.Primitive(x);
        }

        double xVal = (x - xPrev) / xScaling;
        if (x <= x2)
        {
            xVal /= xRatio;
            return prevPrimitive + (xScaling * xRatio * (fAverage + a + ((gPrev - a) / (eta4 * eta4)
                * ((eta4 * eta4) - (eta4 * xVal) + (1.0 / 3.0 * xVal * xVal)))) * xVal);
        }
        else if (x <= x3)
        {
            return prevPrimitive + (xScaling * xRatio * ((fAverage * eta4) + (a * eta4)
                + ((gPrev - a) / (eta4 * eta4) * (1.0 / 3.0 * eta4 * eta4 * eta4))));
        }
        else
        {
            xVal = 1.0 - ((1.0 - xVal) / xRatio);
            return prevPrimitive + (xScaling * xRatio * ((fAverage * xVal) + (a * xVal) + ((gPrev - a) * (1.0 / 3.0 * eta4))
                + ((gNext - a) / ((1.0 - eta4) * (1.0 - eta4))
                    * ((1.0 / 3.0 * xVal * xVal * xVal) - (eta4 * xVal * xVal) + (eta4 * eta4 * xVal)
                        - (1.0 / 3.0 * eta4 * eta4 * eta4)))));
        }
    }

    // The base's fNext, at the split's shifted average where it splits: QuantLib's inherited fAverage_ + gNext_.
    public override double FNext() => fAverage + gNext;
}
