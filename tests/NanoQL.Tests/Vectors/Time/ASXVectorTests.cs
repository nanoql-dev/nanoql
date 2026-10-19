// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ASX vectors, <c>tests/vectors/time/asx.json</c>: <see cref="ASX"/>'s functions on every date of 2023 to 2026,
/// on two-character codes with and without reference dates, on the ASX dates' codes, and with no date, where they read
/// the context's evaluation date.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ASXVectorTests
{
    [Fact]
    public void ASXVector()
    {
        var file = VectorFile.Load("time/asx");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            DateFunctionInputs.Check(
                check,
                c,
                new DateFunctionInputs.Functions(
                    ASX.IsASXdate,
                    ASX.IsASXcode,
                    ASX.Code,
                    ASX.Date,
                    ASX.NextDate,
                    ASX.NextDate,
                    ASX.NextCode,
                    ASX.NextCode));
        }

        check.Complete();
    }
}
