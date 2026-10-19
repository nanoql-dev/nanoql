// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/discretizedasset.hpp ql/discretizedasset.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL;

/// <summary>A discretized discount bond: a unit payment at the time the asset is initialized at.</summary>
public sealed class DiscretizedDiscountBond : DiscretizedAsset
{
    /// <summary>Initializes a new instance of the <see cref="DiscretizedDiscountBond"/> class.</summary>
    public DiscretizedDiscountBond()
    {
    }

    /// <summary>Sets every value to 1.</summary>
    /// <param name="size">The number of values.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    public override void Reset(int size) => ResetValues(size, 1.0);

    /// <summary>The bond's mandatory times: none.</summary>
    /// <returns>An empty array.</returns>
    public override ImmutableArray<double> MandatoryTimes() => [];
}
