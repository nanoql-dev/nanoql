// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The <see cref="AUDLibor"/> presets' vectors, <c>tests/vectors/indexes/ibor/audlibor.json</c>, AUDLibor at 3M and 6M:
/// the names, conventions, fixing calendar and dates, and the forecasts on two curves.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AUDLiborVectorTests
{
    [Fact]
    public void AUDLiborVector()
    {
        var file = VectorFile.Load("indexes/ibor/audlibor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
