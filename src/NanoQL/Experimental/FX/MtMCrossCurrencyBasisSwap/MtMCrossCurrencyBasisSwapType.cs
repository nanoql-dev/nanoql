// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/fx/mtmcrosscurrencybasisswap.hpp ql/experimental/fx/mtmcrosscurrencybasisswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// Whether a mark-to-market cross-currency basis swap pays or receives its FX base-currency leg: QuantLib's
/// <c>MtMCrossCurrencyBasisSwap::Type</c>.
/// </summary>
[Experimental("NQL0001")]
public enum MtMCrossCurrencyBasisSwapType
{
    /// <summary>Pays the FX base-currency leg.</summary>
    PayFxBaseCurrency,

    /// <summary>Receives the FX base-currency leg.</summary>
    ReceiveFxBaseCurrency,
}
