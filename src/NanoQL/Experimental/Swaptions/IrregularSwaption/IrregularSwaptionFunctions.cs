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

/// <summary>
/// The irregular swaption's free functions: QuantLib's stream operator on <see cref="IrregularSettlementType"/>, whose
/// text differs from the one its <see cref="object.ToString"/> gives.
/// </summary>
[Experimental("NQL0001")]
public static class IrregularSwaptionFunctions
{
    /// <summary>A settlement type's text, as QuantLib's stream operator writes it.</summary>
    /// <param name="type">The settlement type.</param>
    /// <returns>"Delivery" or "Cash".</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public static string ToString(IrregularSettlementType type) => type switch
    {
        IrregularSettlementType.Physical => "Delivery",
        IrregularSettlementType.Cash => "Cash",
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, $"unknown IrregularSettlement::Type({(int)type})"),
    };
}
