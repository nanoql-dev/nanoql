// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2014 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2016, 2018 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/swaption.hpp ql/instruments/swaption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>How a swaption settles on exercise: QuantLib's <c>Settlement::Type</c>.</summary>
/// <remarks>
/// QuantLib's stream operator writes <see cref="Physical"/> as "Delivery", which differs from the member name:
/// <see cref="SwaptionFunctions.ToString(SettlementType)"/> gives its texts.
/// </remarks>
public enum SettlementType
{
    /// <summary>Delivery of the underlying swap.</summary>
    Physical = 0,

    /// <summary>A cash payment of the swap's value.</summary>
    Cash = 1,
}
