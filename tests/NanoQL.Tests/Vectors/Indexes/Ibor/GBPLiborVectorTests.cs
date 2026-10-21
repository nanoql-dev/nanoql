// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The GBP LIBOR indexes' vectors, <c>tests/vectors/indexes/ibor/gbplibor.json</c>:
/// <see cref="GBPLibor"/> at five tenors, <see cref="GBPLiborON"/> and <see cref="DailyTenorGBPLibor"/>, their names,
/// conventions, fixing calendar and dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GBPLiborVectorTests
{
    [Fact]
    public void GBPLiborVector()
    {
        var file = VectorFile.Load("indexes/ibor/gbplibor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
