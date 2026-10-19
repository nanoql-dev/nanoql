// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2013, 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/model.hpp ql/models/model.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>The abstract short-rate model class: a calibrated model that builds a lattice on a time grid.</summary>
/// <remarks>An extension point, as in QuantLib: the one-factor models derive from it, as later ports' two-factor models will.</remarks>
public abstract class ShortRateModel : CalibratedModel
{
    /// <summary>Initializes a new instance of the <see cref="ShortRateModel"/> class.</summary>
    /// <param name="nArguments">The number of arguments.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nArguments"/> is negative.</exception>
    protected ShortRateModel(int nArguments)
        : base(nArguments)
    {
    }

    /// <summary>Builds the model's lattice on a time grid.</summary>
    /// <param name="grid">The time grid.</param>
    /// <returns>The lattice, immutable once returned.</returns>
    public abstract Lattice Tree(TimeGrid grid);
}
