// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/convexmonotoneinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

// QuantLib's detail::QuadraticHelper: the quadratic through the section's end forwards with the section's average.
internal sealed class QuadraticHelper : SectionHelper
{
    private readonly double xPrev;
    private readonly double fNext;
    private readonly double prevPrimitive;
    private readonly double xScaling;
    private readonly double a;
    private readonly double b;
    private readonly double c;

    public QuadraticHelper(double xPrev, double xNext, double fPrev, double fNext, double fAverage, double prevPrimitive)
    {
        this.xPrev = xPrev;
        this.fNext = fNext;
        this.prevPrimitive = prevPrimitive;
        a = (3 * fPrev) + (3 * fNext) - (6 * fAverage);
        b = -((4 * fPrev) + (2 * fNext) - (6 * fAverage));
        c = fPrev;
        xScaling = xNext - xPrev;
    }

    public override double Value(double x)
    {
        double xVal = (x - xPrev) / xScaling;
        return (a * xVal * xVal) + (b * xVal) + c;
    }

    public override double Primitive(double x)
    {
        double xVal = (x - xPrev) / xScaling;
        return prevPrimitive + (xScaling * ((a / 3 * xVal * xVal) + (b / 2 * xVal) + c) * xVal);
    }

    public override double FNext() => fNext;
}
