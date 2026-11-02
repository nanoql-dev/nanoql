// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/interpolatedyoyoptionletstripper.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The year-on-year optionlet stripper on linear interpolation: QuantLib-Python's
/// <c>InterpolatedYoYInflationOptionletStripper</c>.
/// </summary>
/// <remarks>
/// QuantLib-Python's <c>%template</c> name for <see cref="InterpolatedYoYOptionletStripper{TInterpolator}"/> on
/// <see cref="Linear"/>: it adds nothing.
/// </remarks>
[Experimental("NQL0001")]
public sealed class InterpolatedYoYInflationOptionletStripper : InterpolatedYoYOptionletStripper<Linear>
{
}
