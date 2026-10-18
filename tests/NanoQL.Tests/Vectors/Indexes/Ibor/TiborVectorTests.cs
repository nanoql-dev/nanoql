// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The <see cref="Tibor"/> preset's vectors, <c>tests/vectors/indexes/ibor/tibor.json</c>, at 1M, 3M and
/// 6M: the names, conventions, fixing calendar and dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TiborVectorTests
{
    [Fact]
    public void TiborVector()
    {
        var file = VectorFile.Load("indexes/ibor/tibor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
