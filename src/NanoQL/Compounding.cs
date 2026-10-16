// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/compounding.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>Interest-rate compounding rules.</summary>
/// <remarks>
/// <see cref="SimpleThenCompounded"/> and <see cref="CompoundedThenSimple"/> express that the compounding style
/// depends on the position of the cash flow, so they should not be used on their own when that position is not
/// available from the context of a calculation.
/// </remarks>
public enum Compounding
{
    /// <summary>1 + rt.</summary>
    Simple = 0,

    /// <summary>(1 + r)^t.</summary>
    Compounded = 1,

    /// <summary>e^(rt).</summary>
    Continuous = 2,

    /// <summary>Simple up to the first period, then compounded.</summary>
    SimpleThenCompounded,

    /// <summary>Compounded up to the first period, then simple.</summary>
    CompoundedThenSimple,
}
