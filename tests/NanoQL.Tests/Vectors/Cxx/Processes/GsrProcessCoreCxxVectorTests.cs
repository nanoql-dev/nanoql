// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Reflection;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's GSR process core vectors, <c>tests/vectors/cxx/processes/gsrprocesscore.json</c>: the internal
/// core of <see cref="GsrProcess"/>, QuantLib's <c>detail::GsrProcessCore</c>, which the wheel doesn't expose, reached
/// by reflection: the three parts of the conditional expectation, the variance, y, σ, the reversion and G, each on a
/// fresh memo.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GsrProcessCoreCxxVectorTests
{
    private static readonly Type CoreType = typeof(GsrProcess).Assembly.GetType("NanoQL.Processes.GsrProcessCore", true)!;
    private static readonly Type MemoType = typeof(GsrProcess).Assembly.GetType("NanoQL.Processes.GsrProcessCoreMemo", true)!;

    [Fact]
    public void GsrProcessCoreCxxVector()
    {
        var file = VectorFile.Load("cxx/processes/gsrprocesscore");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            object core = Activator.CreateInstance(
                CoreType,
                BindingFlags.Public | BindingFlags.Instance,
                null,
                [ImmutableArray.Create(Numbers(c.Input("times"))), ImmutableArray.Create(Numbers(c.Input("vols"))),
                    ImmutableArray.Create(Numbers(c.Input("reversions"))), c.Double("T")],
                null)!;
            double[][] steps = ModelInputs.Points(c, "stepPoints");
            double[] times = Numbers(c.Input("pointTimes"));
            double[][] gPoints = ModelInputs.Points(c, "gPoints");
            check.Output(c, "expectation_x0dep_part", steps.Select(s => (object?)Call(core, "ExpectationX0DepPart", s[0], s[1], s[2])).ToArray());
            check.Output(c, "expectation_rn_part", steps.Select(s => (object?)Call(core, "ExpectationRnPart", s[0], s[2])).ToArray());
            check.Output(c, "expectation_tf_part", steps.Select(s => (object?)Call(core, "ExpectationTfPart", s[0], s[2])).ToArray());
            check.Output(c, "variance", steps.Select(s => (object?)Call(core, "Variance", s[0], s[2])).ToArray());
            check.Output(c, "y", times.Select(t => (object?)Call(core, "Y", t)).ToArray());
            check.Output(c, "sigma", times.Select(t => (object?)(double)CoreType.GetMethod("Sigma")!.Invoke(core, [t])!).ToArray());
            check.Output(c, "reversion", times.Select(t => (object?)(double)CoreType.GetMethod("Reversion")!.Invoke(core, [t])!).ToArray());
            check.Output(c, "G", gPoints.Select(g => (object?)Call(core, "G", g[0], g[1])).ToArray());
        }

        check.Complete();
    }

    // A core member on a fresh memo, its last parameter.
    private static double Call(object core, string member, params double[] arguments)
    {
        object memo = Activator.CreateInstance(MemoType)!;
        MethodInfo method = CoreType.GetMethod(member)!;
        return (double)method.Invoke(core, [.. arguments.Cast<object>(), memo])!;
    }
}
