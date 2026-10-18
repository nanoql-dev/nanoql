// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The rounding vectors, <c>tests/vectors/math/rounding.json</c>: <see cref="UpRounding"/>, <see cref="DownRounding"/>,
/// <see cref="ClosestRounding"/>, <see cref="FloorTruncation"/> and <see cref="CeilingTruncation"/> at precisions 0
/// to 16 with digits 1, 5 and 9, and the default <see cref="Rounding()"/>, each applied to 30 values among them both
/// zeros, both infinities, NaN, ties and magnitudes to 1e300. The tolerance can't tell a zero's sign, which QuantLib's
/// rounding drops for a zero and keeps for a negative value rounded to zero, so the test also checks each zero's sign;
/// and it prints how many outputs are bit-identical to QuantLib's.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RoundingVectorTests
{
    [Fact]
    public void RoundingVector()
    {
        var file = VectorFile.Load("math/rounding");
        var check = new VectorAssert(file);
        var signs = new List<string>();
        foreach (VectorCase c in file.Cases)
        {
            string kind = c.String("kind");
            int? precision = c.NullableInt("precision"), digit = c.NullableInt("digit");
            Rounding rounding = kind switch
            {
                "Rounding" => new Rounding(),
                "UpRounding" => new UpRounding(precision!.Value, digit!.Value),
                "DownRounding" => new DownRounding(precision!.Value, digit!.Value),
                "ClosestRounding" => new ClosestRounding(precision!.Value, digit!.Value),
                "FloorTruncation" => new FloorTruncation(precision!.Value, digit!.Value),
                "CeilingTruncation" => new CeilingTruncation(precision!.Value, digit!.Value),
                _ => throw new InvalidOperationException($"unknown rounding {kind}"),
            };
            double[] rounded = [.. Numbers(c.Input("values")).Select(rounding.Evaluate)];
            check.Output(c, "rounded", rounded.Select(x => (object?)x));
            double[] expected = Numbers(c.Expected.GetProperty("rounded"));
            for (int i = 0; i < Math.Min(expected.Length, rounded.Length); ++i)
            {
                if (expected[i] == 0.0 && rounded[i] == 0.0
                    && double.IsNegative(expected[i]) != double.IsNegative(rounded[i]))
                {
                    signs.Add($"{c.Name} › rounded[{i}]: expected {expected[i]:R}, got {rounded[i]:R}");
                }
            }
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"math/rounding: bit-identical {check.BitIdentical} of {check.Reals} real outputs");
        check.Complete();
        Assert.True(
            signs.Count == 0,
            $"math/rounding: {signs.Count} zeros of the wrong sign{Environment.NewLine}"
            + string.Join(Environment.NewLine, signs.Take(50)));
    }

    // The values as the vectors encode them: NaN and the infinities as strings.
    private static double[] Numbers(JsonElement e) => [.. e.EnumerateArray().Select(Number)];

    private static double Number(JsonElement e) =>
        e.ValueKind == JsonValueKind.String ? double.Parse(e.GetString()!, CultureInfo.InvariantCulture) : e.GetDouble();
}
