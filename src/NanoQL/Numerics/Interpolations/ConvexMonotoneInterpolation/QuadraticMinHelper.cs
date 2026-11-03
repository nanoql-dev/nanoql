// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/convexmonotoneinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

// QuantLib's detail::QuadraticMinHelper: the section's quadratic kept non-negative. Where the parabola dips below 0
// inside the section it is split: its average raised, the parabola squeezed to the section's ends and 0 between them.
//
// Two fixes of QuantLib's split test (documented divergences):
// - QuantLib splits wherever the discriminant b² − 4ac is positive (convexmonotoneinterpolation.hpp:524), even where
//   the parabola never dips below 0 inside the section, a linear or concave piece, which breaks the section's average
//   and the forwards' continuity. Here it splits only where the parabola is convex, its vertex inside (0, 1) and the
//   discriminant positive: QuantLib's split wherever it splits correctly.
// - The averages' discriminant, 0 in exact arithmetic beside a floored end forward, rounds a hair below 0, and QuantLib
//   then takes the unsplit parabola, which dips below 0 (:528-529). A value in (−1e-12 bAv², 0) is taken as 0.
internal sealed class QuadraticMinHelper : SectionHelper
{
    private readonly bool splitRegion;
    private readonly double x1;
    private readonly double x2;
    private readonly double x3;
    private readonly double x4;
    private readonly double a;
    private readonly double b;
    private readonly double c;
    private readonly double primitive1;
    private readonly double primitive2;
    private readonly double fNext;
    private readonly double xScaling;
    private readonly double xRatio = 1.0;

    public QuadraticMinHelper(double xPrev, double xNext, double fPrev, double fNext, double fAverage, double prevPrimitive)
    {
        x1 = xPrev;
        x4 = xNext;
        primitive1 = prevPrimitive;
        this.fNext = fNext;
        a = (3 * fPrev) + (3 * fNext) - (6 * fAverage);
        b = -((4 * fPrev) + (2 * fNext) - (6 * fAverage));
        c = fPrev;
        double d = (b * b) - (4 * a * c);
        xScaling = x4 - x1;

        // the parabola is negative somewhere inside the section only if it is convex, its vertex lies inside and its
        // value there is negative
        double xv = a > 0.0 ? -b / (2 * a) : -1.0;
        if (a > 0.0 && xv > 0.0 && xv < 1.0 && d > 0)
        {
            const double AAv = 36;
            double bAv = -24 * (fPrev + fNext);
            double cAv = 4 * ((fPrev * fPrev) + (fPrev * fNext) + (fNext * fNext));
            double dAv = (bAv * bAv) - (4.0 * AAv * cAv);
            if (dAv < 0.0 && dAv > -1e-12 * bAv * bAv)
            {
                // a rounding of 0
                dAv = 0.0;
            }

            if (dAv >= 0.0)
            {
                splitRegion = true;
                double avRoot = (-bAv - Math.Sqrt(dAv)) / (2 * AAv);

                xRatio = fAverage / avRoot;
                xScaling *= xRatio;

                a = (3 * fPrev) + (3 * fNext) - (6 * avRoot);
                b = -((4 * fPrev) + (2 * fNext) - (6 * avRoot));
                c = fPrev;
                double xRoot = -b / (2 * a);
                x2 = x1 + (xRatio * (x4 - x1) * xRoot);
                x3 = x4 - (xRatio * (x4 - x1) * (1 - xRoot));
                primitive2 = primitive1 + (xScaling * ((a / 3 * xRoot * xRoot) + (b / 2 * xRoot) + c) * xRoot);
            }
        }
    }

    public override double Value(double x)
    {
        double xVal = (x - x1) / (x4 - x1);
        if (splitRegion)
        {
            if (x <= x2)
            {
                xVal /= xRatio;
            }
            else if (x < x3)
            {
                return 0.0;
            }
            else
            {
                xVal = 1.0 - ((1.0 - xVal) / xRatio);
            }
        }

        return c + (b * xVal) + (a * xVal * xVal);
    }

    public override double Primitive(double x)
    {
        double xVal = (x - x1) / (x4 - x1);
        if (splitRegion)
        {
            if (x < x2)
            {
                xVal /= xRatio;
            }
            else if (x < x3)
            {
                return primitive2;
            }
            else
            {
                xVal = 1.0 - ((1.0 - xVal) / xRatio);
            }
        }

        return primitive1 + (xScaling * ((a / 3 * xVal * xVal) + (b / 2 * xVal) + c) * xVal);
    }

    public override double FNext() => fNext;
}
