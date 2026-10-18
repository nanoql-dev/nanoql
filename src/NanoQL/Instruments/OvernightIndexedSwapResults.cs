// SPDX-FileCopyrightText: Copyright (C) 2009 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2017 Joseph Jeisman
// SPDX-FileCopyrightText: Copyright (C) 2017 Fabrice Lecuyer
// SPDX-FileCopyrightText: Copyright (C) 2026 Sergio Araujo
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/overnightindexedswap.hpp ql/instruments/overnightindexedswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// The results an engine returns for an overnight-indexed swap: a fixed-vs-floating swap's, with QuantLib's
/// overnight-leg accessors.
/// </summary>
/// <remarks>
/// QuantLib's overnight-indexed swap has accessors but no results class of its own, so this record holds no state of
/// its own: its overnight leg is the floating leg. Unsealed, as the results records of QuantLib's chain are.
/// Equality is <see cref="FixedVsFloatingSwapResults"/>'s.
/// </remarks>
public record OvernightIndexedSwapResults : FixedVsFloatingSwapResults
{
    /// <summary>Initializes a new instance of the <see cref="OvernightIndexedSwapResults"/> class.</summary>
    public OvernightIndexedSwapResults()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OvernightIndexedSwapResults"/> class with a fixed-vs-floating
    /// swap's results.
    /// </summary>
    /// <param name="results">The results.</param>
    internal OvernightIndexedSwapResults(FixedVsFloatingSwapResults results)
        : base(results)
    {
    }

    /// <summary>Gets the overnight leg's basis-point sensitivity, signed by whether it is paid.</summary>
    public double? OvernightLegBPS => FloatingLegBPS;

    /// <summary>Gets the overnight leg's NPV, signed by whether it is paid.</summary>
    public double? OvernightLegNPV => FloatingLegNPV;

    /// <inheritdoc/>
    public virtual bool Equals(OvernightIndexedSwapResults? other) => base.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => base.GetHashCode();
}
