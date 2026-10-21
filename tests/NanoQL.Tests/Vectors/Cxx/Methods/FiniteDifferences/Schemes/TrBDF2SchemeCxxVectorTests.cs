// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's TR-BDF2 vectors, <c>tests/vectors/cxx/methods/finitedifferences/schemes/trbdf2scheme.json</c>:
/// <see cref="TrBDF2Scheme{TTrapezoidalScheme}"/> over a Crank-Nicolson trapezoid stepped on its own on the
/// Black-Scholes operator, the values after each step and
/// <see cref="TrBDF2Scheme{TTrapezoidalScheme}.NumberOfIterations"/> after the last.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TrBDF2SchemeCxxVectorTests
{
    [Fact]
    public void TrBDF2SchemeCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/finitedifferences/schemes/trbdf2scheme");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Date today = context.EvaluationDate;
            JsonElement spec = c.Input("scheme");
            double Get(string name) => spec.GetProperty(name).GetDouble();
            DayCounter dc = new Actual365Fixed();
            var process = new BlackScholesMertonProcess(
                Get("spot"),
                new FlatForward(today, Get("dividendYield"), dc),
                new FlatForward(today, Get("riskFreeRate"), dc),
                new BlackConstantVol(today, null, Get("volatility"), dc));
            var mesher = new FdmMesherComposite(new Uniform1dMesher(Get("xMin"), Get("xMax"), spec.GetProperty("size").GetInt32()));
            var op = new FdmBlackScholesOp(mesher, process, Get("spot"));
            var scheme = new TrBDF2Scheme<CrankNicolsonScheme>(Get("alpha"), op, new CrankNicolsonScheme(0.5, op));
            double dt = Get("dt");
            double t = Get("from");
            double[] a = c.Doubles("initial");
            scheme.SetStep(dt);
            var values = new List<object?>();
            for (int k = 0; k < spec.GetProperty("steps").GetInt32(); ++k)
            {
                scheme.Step(a, t);
                t -= dt;
                values.Add(a.ToArray());
            }

            check.Output(c, "values", values);
            check.Output(c, "numberOfIterations", scheme.NumberOfIterations);
        }

        check.Complete();
    }
}
