// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/fdblackscholesvanillaengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>
/// How the finite-difference Black-Scholes engine models cash dividends: QuantLib's
/// <c>FdBlackScholesVanillaEngine::CashDividendModel</c>, whose values it takes from the cash-dividend European
/// engine's (the S-eq plan's SEQD4).
/// </summary>
public enum FdBlackScholesVanillaEngineCashDividendModel
{
    /// <summary>Each dividend drops the spot when it is paid.</summary>
    Spot = 0,

    /// <summary>The dividends' value is escrowed from the spot until they are paid.</summary>
    Escrowed = 1,
}
