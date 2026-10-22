// SPDX-FileCopyrightText: Copyright (C) 2010 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Dmitri Nesteruk
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/russia.hpp ql/time/calendars/russia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Russian calendars: the markets a <see cref="Russia"/> calendar can follow.</summary>
public enum RussiaMarket
{
    /// <summary>Generic settlement calendar.</summary>
    Settlement,

    /// <summary>Moscow Exchange calendar.</summary>
    MOEX,
}
