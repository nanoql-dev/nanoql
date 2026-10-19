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

/// <summary>
/// The free functions of QuantLib's <c>swaption.hpp</c>: its stream operator on <see cref="SettlementType"/>, whose
/// texts differ from the member names. The operator on <see cref="SettlementMethod"/> writes the member names, which
/// its <see cref="object.ToString"/> gives.
/// </summary>
public static class SwaptionFunctions
{
    /// <summary>A settlement type's text, as QuantLib's stream operator writes it.</summary>
    /// <param name="type">The settlement type.</param>
    /// <returns>"Delivery" or "Cash".</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public static string ToString(SettlementType type) => type switch
    {
        SettlementType.Physical => "Delivery",
        SettlementType.Cash => "Cash",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"unknown Settlement::Type({(int)type})"),
    };
}
