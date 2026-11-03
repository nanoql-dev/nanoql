// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's square-root volatility process vectors,
/// <c>tests/vectors/cxx/models/marketmodels/evolvers/volprocesses/squarerootandersen.json</c>:
/// <see cref="SquareRootAndersen"/>'s paths of given variates where the first step's e^{−κΔt} underflows to 0,
/// QuantLib's own value, and the later steps' κΔt are past nanoql's small-reversion cut, each sub-step on either side
/// of the cut point: each step's weight, standard deviation and state; and the variates per step and state variables.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SquareRootAndersenCxxVectorTests
{
    [Fact]
    public void SquareRootAndersenCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/evolvers/volprocesses/squarerootandersen");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            SquareRootAndersen process = SquareRootAndersenOf(c.Inputs);
            check.Output(c, "variatesPerStep", process.VariatesPerStep);
            check.Output(c, "numberStateVariables", process.NumberStateVariables);
            var weights = new List<object?>();
            var sds = new List<object?>();
            var states = new List<object?>();
            foreach (JsonElement path in c.Input("variates").EnumerateArray())
            {
                process.NextPath();
                var w = new List<object?>();
                var sd = new List<object?>();
                var state = new List<object?>();
                foreach (JsonElement z in path.EnumerateArray())
                {
                    w.Add(process.Nextstep(Reals(z)));
                    sd.Add(process.StepSd());
                    state.Add(process.StateVariables().ToArray());
                }

                weights.Add(w);
                sds.Add(sd);
                states.Add(state);
            }

            check.Output(c, "weight", weights);
            check.Output(c, "stepSd", sds);
            check.Output(c, "state", states);
        }

        check.Complete();
    }
}
