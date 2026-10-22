// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The <see cref="Saron"/> presets' vectors, <c>tests/vectors/indexes/ibor/saron.json</c>, Saron: the names,
/// conventions, fixing calendar and dates, and the forecasts on two curves.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SaronVectorTests
{
    [Fact]
    public void SaronVector()
    {
        var file = VectorFile.Load("indexes/ibor/saron");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
