// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using System.Text.Json;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's analytic time-dependent Heston engine vectors,
/// <c>tests/vectors/cxx/pricingengines/vanilla/analyticptdhestonengine.json</c>: the normalized characteristic function
/// and its logarithm at six complex arguments and four times, on three piecewise time-dependent models.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticPTDHestonEngineCxxVectorTests
{
    [Fact]
    public void AnalyticPTDHestonEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/vanilla/analyticptdhestonengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            HestonProcess process = HestonCxxInputs.ProcessOf(c.Input("process"));
            double[] pTimes = c.Doubles("pTimes");

            Parameter ParameterOf(string name, Constraint constraint)
            {
                double[] values = c.Doubles(name);
                if (values.Length == 1)
                {
                    return new ConstantParameter(values[0], constraint);
                }

                Parameter p = new PiecewiseConstantParameter(pTimes, constraint);
                for (int i = 0; i < values.Length; ++i)
                {
                    p = p.SetParam(i, values[i]);
                }

                return p;
            }

            var model = new PiecewiseTimeDependentHestonModel(
                process.RiskFreeRate,
                process.DividendYield,
                process.S0,
                process.V0,
                ParameterOf("theta", new PositiveConstraint()),
                ParameterOf("kappa", new PositiveConstraint()),
                ParameterOf("sigma", new PositiveConstraint()),
                ParameterOf("rho", new BoundaryConstraint(-1.0, 1.0)),
                new TimeGrid(c.Doubles("grid")));
            var engine = new AnalyticPTDHestonEngine(model);
            Complex[] zs = [.. c.Input("zs").EnumerateArray().Select(ComplexOf)];
            double[] ts = c.Doubles("ts");
            check.Output(c, "chF", zs.SelectMany(z => ts.Select(t => Lazy(() => COSHestonEngineCxxVectorTests.Parts(engine.ChF(z, t))))).ToArray());
            check.Output(
                c, "lnChF", zs.SelectMany(z => ts.Select(t => Lazy(() => COSHestonEngineCxxVectorTests.Parts(engine.LnChF(z, t))))).ToArray());
        }

        check.Complete();
    }

    // a complex value as the vectors write it, [real, imaginary]
    internal static Complex ComplexOf(JsonElement e) => new(e[0].GetDouble(), e[1].GetDouble());
}
