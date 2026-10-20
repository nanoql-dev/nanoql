// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Black-Scholes process vectors, <c>tests/vectors/processes/blackscholesprocess.json</c>: the four named processes
/// and the generalized one on flat and zero curves and constant, curve and surface volatilities, their accessors and
/// the process interface at 10 points; a process given its own local volatility; and BlackScholesProcess's dividend
/// curve under two evaluation dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackScholesProcessVectorTests
{
    [Fact]
    public void BlackScholesProcessVector()
    {
        var file = VectorFile.Load("processes/blackscholesprocess");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("dividendDiscount", out _))
            {
                GeneralizedBlackScholesProcess bs = ProcessOf(c.Input("process"), context);
                check.Output(c, "dividendReferenceDate", bs.DividendYield.ReferenceDate);
                check.Output(c, "dividendDiscount", bs.DividendYield.Discount(context.EvaluationDate + 365));
                continue;
            }

            GeneralizedBlackScholesProcess p = c.Inputs.TryGetProperty("localVolatility", out var local)
                ? ProcessWithLocalVolOf(c.Input("process"), local, context)
                : ProcessOf(c.Input("process"), context);
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Doubles)];
            if (c.Expected.TryGetProperty("x0", out _))
            {
                check.Output(c, "x0", p.X0);
                check.Output(c, "stateVariable", p.StateVariable);
                check.Output(c, "riskFreeReferenceDate", p.RiskFreeRate.ReferenceDate);
                check.Output(c, "dividendReferenceDate", p.DividendYield.ReferenceDate);
                check.Output(c, "blackVolatilityReferenceDate", p.BlackVolatility.ReferenceDate);
                check.Output(c, "localVolatilityReferenceDate", p.LocalVolatility.ReferenceDate);
                check.Output(c, "localVol", points.Select(x => Lazy(() => p.LocalVolatility.LocalVol(x[0], x[1], true))).ToArray());
                check.Output(c, "apply", points.Select(x => (object?)p.Apply(x[1], x[3] * 0.1)).ToArray());
            }

            check.Output(c, "drift", points.Select(x => Lazy(() => p.Drift(x[0], x[1]))).ToArray());
            check.Output(c, "diffusion", points.Select(x => Lazy(() => p.Diffusion(x[0], x[1]))).ToArray());
            check.Output(c, "expectation", points.Select(x => Lazy(() => p.Expectation(x[0], x[1], x[2]))).ToArray());
            check.Output(c, "stdDeviation", points.Select(x => Lazy(() => p.StdDeviation(x[0], x[1], x[2]))).ToArray());
            check.Output(c, "variance", points.Select(x => Lazy(() => p.Variance(x[0], x[1], x[2]))).ToArray());
            check.Output(c, "evolve", points.Select(x => Lazy(() => p.Evolve(x[0], x[1], x[2], x[3]))).ToArray());
        }

        check.Complete();
    }
}
