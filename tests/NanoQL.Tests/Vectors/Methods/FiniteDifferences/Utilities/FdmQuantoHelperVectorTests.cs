// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmQuantoHelper vectors, <c>tests/vectors/methods/finitedifferences/utilities/fdmquantohelper.json</c>:
/// <see cref="FdmQuantoHelper.QuantoAdjustment(double, double, double)"/> read through the
/// <see cref="FdmBlackScholesOp"/> it adjusts, its apply and splitting solve after three time steps, over helpers with
/// five correlations, three exchange-rate volatilities, two foreign curves, two at-the-money levels and another
/// domestic curve, with the Black and the local volatility.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmQuantoHelperVectorTests
{
    [Fact]
    public void FdmQuantoHelperVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/utilities/fdmquantohelper");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (FdmBlackScholesOp op, _) = OperatorOf(c.Input("op"), c.Context());
            Vector r = VectorOf(c.Input("r"));
            double s = c.Double("s");
            var apply = new List<object?>();
            var solve = new List<object?>();
            foreach (var t in c.Input("times").EnumerateArray())
            {
                op.SetTime(t[0].GetDouble(), t[1].GetDouble());
                apply.Add(op.Apply(r).ToArray());
                solve.Add(op.SolveSplitting(0, r, s).ToArray());
            }

            check.Output(c, "apply", apply);
            check.Output(c, "solveSplitting", solve);
        }

        check.Complete();
    }
}
