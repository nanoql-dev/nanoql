// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/convexmonotoneinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

// QuantLib's detail::ComboHelper: a section's quadratic and convex-monotone forms weighted by the quadraticity. Its
// check that the quadraticity lies in (0, 1) holds by construction: the interpolation builds one only there.
internal sealed class ComboHelper(SectionHelper quadraticHelper, SectionHelper convMonoHelper, double quadraticity)
    : SectionHelper
{
    public override double Value(double x) =>
        (quadraticity * quadraticHelper.Value(x)) + ((1.0 - quadraticity) * convMonoHelper.Value(x));

    public override double Primitive(double x) =>
        (quadraticity * quadraticHelper.Primitive(x)) + ((1.0 - quadraticity) * convMonoHelper.Primitive(x));

    public override double FNext() =>
        (quadraticity * quadraticHelper.FNext()) + ((1.0 - quadraticity) * convMonoHelper.FNext());
}
