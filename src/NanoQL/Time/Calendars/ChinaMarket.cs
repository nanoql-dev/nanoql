// SPDX-FileCopyrightText: Copyright (C) 2004 FIMAT Group
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009, 2010, 2011 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009, 2010, 2011 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/china.hpp ql/time/calendars/china.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Chinese calendars: the markets a <see cref="China"/> calendar can follow.</summary>
public enum ChinaMarket
{
    /// <summary>Shanghai stock exchange.</summary>
    SSE,

    /// <summary>Interbank calendar.</summary>
    IB,
}
