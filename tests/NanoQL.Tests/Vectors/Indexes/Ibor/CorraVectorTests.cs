// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The <see cref="Corra"/> preset's vectors, <c>tests/vectors/indexes/ibor/corra.json</c>: its name,
/// conventions, fixing calendar and dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CorraVectorTests
{
    [Fact]
    public void CorraVector()
    {
        var file = VectorFile.Load("indexes/ibor/corra");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
