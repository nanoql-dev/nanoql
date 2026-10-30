// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/ConventionsXmlTests.cs (nanoql's own record kind, which QuantLib's presets don't use: the round trips, and the set's, Describe and sensitivity facts)

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A named day counter on a base (<c>DayCounter</c>): the base itself, or for <see cref="Time.Business252"/> the base
/// counting another calendar's business days, the only day counter with convention data.
/// </summary>
public sealed record DayCounterRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="DayCounterRecord"/> class.</summary>
    /// <param name="name">The day counter's name in the set.</param>
    /// <param name="base">The day counter it is based on: any day counter name, its own for the record below it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="base"/> is null.</exception>
    public DayCounterRecord(string name, string @base)
        : base(name, @base ?? throw new ArgumentNullException(nameof(@base)))
    {
    }

    /// <summary>Gets the calendar's name, on a <c>Business252</c> base only, or <see langword="null"/> for the base's.</summary>
    public string? Calendar { get; init; }
}
