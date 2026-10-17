// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's error-function vectors, <c>tests/vectors/cxx/math/errorfunction.json</c>:
/// <see cref="ErrorFunction"/>, which the wheel doesn't expose, at about 400 points over FDLIBM's branches, both
/// signs, ±0, ±∞ and NaN.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ErrorFunctionCxxVectorTests
{
    [Fact]
    public void ErrorFunctionCxxVector()
    {
        var file = VectorFile.Load("cxx/math/errorfunction");
        var check = new VectorAssert(file);
        var erf = new ErrorFunction();
        foreach (VectorCase c in file.Cases)
        {
            double[] xs = [.. c.Input("x").EnumerateArray().Select(Number)];
            check.Output(c, "erf", xs.Select(x => (object?)(Func<object?>)(() => erf.Evaluate(x))).ToArray());
        }

        check.Complete();
    }

    // A number as the vectors encode it: the non-finite ones as strings.
    private static double Number(JsonElement e) =>
        e.ValueKind == JsonValueKind.String ? double.Parse(e.GetString()!, CultureInfo.InvariantCulture) : e.GetDouble();
}
