// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/credit/isdacdsengine.hpp ql/pricingengines/credit/isdacdsengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>
/// Whether the ISDA engine's default accrual keeps the half-day error term: QuantLib's
/// <c>IsdaCdsEngine::AccrualBias</c>, with its values.
/// </summary>
public enum IsdaCdsEngineAccrualBias
{
    /// <summary>The OpenGamma paper's formula (50) with its second, error, term: the standard model before
    /// 1.8.2.</summary>
    HalfDayBias = 0,

    /// <summary>The formula without the second term: the standard model from 1.8.2.</summary>
    NoBias = 1,
}
