// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/austria.hpp ql/time/calendars/austria.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Austrian calendars: the markets an <see cref="Austria"/> calendar can follow.</summary>
public enum AustriaMarket
{
    /// <summary>Generic settlement calendar.</summary>
    Settlement,

    /// <summary>Vienna stock-exchange calendar.</summary>
    Exchange,
}
