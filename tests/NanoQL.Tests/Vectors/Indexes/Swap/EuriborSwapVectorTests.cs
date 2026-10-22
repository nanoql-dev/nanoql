// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Euribor swap presets' vectors, <c>tests/vectors/indexes/swap/euriborswap.json</c>:
/// <see cref="EuriborSwapIsdaFixA"/>, <see cref="EuriborSwapIsdaFixB"/> and <see cref="EuriborSwapIfrFix"/> at several
/// tenors, in both constructors' forms: their names, conventions and Ibor indexes, their fixings, and the refusal of a
/// tenor that can't be compared with a year.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EuriborSwapVectorTests
{
    [Fact]
    public void EuriborSwapVector() => SwapPresetInputs.Check("indexes/swap/euriborswap");
}
