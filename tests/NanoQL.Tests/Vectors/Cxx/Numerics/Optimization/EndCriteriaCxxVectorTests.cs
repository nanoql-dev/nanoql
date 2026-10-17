// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's end-criteria vectors, <c>tests/vectors/cxx/math/optimization/endcriteria.json</c>:
/// <see cref="EndCriteria"/>'s constructor, its defaults and refusals, and its inspectors; each check and
/// <see cref="EndCriteria.Evaluate"/> (QuantLib's <c>operator()</c>), with the stationary count and the criterion met;
/// <see cref="EndCriteria.Succeeded"/>; and <see cref="EndCriteriaType"/>'s text.
/// </summary>
/// <remarks>
/// QuantLib's checks return a bool and set their <c>ecType</c> argument, from None, only where they hold: a check that
/// returns <see cref="bool"/> here gives the type its own criterion names when it holds, and None otherwise.
/// </remarks>
[Trait("Oracle", "QuantLib")]
public class EndCriteriaCxxVectorTests
{
    [Fact]
    public void EndCriteriaCxxVector()
    {
        var file = VectorFile.Load("cxx/math/optimization/endcriteria");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.Name)
            {
                case "constructor and inspectors":
                    check.Output(
                        c,
                        "inspectors",
                        c.Input("criteria").EnumerateArray().Select(spec => (object?)(Func<object?>)(() =>
                        {
                            EndCriteria e = OptimizationSpecs.EndCriteria(spec);
                            return new object?[]
                            {
                                e.MaxIterations, e.MaxStationaryStateIterations, e.RootEpsilon, e.FunctionEpsilon,
                                e.GradientNormEpsilon,
                            };
                        })).ToArray());
                    break;
                case "checkMaxIterations":
                {
                    EndCriteria e = OptimizationSpecs.EndCriteria(c.Input("criteria"));
                    bool[] results = [.. c.Ints("iteration").Select(e.CheckMaxIterations)];
                    Checked(check, c, results, EndCriteriaType.MaxIterations);
                    break;
                }

                case "checkStationaryPoint":
                case "checkStationaryFunctionValue":
                {
                    EndCriteria e = OptimizationSpecs.EndCriteria(c.Input("criteria"));
                    EndCriteriaCheck[] results =
                    [
                        .. c.Input("inputs").EnumerateArray().Select(t =>
                        {
                            JsonElement[] a = [.. t.EnumerateArray()];
                            return c.Name == "checkStationaryPoint"
                                ? e.CheckStationaryPoint(Number(a[0]), Number(a[1]), a[2].GetInt32())
                                : e.CheckStationaryFunctionValue(Number(a[0]), Number(a[1]), a[2].GetInt32());
                        }),
                    ];
                    Counted(check, c, results);
                    break;
                }

                case "checkStationaryFunctionAccuracy":
                {
                    EndCriteria e = OptimizationSpecs.EndCriteria(c.Input("criteria"));
                    bool[] results =
                    [
                        .. c.Input("inputs").EnumerateArray().Select(t =>
                        {
                            JsonElement[] a = [.. t.EnumerateArray()];
                            return e.CheckStationaryFunctionAccuracy(Number(a[0]), a[1].GetBoolean());
                        }),
                    ];
                    Checked(check, c, results, EndCriteriaType.StationaryFunctionAccuracy);
                    break;
                }

                case "checkZeroGradientNorm":
                case "checkZeroGradientNorm, null epsilon":
                {
                    EndCriteria e = OptimizationSpecs.EndCriteria(c.Input("criteria"));
                    bool[] results = [.. Numbers(c.Input("gradientNorm")).Select(e.CheckZeroGradientNorm)];
                    Checked(check, c, results, EndCriteriaType.ZeroGradientNorm);
                    break;
                }

                case "operator()":
                {
                    EndCriteria e = OptimizationSpecs.EndCriteria(c.Input("criteria"));
                    EndCriteriaCheck[] results =
                    [
                        .. c.Input("inputs").EnumerateArray().Select(t =>
                        {
                            JsonElement[] a = [.. t.EnumerateArray()];
                            return e.Evaluate(
                                a[0].GetInt32(),
                                a[1].GetInt32(),
                                a[2].GetBoolean(),
                                Number(a[3]),
                                Number(a[4]),
                                Number(a[5]),
                                Number(a[6]));
                        }),
                    ];
                    Counted(check, c, results);
                    break;
                }

                case "succeeded and the stream text":
                {
                    EndCriteriaType[] types = [.. c.Ints("types").Select(t => (EndCriteriaType)t)];
                    check.Output(c, "succeeded", types.Select(t => (object?)EndCriteria.Succeeded(t)).ToArray());
                    check.Output(c, "text", types.Select(t => (object?)t.ToString()).ToArray());
                    break;
                }

                default:
                    throw new InvalidOperationException($"unknown case {c.Name}");
            }
        }

        check.Complete();
    }

    // A check returning bool: QuantLib's ecType is the check's own criterion where it holds, and stays None otherwise.
    private static void Checked(VectorAssert check, VectorCase c, bool[] results, EndCriteriaType type)
    {
        check.Output(c, "result", results.Select(r => (object?)r).ToArray());
        check.Output(c, "ecType", results.Select(r => (object?)(r ? type : EndCriteriaType.None)).ToArray());
    }

    private static void Counted(VectorAssert check, VectorCase c, EndCriteriaCheck[] results)
    {
        check.Output(c, "result", results.Select(r => (object?)r.Stop).ToArray());
        check.Output(c, "statStateIterations", results.Select(r => (object?)r.StationaryStateIterations).ToArray());
        check.Output(c, "ecType", results.Select(r => (object?)r.Type).ToArray());
    }
}
