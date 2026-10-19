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
/// Settlement information: QuantLib's <c>Settlement</c> struct, whose enums are <see cref="SettlementType"/> and
/// <see cref="SettlementMethod"/>.
/// </summary>
public static class Settlement
{
    /// <summary>
    /// Checks that a settlement method goes with a settlement type: a physical settlement with
    /// <see cref="SettlementMethod.PhysicalOTC"/> or <see cref="SettlementMethod.PhysicalCleared"/>, a cash settlement
    /// with <see cref="SettlementMethod.CollateralizedCashPrice"/> or <see cref="SettlementMethod.ParYieldCurve"/>.
    /// </summary>
    /// <param name="settlementType">The settlement type.</param>
    /// <param name="settlementMethod">The settlement method.</param>
    /// <exception cref="ArgumentException">The method doesn't go with the type.</exception>
    /// <remarks>
    /// The two arguments are compared only, as QuantLib compares them, so a type outside its enum passes, and a method
    /// outside its enum is refused with a type that is a member.
    /// </remarks>
    public static void CheckTypeAndMethodConsistency(SettlementType settlementType, SettlementMethod settlementMethod)
    {
        if (settlementType == SettlementType.Physical)
        {
            if (!(settlementMethod == SettlementMethod.PhysicalOTC
                || settlementMethod == SettlementMethod.PhysicalCleared))
            {
                throw new ArgumentException(
                    "invalid settlement method for physical settlement", nameof(settlementMethod));
            }
        }

        if (settlementType == SettlementType.Cash)
        {
            if (!(settlementMethod == SettlementMethod.CollateralizedCashPrice
                || settlementMethod == SettlementMethod.ParYieldCurve))
            {
                throw new ArgumentException("invalid settlement method for cash settlement", nameof(settlementMethod));
            }
        }
    }
}
