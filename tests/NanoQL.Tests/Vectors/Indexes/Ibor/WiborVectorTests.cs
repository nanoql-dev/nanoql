// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The <see cref="Wibor"/> presets' vectors, <c>tests/vectors/indexes/ibor/wibor.json</c>, Wibor at 1D and 3M: the
/// names, conventions, fixing calendar and dates, and the forecasts on two curves.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class WiborVectorTests
{
    [Fact]
    public void WiborVector()
    {
        var file = VectorFile.Load("indexes/ibor/wibor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
