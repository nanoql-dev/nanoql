// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/convexmonotoneinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

// QuantLib's detail::EverywhereConstantHelper: a constant section, and the extrapolation past the last node.
internal sealed class EverywhereConstantHelper(double value, double prevPrimitive, double xPrev) : SectionHelper
{
    public override double Value(double x) => value;

    public override double Primitive(double x) => prevPrimitive + ((x - xPrev) * value);

    public override double FNext() => value;
}
