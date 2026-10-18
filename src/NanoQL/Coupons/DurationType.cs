// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2005 Charles Whitmore
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/duration.hpp ql/cashflows/duration.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>The type of a duration: QuantLib's <c>Duration::Type</c>.</summary>
/// <remarks>Its <see cref="object.ToString"/> gives QuantLib's stream text, "Simple", "Macaulay" or "Modified".</remarks>
public enum DurationType
{
    /// <summary>The simple duration.</summary>
    Simple,

    /// <summary>Macaulay's duration.</summary>
    Macaulay,

    /// <summary>The modified duration.</summary>
    Modified,
}
