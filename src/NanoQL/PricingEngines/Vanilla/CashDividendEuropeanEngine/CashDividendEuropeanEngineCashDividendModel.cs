// SPDX-FileCopyrightText: Copyright (C) 2025 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/cashdividendeuropeanengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>
/// The cash-dividend engine's model of the dividends: QuantLib's <c>CashDividendEuropeanEngine::CashDividendModel</c>.
/// </summary>
public enum CashDividendEuropeanEngineCashDividendModel
{
    /// <summary>The dividends drop the spot, the option priced as a basket of the spot and the dividends.</summary>
    Spot,

    /// <summary>The dividends escrowed from the spot, the option priced by the analytic dividend engine.</summary>
    Escrowed,
}
