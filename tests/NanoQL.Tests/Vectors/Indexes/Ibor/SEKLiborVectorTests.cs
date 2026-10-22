// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The <see cref="SEKLibor"/> presets' vectors, <c>tests/vectors/indexes/ibor/seklibor.json</c>, SEKLibor at 3M: the
/// names, conventions, fixing calendar and dates, and the forecasts on two curves.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SEKLiborVectorTests
{
    [Fact]
    public void SEKLiborVector()
    {
        var file = VectorFile.Load("indexes/ibor/seklibor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
