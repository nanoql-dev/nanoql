// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The USD LIBOR swap presets' vectors, <c>tests/vectors/indexes/swap/usdliborswap.json</c>:
/// <see cref="UsdLiborSwapIsdaFixAm"/> and <see cref="UsdLiborSwapIsdaFixPm"/> at several tenors, in both constructors'
/// forms: their names, conventions and Ibor indexes and their fixings; USD LIBOR 3M at every tenor, 365 and 366 days
/// among them, which these constructors never compare with a year.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class UsdLiborSwapVectorTests
{
    [Fact]
    public void UsdLiborSwapVector() => SwapPresetInputs.Check("indexes/swap/usdliborswap");
}
