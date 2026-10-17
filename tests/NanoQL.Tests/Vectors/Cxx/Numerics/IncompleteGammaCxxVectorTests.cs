// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's incomplete-gamma vectors, <c>tests/vectors/cxx/math/incompletegamma.json</c>: the three
/// functions of <see cref="IncompleteGammaFunctions"/>, which the wheel doesn't expose, for ten shapes at 30 points
/// with the default accuracy and iteration limit (a case's <c>accuracy</c> null), at explicit ones, and at the
/// arguments QuantLib refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IncompleteGammaCxxVectorTests
{
    [Fact]
    public void IncompleteGammaCxxVector()
    {
        var file = VectorFile.Load("cxx/math/incompletegamma");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] a = Numbers(c.Input("a"));
            double[] x = Numbers(c.Input("x"));
            bool defaults = c.Input("accuracy").ValueKind == JsonValueKind.Null;
            double[] accuracy = defaults ? [] : Numbers(c.Input("accuracy"));
            int[] maxIteration = defaults ? [] : c.Ints("maxIteration");

            object?[] Outputs(
                Func<double, double, double, int, double> f, Func<double, double, double> withDefaults) =>
                [.. Enumerable.Range(0, a.Length).Select(i => (object?)(Func<object?>)(() =>
                    defaults ? withDefaults(a[i], x[i]) : f(a[i], x[i], accuracy[i], maxIteration[i])))];

            check.Output(c, "function", Outputs(
                IncompleteGammaFunctions.IncompleteGammaFunction,
                (s, t) => IncompleteGammaFunctions.IncompleteGammaFunction(s, t)));
            check.Output(c, "series", Outputs(
                IncompleteGammaFunctions.IncompleteGammaFunctionSeriesRepr,
                (s, t) => IncompleteGammaFunctions.IncompleteGammaFunctionSeriesRepr(s, t)));
            check.Output(c, "continuedFraction", Outputs(
                IncompleteGammaFunctions.IncompleteGammaFunctionContinuedFractionRepr,
                (s, t) => IncompleteGammaFunctions.IncompleteGammaFunctionContinuedFractionRepr(s, t)));
        }

        check.Complete();
    }

    // Numbers as the vectors encode them: the non-finite ones as strings.
    private static double[] Numbers(JsonElement e) => [.. e.EnumerateArray().Select(Number)];

    private static double Number(JsonElement e) =>
        e.ValueKind == JsonValueKind.String ? double.Parse(e.GetString()!, CultureInfo.InvariantCulture) : e.GetDouble();
}
