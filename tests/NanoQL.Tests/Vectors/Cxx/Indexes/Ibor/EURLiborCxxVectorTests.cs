// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's EUR LIBOR vectors, <c>tests/vectors/cxx/indexes/ibor/eurlibor.json</c>:
/// <see cref="DailyTenorEURLibor"/> at 0 and 2 settlement days and <see cref="EURLiborON"/>, which the wheel doesn't
/// bind: the names, conventions, fixing calendar and dates, and the forecasts on two curves.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EURLiborCxxVectorTests
{
    [Fact]
    public void EURLiborCxxVector()
    {
        var file = VectorFile.Load("cxx/indexes/ibor/eurlibor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
