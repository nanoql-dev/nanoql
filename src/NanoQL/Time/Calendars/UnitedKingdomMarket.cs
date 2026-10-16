// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/unitedkingdom.hpp ql/time/calendars/unitedkingdom.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The UK calendars: the markets a <see cref="UnitedKingdom"/> calendar can follow.</summary>
public enum UnitedKingdomMarket
{
    /// <summary>Generic settlement calendar.</summary>
    Settlement,

    /// <summary>London stock-exchange calendar.</summary>
    Exchange,

    /// <summary>London metals-exchange calendar.</summary>
    Metals,
}
