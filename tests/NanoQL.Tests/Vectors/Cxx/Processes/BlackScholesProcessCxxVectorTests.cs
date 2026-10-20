// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="GeneralizedBlackScholesProcess"/>'s harness vectors,
/// <c>tests/vectors/cxx/processes/blackscholesprocess.json</c>: the forceDiscretization argument, the moments and
/// evolution of processes with Euler's discretization with it false and true.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackScholesProcessCxxVectorTests
{
    [Fact]
    public void BlackScholesProcessCxxVector()
    {
        var file = VectorFile.Load("cxx/processes/blackscholesprocess");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Date reference = c.DateInput("referenceDate");
            DayCounter dc = CashFlowInputs.DayCounterOf(c.Input("dayCounter"));
            var p = new GeneralizedBlackScholesProcess(
                c.Double("spot"),
                new FlatForward(reference, c.Double("dividendYield"), dc),
                new FlatForward(reference, c.Double("riskFreeRate"), dc),
                new BlackConstantVol(reference, new NullCalendar(), c.Double("volatility"), dc),
                new EulerDiscretization(),
                c.Bool("forceDiscretization"));
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Doubles)];
            check.Output(c, "expectation", points.Select(x => Lazy(() => p.Expectation(x[0], x[1], x[2]))).ToArray());
            check.Output(c, "stdDeviation", points.Select(x => Lazy(() => p.StdDeviation(x[0], x[1], x[2]))).ToArray());
            check.Output(c, "variance", points.Select(x => Lazy(() => p.Variance(x[0], x[1], x[2]))).ToArray());
            check.Output(c, "evolve", points.Select(x => Lazy(() => p.Evolve(x[0], x[1], x[2], x[3]))).ToArray());
        }

        check.Complete();
    }
}
