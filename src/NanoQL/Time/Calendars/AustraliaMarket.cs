// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/australia.hpp ql/time/calendars/australia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Australian calendars: the markets an <see cref="Australia"/> calendar can follow.</summary>
public enum AustraliaMarket
{
    /// <summary>Generic settlement calendar.</summary>
    Settlement,

    /// <summary>Australia ASX calendar.</summary>
    ASX,
}
