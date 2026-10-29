// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's extended Ornstein-Uhlenbeck vectors,
/// <c>tests/vectors/cxx/experimental/processes/extendedornsteinuhlenbeckprocess.json</c>:
/// <see cref="ExtendedOrnsteinUhlenbeckProcess"/>'s trapezoidal and Gauss-Lobatto expectations, deviations and
/// evolutions where nanoql's rules are QuantLib's. The process as
/// <c>tools/cxx/src/experimental/processes/extendedornsteinuhlenbeckprocess.cpp</c> builds it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ExtendedOrnsteinUhlenbeckProcessCxxVectorTests
{
    [Fact]
    public void ExtendedOrnsteinUhlenbeckProcessCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/processes/extendedornsteinuhlenbeckprocess");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            ExtendedOrnsteinUhlenbeckProcess p = EnergyInputs.ExtendedOU(c.Input("process"));
            double[][] steps = [.. c.Input("steps").EnumerateArray().Select(Doubles)];
            double[] draws = Doubles(c.Input("draws"));
            check.Output(c, "expectation", steps.Select(s => p.Expectation(s[0], s[1], s[2])).ToArray());
            check.Output(c, "stdDeviation", steps.Select(s => p.StdDeviation(s[0], s[1], s[2])).ToArray());
            check.Output(c, "evolve", (from s in steps from w in draws select p.Evolve(s[0], s[1], s[2], w)).ToArray());
        }

        check.Complete();
    }
}
