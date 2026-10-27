// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/convexmonotoneinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

// QuantLib's detail::SectionHelper: the convex-monotone interpolation's value and primitive over one section, and the
// forward at its right end. Immutable; a later interpolation over more points reuses the earlier sections' helpers.
internal abstract class SectionHelper
{
    public abstract double Value(double x);

    public abstract double Primitive(double x);

    public abstract double FNext();
}
