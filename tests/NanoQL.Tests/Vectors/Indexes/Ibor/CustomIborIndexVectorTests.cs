// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The <see cref="CustomIborIndex"/> vectors, <c>tests/vectors/indexes/ibor/custom.json</c>, three setups of its
/// fixing, value and maturity calendars: the names, conventions, fixing calendar and dates, and the forecasts on two
/// curves.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CustomIborIndexVectorTests
{
    [Fact]
    public void CustomIborIndexVector()
    {
        var file = VectorFile.Load("indexes/ibor/custom");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
