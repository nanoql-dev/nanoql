// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/stubiborcoupon.hpp ql/cashflows/stubiborcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;

namespace NanoQL.Coupons;

/// <summary>The index selection for irregular Ibor coupons.</summary>
/// <remarks>
/// <para>
/// An empty selection, which the parameterless constructor builds, means broken periods fix on the leg's own index as
/// usual. A non-empty one supplies the candidate indices used instead, as fully built objects, so that each uses its
/// own conventions, stored fixings and forwarding curve.
/// </para>
/// <para>
/// For <see cref="StubIndexSelectionConvention.ClosestIndex"/>, the index whose maturity is closest to the coupon's end
/// date is used. For <see cref="StubIndexSelectionConvention.Interpolated"/>, the two index maturities bracketing the
/// coupon's end date are used for linear interpolation in calendar days. The weights depend on the accrual dates.
/// </para>
/// </remarks>
public sealed class StubIndexSelection
{
    /// <summary>Initializes a new instance of the <see cref="StubIndexSelection"/> class, empty: broken periods fix on
    /// the leg's own index.</summary>
    public StubIndexSelection()
    {
        Convention = StubIndexSelectionConvention.ClosestIndex;
        Indices = [];
    }

    /// <summary>Initializes a new instance of the <see cref="StubIndexSelection"/> class with candidate indices.</summary>
    /// <param name="convention">The convention selecting among the candidates.</param>
    /// <param name="indices">The candidate indices.</param>
    /// <exception cref="ArgumentException">No candidates are given, or one is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="convention"/> isn't a member of its type.</exception>
    public StubIndexSelection(StubIndexSelectionConvention convention, IEnumerable<IborIndex> indices)
    {
        EnumArguments.Check(convention, nameof(convention));
        ArgumentNullException.ThrowIfNull(indices);
        ImmutableArray<IborIndex> candidates = [.. indices];
        if (candidates.IsEmpty)
        {
            throw new ArgumentException("no candidate indices provided for stub index selection", nameof(indices));
        }

        foreach (IborIndex candidate in candidates)
        {
            if (candidate is null)
            {
                throw new ArgumentException("null candidate index for stub index selection", nameof(indices));
            }
        }

        Convention = convention;
        Indices = candidates;
    }

    /// <summary>Gets a value indicating whether the selection is empty, so broken periods fix on the leg's index.</summary>
    public bool Empty => Indices.IsEmpty;

    /// <summary>Gets the convention selecting among the candidates.</summary>
    public StubIndexSelectionConvention Convention { get; }

    /// <summary>Gets the candidate indices.</summary>
    public ImmutableArray<IborIndex> Indices { get; }
}
