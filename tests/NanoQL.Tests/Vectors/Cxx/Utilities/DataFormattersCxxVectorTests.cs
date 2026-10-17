// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.Utilities;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's data-formatter vectors, <c>tests/vectors/cxx/utilities/dataformatters.json</c>: every member of
/// <see cref="DataFormattersFunctions"/>, which the wheel doesn't expose, on QuantLib's own output: ordinals, powers of
/// two, checked nulls and sequences for reals and integers, and percentages, rates and volatilities, among them the
/// null, the non-finite values and products that overflow.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DataFormattersCxxVectorTests
{
    [Fact]
    public void DataFormattersCxxVector()
    {
        var file = VectorFile.Load("cxx/utilities/dataformatters");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.Name)
            {
                case "ordinal":
                    check.Output(c, "ordinal", Each(c.Ints("n"), n => DataFormattersFunctions.Ordinal(n)));
                    break;
                case "powerOfTwo<Integer>":
                    check.Output(c, "powerOfTwo", Each(Items(c, "n", NullableInt), n => DataFormattersFunctions.PowerOfTwo(n)));
                    break;
                case "powerOfTwo<long long>":
                    check.Output(c, "powerOfTwo", Each(Items(c, "n", NullableLong), n => DataFormattersFunctions.PowerOfTwo(n)));
                    break;
                case "checknull<Real>":
                    check.Output(c, "checkNull", Each(Items(c, "x", NullableReal), x => DataFormattersFunctions.CheckNull(x)));
                    break;
                case "checknull<Integer>":
                    check.Output(c, "checkNull", Each(Items(c, "n", NullableInt), n => DataFormattersFunctions.CheckNull(n)));
                    break;
                case "sequence<Real>":
                    check.Output(
                        c,
                        "sequence",
                        Each(Items(c, "sequences", s => s.EnumerateArray().Select(e => NullableReal(e)!.Value).ToArray()), DataFormattersFunctions.Sequence));
                    break;
                case "sequence<Integer>":
                    check.Output(
                        c,
                        "sequence",
                        Each(Items(c, "sequences", s => s.EnumerateArray().Select(e => e.GetInt32()).ToArray()), DataFormattersFunctions.Sequence));
                    break;
                case "percent, rate and volatility":
                    double?[] x = Items(c, "x", NullableReal);
                    check.Output(c, "percent", Each(x, v => DataFormattersFunctions.Percent(v)));
                    check.Output(c, "rate", Each(x, v => DataFormattersFunctions.Rate(v)));
                    check.Output(c, "volatility", Each(x, v => DataFormattersFunctions.Volatility(v)));
                    break;
                default:
                    Assert.Fail($"unexpected case '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    private static T[] Items<T>(VectorCase c, string name, Func<JsonElement, T> read) =>
        c.Input(name).EnumerateArray().Select(read).ToArray();

    private static int? NullableInt(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetInt32();

    private static long? NullableLong(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetInt64();

    // The vector encoding writes NaN and the infinities as strings, and Null<Real> as null.
    private static double? NullableReal(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => e.GetString() switch
        {
            "NaN" => double.NaN,
            "Infinity" => double.PositiveInfinity,
            "-Infinity" => double.NegativeInfinity,
            string s => throw new InvalidOperationException($"not a real: {s}"),
            null => throw new InvalidOperationException("not a real"),
        },
        _ => e.GetDouble(),
    };

    private static Func<object?>[] Each<T>(T[] inputs, Func<T, string> f) =>
        inputs.Select(x => (Func<object?>)(() => f(x))).ToArray();
}
