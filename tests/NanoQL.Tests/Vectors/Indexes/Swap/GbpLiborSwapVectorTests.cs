// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The GBP LIBOR swap presets' vectors, <c>tests/vectors/indexes/swap/gbpliborswap.json</c>:
/// <see cref="GbpLiborSwapIsdaFix"/> at several tenors, in both constructors' forms: their names, conventions and Ibor
/// indexes and their fixings; GBP LIBOR 3M on an annual fixed leg up to a year and 6M on a semiannual one beyond, fixed
/// on the day, and 365 and 366 days refused, a tenor QuantLib can't compare with a year.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GbpLiborSwapVectorTests
{
    [Fact]
    public void GbpLiborSwapVector() => SwapPresetInputs.Check("indexes/swap/gbpliborswap");
}
