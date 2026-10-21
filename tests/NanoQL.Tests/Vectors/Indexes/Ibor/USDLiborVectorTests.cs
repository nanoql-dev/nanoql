// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The USD LIBOR indexes' vectors, <c>tests/vectors/indexes/ibor/usdlibor.json</c>:
/// <see cref="USDLibor"/> at five tenors, <see cref="USDLiborON"/> and <see cref="DailyTenorUSDLibor"/>, their names,
/// conventions, fixing calendar and dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class USDLiborVectorTests
{
    [Fact]
    public void USDLiborVector()
    {
        var file = VectorFile.Load("indexes/ibor/usdlibor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
