// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The <see cref="Bibor"/> presets' vectors, <c>tests/vectors/indexes/ibor/bibor.json</c>, BiborSW, Bibor1M to Bibor1Y
/// and Bibor at 4M: the names, conventions, fixing calendar and dates, and the forecasts on two curves.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BiborVectorTests
{
    [Fact]
    public void BiborVector()
    {
        var file = VectorFile.Load("indexes/ibor/bibor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
