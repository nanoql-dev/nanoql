// SPDX-FileCopyrightText: Copyright (C) 2024 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/operatorsplittingspreadengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>
/// The operator-splitting engine's order of approximation: QuantLib's <c>OperatorSplittingSpreadEngine::Order</c>.
/// </summary>
public enum OperatorSplittingSpreadEngineOrder
{
    /// <summary>Kirk's price with the first-order correction.</summary>
    First,

    /// <summary>Kirk's price with the first- and second-order corrections.</summary>
    Second,
}
