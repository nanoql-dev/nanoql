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

/// <summary>The settlement method of a swaption: QuantLib's <c>Settlement::Method</c>.</summary>
/// <remarks>
/// The first two go with <see cref="SettlementType.Physical"/> and the last two with <see cref="SettlementType.Cash"/>
/// (<see cref="Settlement.CheckTypeAndMethodConsistency"/>). QuantLib's stream texts are the member names, which
/// <see cref="object.ToString"/> gives.
/// </remarks>
public enum SettlementMethod
{
    /// <summary>Physical delivery of an over-the-counter swap.</summary>
    PhysicalOTC = 0,

    /// <summary>Physical delivery of a cleared swap.</summary>
    PhysicalCleared = 1,

    /// <summary>Cash settlement at the collateralized cash price: the swap's annuity on the discount curve.</summary>
    CollateralizedCashPrice = 2,

    /// <summary>Cash settlement at the par yield curve: the annuity at the swap's own fair rate.</summary>
    ParYieldCurve = 3,
}
