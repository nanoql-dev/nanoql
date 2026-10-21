// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/newzealand.hpp ql/time/calendars/newzealand.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The New Zealand calendars: the markets a <see cref="NewZealand"/> calendar can follow.</summary>
public enum NewZealandMarket
{
    /// <summary>Wellington, with its Anniversary Day.</summary>
    Wellington,

    /// <summary>Auckland, with its Anniversary Day.</summary>
    Auckland,
}
