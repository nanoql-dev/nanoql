// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/eurlibor.hpp ql/indexes/ibor/eurlibor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The base class for the one day deposit ICE EUR LIBOR indexes: Euro O/N LIBOR fixed by ICE.</summary>
/// <remarks>
/// <para>
/// It can be also used for T/N and S/N indexes, even if such indexes do not have ICE fixing. See
/// https://www.theice.com/marketdata/reports/170. This is the rate fixed in London by ICE; use <see cref="Eonia"/>
/// for the fixing by the ECB.
/// </para>
/// <para>An extension point: QuantLib's <see cref="EURLiborON"/> derives from it.</para>
/// </remarks>
public class DailyTenorEURLibor : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="DailyTenorEURLibor"/> class.</summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    public DailyTenorEURLibor(int settlementDays, YieldTermStructure? h = null)
        : base(
            "EURLibor",
            new Period(1, TimeUnit.Days),
            settlementDays,
            new EURCurrency(),
            // http://www.bba.org.uk/bba/jsp/polopoly.jsp?d=225&a=1412 :
            // no o/n or s/n fixings (as the case may be) will take place
            // when the principal centre of the currency concerned is
            // closed but London is open on the fixing day.
            new TARGET(),
            EURLiborConventions.Convention(new Period(1, TimeUnit.Days)),
            EURLiborConventions.EndOfMonth(new Period(1, TimeUnit.Days)),
            new Actual360(),
            h)
    {
    }
}
