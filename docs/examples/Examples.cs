// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Examples.GettingStarted;
using NanoQL.Examples.Rates;
using NanoQL.Examples.SofrMarket;
using NanoQL.Examples.Stateless;

namespace NanoQL.Examples;

/// <summary>Every example's outputs, each named for the file the docs include it from.</summary>
public static class Examples
{
    /// <summary>Gets each example, by the page it serves, with its outputs.</summary>
    public static IReadOnlyList<(string Page, Func<IEnumerable<(string Name, Table Table)>> Outputs)> All { get; } =
    [
        ("getting-started", () => [("first-swap", FirstSwap.Output())]),
        ("rates/dates-and-calendars", DatesAndCalendars.Outputs),
        ("rates/curves", Curves.Outputs),
        ("rates/indexes-and-fixings", IndexesAndFixings.Outputs),
        ("rates/swaps", Swaps.Outputs),
        ("rates/caps-and-floors", CapsAndFloors.Outputs),
        ("rates/swaptions", Swaptions.Outputs),
        ("stateless", StatelessPatterns.Outputs),
        ("sofr-example", SofrExample.Outputs),
    ];
}
