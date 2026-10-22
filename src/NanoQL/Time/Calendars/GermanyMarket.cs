// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/germany.hpp ql/time/calendars/germany.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The German calendars: the markets a <see cref="Germany"/> calendar can follow.</summary>
public enum GermanyMarket
{
    /// <summary>Generic settlement calendar.</summary>
    Settlement,

    /// <summary>Frankfurt stock-exchange.</summary>
    FrankfurtStockExchange,

    /// <summary>Xetra.</summary>
    Xetra,

    /// <summary>Eurex.</summary>
    Eurex,

    /// <summary>Euwax.</summary>
    Euwax,
}
