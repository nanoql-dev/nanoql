// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's gamma-function vectors, <c>tests/vectors/cxx/math/distributions/gammadistribution.json</c>:
/// <see cref="GammaFunction.Value"/>, which the wheel doesn't expose, at about 200 points over its three branches.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GammaDistributionCxxVectorTests
{
    [Fact]
    public void GammaDistributionCxxVector()
    {
        var file = VectorFile.Load("cxx/math/distributions/gammadistribution");
        var check = new VectorAssert(file);
        var gamma = new GammaFunction();
        foreach (VectorCase c in file.Cases)
        {
            double[] xs = [.. c.Input("x").EnumerateArray().Select(Number)];
            check.Output(c, "value", xs.Select(x => (object?)(Func<object?>)(() => gamma.Value(x))).ToArray());
        }

        check.Complete();
    }

    // A number as the vectors encode it: the non-finite ones as strings.
    private static double Number(JsonElement e) =>
        e.ValueKind == JsonValueKind.String ? double.Parse(e.GetString()!, CultureInfo.InvariantCulture) : e.GetDouble();
}
