// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: Copyright (C) 2015 Riccardo Barone
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/israel.hpp ql/time/calendars/israel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Israeli calendars: the markets an <see cref="Israel"/> calendar can follow.</summary>
/// <remarks>QuantLib's deprecated <c>Settlement</c>, 0, isn't ported.</remarks>
public enum IsraelMarket
{
    /// <summary>Tel-Aviv stock exchange calendar (Fri/Sat weekends, pre-2026; Sat/Sun afterwards).</summary>
    TASE = 1,

    /// <summary>SHIR fixing calendar (Sat/Sun weekends).</summary>
    SHIR = 2,

    /// <summary>Telbor fixing calendar (Sat/Sun weekends).</summary>
    Telbor = 3,
}
