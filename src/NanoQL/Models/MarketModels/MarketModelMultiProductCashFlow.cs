// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/multiproduct.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>
/// A cash flow a <see cref="MarketModelMultiProduct"/> generates at a step: its amount and the index of its payment
/// time in the product's <see cref="MarketModelMultiProduct.PossibleCashFlowTimes"/>. QuantLib's nested
/// <c>MarketModelMultiProduct::CashFlow</c> (R2's top-level name).
/// </summary>
/// <remarks>
/// A mutable struct: a product writes it in place, into the caller's buffer (the S-mmp plan's MMPR1 (iii)).
/// </remarks>
public struct MarketModelMultiProductCashFlow
{
    /// <summary>Gets or sets the index of the cash flow's payment time.</summary>
    public int TimeIndex { get; set; }

    /// <summary>Gets or sets the amount.</summary>
    public double Amount { get; set; }
}
