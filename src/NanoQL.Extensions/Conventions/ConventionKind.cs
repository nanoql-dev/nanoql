// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Extensions.Conventions;

/// <summary>The value kinds of convention records, each with names of its own.</summary>
public enum ConventionKind
{
    /// <summary>Calendars: presets, named calendars, joint and bespoke calendars.</summary>
    Calendar,

    /// <summary>Day counters: presets and named day counters.</summary>
    DayCounter,

    /// <summary>Currencies: presets.</summary>
    Currency,

    /// <summary>Aliases, each in its base's kind.</summary>
    Alias,

    /// <summary>A member of one of the core's enumerations, which an alias may name.</summary>
    Enumeration,
}
