// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/replication.hpp ql/cashflows/replication.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>
/// The strategy by which a digital coupon replicates its embedded digital option: QuantLib's
/// <c>Replication::Type</c>.
/// </summary>
/// <remarks>Its <see cref="object.ToString"/> gives QuantLib's stream text, "Sub", "Central" or "Super".</remarks>
public enum ReplicationType
{
    /// <summary>Sub-replication.</summary>
    Sub,

    /// <summary>Central replication.</summary>
    Central,

    /// <summary>Super-replication.</summary>
    Super,
}
