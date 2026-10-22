// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The JPY LIBOR swap presets' vectors, <c>tests/vectors/indexes/swap/jpyliborswap.json</c>:
/// <see cref="JpyLiborSwapIsdaFixAm"/> and <see cref="JpyLiborSwapIsdaFixPm"/> at several tenors, in both constructors'
/// forms: their names, conventions and Ibor indexes and their fixings; JPY LIBOR 6M at every tenor, 365 and 366 days
/// among them, which these constructors never compare with a year.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class JpyLiborSwapVectorTests
{
    [Fact]
    public void JpyLiborSwapVector() => SwapPresetInputs.Check("indexes/swap/jpyliborswap");
}
