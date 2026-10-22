// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The <see cref="Cdor"/> presets' vectors, <c>tests/vectors/indexes/ibor/cdor.json</c>, Cdor at 1M and 3M: the names,
/// conventions, fixing calendar and dates, and the forecasts on two curves.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CdorVectorTests
{
    [Fact]
    public void CdorVector()
    {
        var file = VectorFile.Load("indexes/ibor/cdor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
