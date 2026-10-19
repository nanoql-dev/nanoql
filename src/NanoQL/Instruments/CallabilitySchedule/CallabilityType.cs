// SPDX-FileCopyrightText: Copyright (C) 2005 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Theo Boafo
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/callabilityschedule.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>The type of a callability: QuantLib's <c>Callability::Type</c>.</summary>
/// <remarks>QuantLib has no stream operator for it, so the member names are its only text.</remarks>
public enum CallabilityType
{
    /// <summary>A call: the issuer may redeem the bond at the callability's price.</summary>
    Call = 0,

    /// <summary>A put: the holder may sell the bond back at the callability's price.</summary>
    Put = 1,
}
