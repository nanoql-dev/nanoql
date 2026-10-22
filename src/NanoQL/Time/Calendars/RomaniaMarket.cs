// SPDX-FileCopyrightText: Copyright (C) 2010 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Riccardo Barone
// SPDX-FileCopyrightText: Copyright (C) 2018 Matthias Lungwitz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/romania.hpp ql/time/calendars/romania.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Romanian calendars: the markets a <see cref="Romania"/> calendar can follow.</summary>
public enum RomaniaMarket
{
    /// <summary>Public holidays.</summary>
    Public,

    /// <summary>Bucharest stock-exchange.</summary>
    BVB,
}
