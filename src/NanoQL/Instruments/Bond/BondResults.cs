// SPDX-FileCopyrightText: Copyright (C) 2004 Jeff Yu
// SPDX-FileCopyrightText: Copyright (C) 2004 M-Dimension Consulting Inc.
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008, 2010 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2022 Oleg Kulkov
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bond.hpp ql/instruments/bond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>The results an engine returns for a bond: the instrument's, and the settlement value.</summary>
/// <remarks>
/// An extension point: the callable bond's results derive from it, as QuantLib's do. The clean and dirty prices and the
/// theoretical yield, which QuantLib's bond computes on demand from the settlement value, are
/// <see cref="Bond.CleanPrice(BondResults, EvaluationContext)"/>, <see cref="Bond.DirtyPrice(BondResults, EvaluationContext)"/>
/// and <see cref="Bond.Yield(BondResults, DayCounter, Compounding, Frequency, EvaluationContext, double, int, double, BondPriceType)"/>.
/// </remarks>
public record BondResults : InstrumentResults
{
    /// <summary>
    /// Gets the value of the cash flows after the bond's settlement date, at that date; <see langword="null"/> where
    /// QuantLib's engine leaves it unset and its <c>settlementValue()</c> throws "settlement value not provided".
    /// </summary>
    public double? SettlementValue { get; init; }
}
