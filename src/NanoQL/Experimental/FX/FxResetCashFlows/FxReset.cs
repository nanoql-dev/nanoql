// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/fx/fxresetcashflows.hpp ql/experimental/fx/fxresetcashflows.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>The contractual observation used to reset a cross-currency notional.</summary>
/// <remarks>
/// The fixing date is the date on which the FX rate is observed. The value date is the settlement date of that FX
/// observation and is normally the corresponding accrual-period boundary. A value; <c>default(FxReset)</c> holds two
/// default dates, which no member accepts.
/// </remarks>
[Experimental("NQL0001")]
public readonly record struct FxReset
{
    /// <summary>Initializes a new instance of the <see cref="FxReset"/> struct.</summary>
    /// <param name="fixingDate">The date on which the FX rate is observed.</param>
    /// <param name="valueDate">The settlement date of the observation.</param>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>, or the fixing date is after the value
    /// date.</exception>
    public FxReset(Date fixingDate, Date valueDate)
    {
        if (fixingDate == default)
        {
            throw new ArgumentException("null FX reset fixing date", nameof(fixingDate));
        }

        if (valueDate == default)
        {
            throw new ArgumentException("null FX reset value date", nameof(valueDate));
        }

        if (!(fixingDate <= valueDate))
        {
            throw new ArgumentException(
                $"FX reset fixing date ({fixingDate}) cannot be after its value date ({valueDate})",
                nameof(fixingDate));
        }

        FixingDate = fixingDate;
        ValueDate = valueDate;
    }

    /// <summary>Gets the date on which the FX rate is observed.</summary>
    public Date FixingDate { get; }

    /// <summary>Gets the settlement date of the observation.</summary>
    public Date ValueDate { get; }
}
