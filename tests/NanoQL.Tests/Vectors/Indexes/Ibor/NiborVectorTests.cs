// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The <see cref="Nibor"/> presets' vectors, <c>tests/vectors/indexes/ibor/nibor.json</c>, Nibor at 3M and 6M: the
/// names, conventions, fixing calendar and dates, and the forecasts on two curves.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NiborVectorTests
{
    [Fact]
    public void NiborVector()
    {
        var file = VectorFile.Load("indexes/ibor/nibor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
