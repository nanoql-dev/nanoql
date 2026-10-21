// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/fx/fxresetcashflows.hpp ql/experimental/fx/fxresetcashflows.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>The convention for creating FX-reset observations from accrual dates.</summary>
/// <remarks>
/// A calendar is required when the convention is used directly with a non-zero fixing lag. Instruments may resolve a
/// missing calendar to an appropriate schedule calendar. Immutable.
/// </remarks>
[Experimental("NQL0001")]
public sealed class FxResetConvention
{
    /// <summary>Initializes a new instance of the <see cref="FxResetConvention"/> class.</summary>
    /// <param name="fixingDays">The business days from a fixing date to its value date.</param>
    /// <param name="fixingCalendar">The calendar the fixing days are counted on; none when <see langword="null"/>,
    /// QuantLib's empty calendar.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative.</exception>
    public FxResetConvention(int fixingDays = 0, Calendar? fixingCalendar = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fixingDays);
        FixingDays = fixingDays;
        FixingCalendar = fixingCalendar;
    }

    /// <summary>Gets the business days from a fixing date to its value date.</summary>
    public int FixingDays { get; }

    /// <summary>Gets the calendar the fixing days are counted on, <see langword="null"/> for none.</summary>
    public Calendar? FixingCalendar { get; }

    /// <summary>The fixing observation associated with an FX value date.</summary>
    /// <param name="valueDate">The value date.</param>
    /// <returns>The observation: its fixing date the fixing days before the value date.</returns>
    /// <exception cref="ArgumentException">The value date is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The fixing days are non-zero and there's no calendar.</exception>
    public FxReset Reset(Date valueDate)
    {
        if (valueDate == default)
        {
            throw new ArgumentException("null FX reset value date", nameof(valueDate));
        }

        Calendar? calendar = RequireCalendar();
        Date fixingDate = calendar is null ? valueDate : calendar.Advance(valueDate, -FixingDays, TimeUnit.Days);
        return new FxReset(fixingDate, valueDate);
    }

    /// <summary>The FX spot value date associated with a fixing date.</summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <returns>The value date: the fixing days after the fixing date.</returns>
    /// <exception cref="ArgumentException">The fixing date is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The fixing days are non-zero and there's no calendar.</exception>
    public Date ValueDate(Date fixingDate)
    {
        if (fixingDate == default)
        {
            throw new ArgumentException("null FX fixing date", nameof(fixingDate));
        }

        Calendar? calendar = RequireCalendar();
        return calendar is null ? fixingDate : calendar.Advance(fixingDate, FixingDays, TimeUnit.Days);
    }

    // The calendar a non-zero fixing lag is counted on, or null for no lag.
    private Calendar? RequireCalendar()
    {
        if (FixingDays == 0)
        {
            return null;
        }

        return FixingCalendar
            ?? throw new NanoQLException("an FX reset fixing calendar is required when fixing days are non-zero");
    }
}
