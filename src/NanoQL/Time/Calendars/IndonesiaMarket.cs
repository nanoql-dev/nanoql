// SPDX-FileCopyrightText: Copyright (C) 2005, 2007, 2008, 2009, 2010, 2011 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/indonesia.hpp ql/time/calendars/indonesia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Indonesian calendars: the markets an <see cref="Indonesia"/> calendar can follow.</summary>
public enum IndonesiaMarket
{
    /// <summary>Jakarta stock exchange (merged into IDX).</summary>
    BEJ,

    /// <summary>Jakarta stock exchange (merged into IDX).</summary>
    JSX,

    /// <summary>Indonesia stock exchange.</summary>
    IDX,
}
