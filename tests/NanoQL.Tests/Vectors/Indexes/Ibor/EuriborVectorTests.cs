// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Euribor presets' vectors, <c>tests/vectors/indexes/ibor/euribor.json</c>: <see cref="Euribor1W"/>,
/// <see cref="Euribor1M"/>, <see cref="Euribor3M"/>, <see cref="Euribor6M"/>, <see cref="Euribor1Y"/> and
/// <see cref="Euribor365"/>'s names, conventions, fixing calendars and dates, and the refusal of a daily or an
/// intraday tenor.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EuriborVectorTests
{
    [Fact]
    public void EuriborVector()
    {
        var file = VectorFile.Load("indexes/ibor/euribor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            PresetInputs.Check(check, c);
        }

        check.Complete();
    }
}
