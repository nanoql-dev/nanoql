// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/exchangecontract.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// An exchange-traded contract: its code, its expiration date and its underlying's delivery dates, which a nearby
/// commodity curve rolls on.
/// </summary>
/// <remarks>Immutable. QuantLib's <c>ExchangeContracts</c>, a map from a date to a contract, is an
/// <c>ImmutableSortedDictionary&lt;Date, ExchangeContract&gt;</c>.</remarks>
[Experimental("NQL0001")]
public sealed class ExchangeContract
{
    /// <summary>Initializes a new instance of the <see cref="ExchangeContract"/> class.</summary>
    /// <param name="code">The contract's code.</param>
    /// <param name="expirationDate">The expiration date.</param>
    /// <param name="underlyingStartDate">The underlying's first delivery date.</param>
    /// <param name="underlyingEndDate">The underlying's last delivery date.</param>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    public ExchangeContract(string code, Date expirationDate, Date underlyingStartDate, Date underlyingEndDate)
    {
        ArgumentNullException.ThrowIfNull(code);
        Date.ThrowIfDefault(expirationDate, nameof(expirationDate));
        Date.ThrowIfDefault(underlyingStartDate, nameof(underlyingStartDate));
        Date.ThrowIfDefault(underlyingEndDate, nameof(underlyingEndDate));
        Code = code;
        ExpirationDate = expirationDate;
        UnderlyingStartDate = underlyingStartDate;
        UnderlyingEndDate = underlyingEndDate;
    }

    /// <summary>Gets the contract's code.</summary>
    public string Code { get; }

    /// <summary>Gets the expiration date.</summary>
    public Date ExpirationDate { get; }

    /// <summary>Gets the underlying's first delivery date.</summary>
    public Date UnderlyingStartDate { get; }

    /// <summary>Gets the underlying's last delivery date.</summary>
    public Date UnderlyingEndDate { get; }
}
