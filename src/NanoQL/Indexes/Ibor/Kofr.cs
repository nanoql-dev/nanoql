// SPDX-FileCopyrightText: Copyright (C) 2024 Jongbong An
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/kofr.hpp ql/indexes/ibor/kofr.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The KOFR index: the Korea Overnight Financing Repo Rate, published by the Korea Securities Depository (KSD).
/// </summary>
/// <remarks>
/// Please refer to (1) https://www.bok.or.kr/eng/main/contents.do?menuNo=400399 (Overview) and (2)
/// https://www.kofr.kr/main.jsp (Detailed information).
/// </remarks>
public sealed class Kofr : OvernightIndex
{
    /// <summary>Initializes a new instance of the <see cref="Kofr"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Kofr(YieldTermStructure? h = null)
        : this(ConventionProvider.ReadOvernightIndex("Kofr", Reference), h)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private Kofr((IborIndexConventions Values, ConventionProvider Provider) read, YieldTermStructure? h)
        : base("KOFR", read.Values.FixingDays, read.Values.Currency, read.Values.FixingCalendar, read.Values.DayCounter, h, read.Provider)
    {
    }

    // QuantLib's conventions, built in the provider's scope.
    private static IborIndexConventions Reference() => new(
        0,
        new KRWCurrency(),
        new SouthKorea(SouthKoreaMarket.Settlement),
        BusinessDayConvention.Following,
        false,
        new Actual365Fixed());
}
