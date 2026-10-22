// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The <see cref="CHFLibor"/> presets' vectors, <c>tests/vectors/indexes/ibor/chflibor.json</c>, CHFLibor at 3M and 6M:
/// the names, conventions, fixing calendar and dates, and the forecasts on two curves.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CHFLiborVectorTests
{
    [Fact]
    public void CHFLiborVector()
    {
        var file = VectorFile.Load("indexes/ibor/chflibor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
