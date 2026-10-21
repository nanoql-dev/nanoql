// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The JPY LIBOR indexes' vectors, <c>tests/vectors/indexes/ibor/jpylibor.json</c>:
/// <see cref="JPYLibor"/> at five tenors and <see cref="DailyTenorJPYLibor"/>, their names, conventions, fixing
/// calendar and dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class JPYLiborVectorTests
{
    [Fact]
    public void JPYLiborVector()
    {
        var file = VectorFile.Load("indexes/ibor/jpylibor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
