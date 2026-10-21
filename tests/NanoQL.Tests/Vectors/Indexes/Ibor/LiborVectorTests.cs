// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The <see cref="Libor"/> and <see cref="DailyTenorLibor"/> bases' vectors,
/// <c>tests/vectors/indexes/ibor/libor.json</c>, built with a currency's own arguments: the names, conventions,
/// fixing calendar and dates, and the refused constructions.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LiborVectorTests
{
    [Fact]
    public void LiborVector()
    {
        var file = VectorFile.Load("indexes/ibor/libor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
