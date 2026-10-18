// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007, 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/iborindex.hpp ql/indexes/iborindex.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The base class of overnight indexes (SOFR, €STR, SONIA, ...): a one-day Ibor index.</summary>
/// <remarks>Unsealed, as in QuantLib, where the presets derive from it.</remarks>
public class OvernightIndex : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="OvernightIndex"/> class.</summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="settlementDays">The fixing days.</param>
    /// <param name="currency">The currency, or <see langword="null"/>, QuantLib's empty currency.</param>
    /// <param name="fixingCalendar">The fixing calendar.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="h">The curve that forecasts fixings, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    public OvernightIndex(
        string familyName,
        int settlementDays,
        Currency? currency,
        Calendar fixingCalendar,
        DayCounter dayCounter,
        YieldTermStructure? h = null)
        : base(
            familyName,
            new Period(1, TimeUnit.Days),
            settlementDays,
            currency,
            fixingCalendar,
            BusinessDayConvention.Following,
            false,
            dayCounter,
            h)
    {
    }

    /// <summary>A copy of the index on another forwarding curve: a plain <see cref="OvernightIndex"/>, as in QuantLib.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>The copy.</returns>
    public override OvernightIndex Clone(YieldTermStructure? h) =>
        new(FamilyName, FixingDays, Currency, FixingCalendar, DayCounter, h);
}
