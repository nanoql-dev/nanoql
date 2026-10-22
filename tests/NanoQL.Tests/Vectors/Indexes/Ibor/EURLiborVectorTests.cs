// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The <see cref="EURLibor"/> presets' vectors, <c>tests/vectors/indexes/ibor/eurlibor.json</c>, EURLibor1M to
/// EURLibor1Y and EURLibor at 2W: the names, conventions, fixing calendar and dates, and the forecasts on two curves.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EURLiborVectorTests
{
    [Fact]
    public void EURLiborVector()
    {
        var file = VectorFile.Load("indexes/ibor/eurlibor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
