// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/energycommodity.hpp ql/experimental/commodities/energycommodity.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>An energy swap's position on one pricing day: QuantLib's <c>EnergyDailyPosition</c>.</summary>
/// <param name="Date">The day.</param>
/// <param name="PayLegPrice">The pay leg's price, in the base currency per base unit.</param>
/// <param name="ReceiveLegPrice">The receive leg's price, in the base currency per base unit.</param>
/// <param name="QuantityAmount">The quantity priced on the day, in the base unit: the period's quantity spread evenly
/// over its pricing days.</param>
/// <param name="RiskDelta">The day's value: (receive price − pay price) × quantity.</param>
/// <param name="Unrealized">Whether the day is after the evaluation date.</param>
/// <remarks>Immutable: QuantLib builds a position with its prices and fills its quantity and delta later; here a swap
/// builds it whole.</remarks>
[Experimental("NQL0001")]
public sealed record EnergyDailyPosition(
    Date Date,
    double PayLegPrice,
    double ReceiveLegPrice,
    double QuantityAmount,
    double RiskDelta,
    bool Unrealized);
