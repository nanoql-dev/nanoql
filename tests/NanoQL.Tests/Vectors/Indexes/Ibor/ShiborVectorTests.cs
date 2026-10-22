// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The <see cref="Shibor"/> presets' vectors, <c>tests/vectors/indexes/ibor/shibor.json</c>, Shibor at 1D, 1W and 3M:
/// the names, conventions, fixing calendar and dates, and the forecasts on two curves.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ShiborVectorTests
{
    [Fact]
    public void ShiborVector()
    {
        var file = VectorFile.Load("indexes/ibor/shibor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
