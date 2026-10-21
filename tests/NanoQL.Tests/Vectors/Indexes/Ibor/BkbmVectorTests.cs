// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The <see cref="Bkbm"/> presets' vectors, <c>tests/vectors/indexes/ibor/bkbm.json</c>, 1M to 6M and a
/// 2W tenor: the names, conventions, fixing calendar and dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BkbmVectorTests
{
    [Fact]
    public void BkbmVector()
    {
        var file = VectorFile.Load("indexes/ibor/bkbm");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
