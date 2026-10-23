// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Markov-functional state process vectors, <c>tests/vectors/cxx/processes/mfstateprocess.json</c>:
/// <see cref="MfStateProcess"/>'s members over its steps, the diffusion at a zero reversion, and its constructor's
/// refusals, each read on a process built anew.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MfStateProcessCxxVectorTests
{
    [Fact]
    public void MfStateProcessCxxVector()
    {
        var file = VectorFile.Load("cxx/processes/mfstateprocess");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("process");
            MfStateProcess Build() => new(spec.GetProperty("reversion").GetDouble(), Numbers(spec.GetProperty("times")), Numbers(spec.GetProperty("vols")));

            check.Output(c, "x0", (Func<object?>)(() => Build().X0));
            if (!c.Inputs.TryGetProperty("steps", out _))
            {
                continue;
            }

            double[][] steps = ModelInputs.Points(c, "steps");
            double[] dw = Numbers(c.Input("dw"));
            check.Output(c, "drift", steps.Select(s => (object?)(Func<object?>)(() => Build().Drift(s[0], s[1]))).ToArray());
            check.Output(c, "expectation", steps.Select(s => (object?)(Func<object?>)(() => Build().Expectation(s[0], s[1], s[2]))).ToArray());
            check.Output(c, "variance", steps.Select(s => (object?)(Func<object?>)(() => Build().Variance(s[0], s[1], s[2]))).ToArray());
            check.Output(c, "stdDeviation", steps.Select(s => (object?)(Func<object?>)(() => Build().StdDeviation(s[0], s[1], s[2]))).ToArray());
            check.Output(
                c, "evolve", steps.Select((s, i) => (object?)(Func<object?>)(() => Build().Evolve(s[0], s[1], s[2], dw[i]))).ToArray());
            if (c.Expected.TryGetProperty("diffusion", out _))
            {
                check.Output(
                    c,
                    "diffusion",
                    Numbers(c.Input("diffusionTimes")).Select(t => (object?)(Func<object?>)(() => Build().Diffusion(t, 0.0))).ToArray());
            }
        }

        check.Complete();
    }
}
