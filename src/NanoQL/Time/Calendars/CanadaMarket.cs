// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/canada.hpp ql/time/calendars/canada.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Canadian calendars: the markets a <see cref="Canada"/> calendar can follow.</summary>
public enum CanadaMarket
{
    /// <summary>Generic settlement calendar.</summary>
    Settlement,

    /// <summary>Toronto stock exchange calendar.</summary>
    TSX,
}
