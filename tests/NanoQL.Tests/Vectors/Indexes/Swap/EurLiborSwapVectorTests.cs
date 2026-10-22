// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The EUR LIBOR swap presets' vectors, <c>tests/vectors/indexes/swap/eurliborswap.json</c>:
/// <see cref="EurLiborSwapIsdaFixA"/>, <see cref="EurLiborSwapIsdaFixB"/> and <see cref="EurLiborSwapIfrFix"/> at
/// several tenors, in both constructors' forms: their names, conventions and Ibor indexes and their fixings; EUR LIBOR
/// 3M up to a year and 6M beyond, and 365 and 366 days refused, a tenor QuantLib can't compare with a year.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EurLiborSwapVectorTests
{
    [Fact]
    public void EurLiborSwapVector() => SwapPresetInputs.Check("indexes/swap/eurliborswap");
}
