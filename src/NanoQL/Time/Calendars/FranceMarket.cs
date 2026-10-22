// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/france.hpp ql/time/calendars/france.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The French calendars: the markets a <see cref="France"/> calendar can follow.</summary>
public enum FranceMarket
{
    /// <summary>Generic settlement calendar.</summary>
    Settlement,

    /// <summary>Paris stock-exchange calendar.</summary>
    Exchange,
}
