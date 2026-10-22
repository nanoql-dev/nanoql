// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Piter Dias
// SPDX-FileCopyrightText: Copyright (C) 2007 Richard Gomes
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/brazil.hpp ql/time/calendars/brazil.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Brazilian calendars: the markets a <see cref="Brazil"/> calendar can follow.</summary>
public enum BrazilMarket
{
    /// <summary>Generic settlement calendar.</summary>
    Settlement,

    /// <summary>BOVESPA calendar.</summary>
    Exchange,
}
