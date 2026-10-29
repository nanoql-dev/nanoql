// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2010 Andre Miemiec
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/swaptions/irregularswaption.hpp ql/experimental/swaptions/irregularswaption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>How an irregular swaption settles on exercise: QuantLib's <c>IrregularSettlement::Type</c>.</summary>
/// <remarks>
/// QuantLib's stream operator writes <see cref="Physical"/> as "Delivery", which differs from the member name:
/// <see cref="IrregularSwaptionFunctions.ToString(IrregularSettlementType)"/> gives its texts.
/// </remarks>
[Experimental("NQL0001")]
public enum IrregularSettlementType
{
    /// <summary>Delivery of the underlying swap.</summary>
    Physical = 0,

    /// <summary>A cash payment of the swap's value.</summary>
    Cash = 1,
}
