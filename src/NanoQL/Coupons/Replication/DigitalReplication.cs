// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/replication.hpp ql/cashflows/replication.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>
/// The replication strategy used to price the digital option embedded in a digital coupon, and the gap of the call
/// spread that replicates it.
/// </summary>
public sealed class DigitalReplication
{
    /// <summary>Initializes a new instance of the <see cref="DigitalReplication"/> class.</summary>
    /// <param name="t">The replication strategy.</param>
    /// <param name="gap">The gap.</param>
    /// <exception cref="ArgumentOutOfRangeException">The strategy isn't a member of its enum.</exception>
    public DigitalReplication(ReplicationType t = ReplicationType.Central, double gap = 1e-4)
    {
        EnumArguments.Check(t, nameof(t));
        Gap = gap;
        ReplicationType = t;
    }

    /// <summary>Gets the replication strategy.</summary>
    public ReplicationType ReplicationType { get; }

    /// <summary>Gets the gap.</summary>
    public double Gap { get; }
}
