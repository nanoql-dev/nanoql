// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's variance gamma model vectors, <c>tests/vectors/cxx/experimental/variancegamma/variancegammamodel.json</c>:
/// <see cref="VarianceGammaModel"/>'s sigma, nu, theta and parameters on four processes, as
/// <c>tools/cxx/src/experimental/variancegamma/variancegammamodel.cpp</c> builds them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class VarianceGammaModelCxxVectorTests
{
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void VarianceGammaModelCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/variancegamma/variancegammamodel");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement p = c.Input("process");
            Date today = context.EvaluationDate;
            var model = new VarianceGammaModel(new VarianceGammaProcess(
                p.GetProperty("s0").GetDouble(),
                new FlatForward(today, p.GetProperty("dividendYield").GetDouble(), A365),
                new FlatForward(today, p.GetProperty("riskFreeRate").GetDouble(), A365),
                p.GetProperty("sigma").GetDouble(),
                p.GetProperty("nu").GetDouble(),
                p.GetProperty("theta").GetDouble()));
            check.Output(c, "sigma", model.Sigma);
            check.Output(c, "nu", model.Nu);
            check.Output(c, "theta", model.Theta);
            check.Output(c, "params", model.Params().ToArray());
        }

        check.Complete();
    }
}
