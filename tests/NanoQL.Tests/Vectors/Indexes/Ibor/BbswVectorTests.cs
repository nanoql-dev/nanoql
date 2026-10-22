// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The <see cref="Bbsw"/> presets' vectors, <c>tests/vectors/indexes/ibor/bbsw.json</c>, Bbsw1M to Bbsw6M and Bbsw at
/// 2W: the names, conventions, fixing calendar and dates, and the forecasts on two curves.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BbswVectorTests
{
    [Fact]
    public void BbswVector()
    {
        var file = VectorFile.Load("indexes/ibor/bbsw");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
