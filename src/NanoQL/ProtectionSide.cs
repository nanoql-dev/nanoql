// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/default.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>The side of a default-protection contract: QuantLib's <c>Protection::Side</c>, with its values.</summary>
public enum ProtectionSide
{
    /// <summary>Buys the protection: pays the premium, receives the default payment.</summary>
    Buyer = 0,

    /// <summary>Sells the protection: receives the premium, pays the default payment.</summary>
    Seller = 1,
}
