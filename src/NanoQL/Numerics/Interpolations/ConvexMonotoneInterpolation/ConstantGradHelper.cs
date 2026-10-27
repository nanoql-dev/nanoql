// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/convexmonotoneinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

// QuantLib's detail::ConstantGradHelper: a linear section between its end forwards, where both lie at the average.
internal sealed class ConstantGradHelper : SectionHelper
{
    private readonly double fPrev;
    private readonly double prevPrimitive;
    private readonly double xPrev;
    private readonly double fGrad;
    private readonly double fNext;

    public ConstantGradHelper(double fPrev, double prevPrimitive, double xPrev, double xNext, double fNext)
    {
        this.fPrev = fPrev;
        this.prevPrimitive = prevPrimitive;
        this.xPrev = xPrev;
        fGrad = (fNext - fPrev) / (xNext - xPrev);
        this.fNext = fNext;
    }

    public override double Value(double x) => fPrev + ((x - xPrev) * fGrad);

    public override double Primitive(double x) => prevPrimitive + ((x - xPrev) * (fPrev + (0.5 * (x - xPrev) * fGrad)));

    public override double FNext() => fNext;
}
