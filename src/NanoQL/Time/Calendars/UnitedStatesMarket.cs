// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2017 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2017 Oleg Kulkov
// SPDX-FileCopyrightText: Copyright (C) 2023 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2024 Dirk Eddelbuettel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/unitedstates.hpp ql/time/calendars/unitedstates.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The US calendars: the markets a <see cref="UnitedStates"/> calendar can follow.</summary>
public enum UnitedStatesMarket
{
    /// <summary>Generic settlement calendar.</summary>
    Settlement,

    /// <summary>New York stock exchange calendar.</summary>
    NYSE,

    /// <summary>Government-bond calendar.</summary>
    GovernmentBond,

    /// <summary>Off-peak days for NERC.</summary>
    NERC,

    /// <summary>Libor impact calendar.</summary>
    LiborImpact,

    /// <summary>Federal Reserve Bankwire System.</summary>
    FederalReserve,

    /// <summary>SOFR fixing calendar.</summary>
    SOFR,
}
