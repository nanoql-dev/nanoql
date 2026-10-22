// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/poland.hpp ql/time/calendars/poland.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Polish calendars: the markets a <see cref="Poland"/> calendar can follow.</summary>
public enum PolandMarket
{
    /// <summary>Settlement calendar.</summary>
    Settlement,

    /// <summary>Warsaw stock exchange calendar.</summary>
    WSE,
}
