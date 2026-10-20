// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/creditdefaultswap.hpp ql/instruments/creditdefaultswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// The model a credit default swap's implied quantities and its helpers price with: QuantLib's
/// <c>CreditDefaultSwap::PricingModel</c>, with its values.
/// </summary>
public enum CreditDefaultSwapPricingModel
{
    /// <summary>The mid-point engine.</summary>
    Midpoint = 0,

    /// <summary>The ISDA CDS Standard Model's engine.</summary>
    ISDA = 1,
}
