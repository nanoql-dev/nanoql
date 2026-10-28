// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's TR-BDF2 vectors, <c>tests/vectors/cxx/methods/finitedifferences/trbdf2.json</c>: values rolled
/// back by <see cref="TRBDF2"/> through <see cref="FiniteDifferenceModel{TScheme}"/> on three time-independent
/// tridiagonal operators, over two intervals in 1 to 50 steps, with <see cref="ZeroCondition"/> and without. The wheel
/// exposes neither.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TRBDF2CxxVectorTests
{
    [Fact]
    public void TRBDF2CxxVector()
    {
        var file = VectorFile.Load("cxx/methods/finitedifferences/trbdf2");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var l = new TridiagonalOperator(new Vector(c.Doubles("low")), new Vector(c.Doubles("mid")), new Vector(c.Doubles("high")));
            var model = new FiniteDifferenceModel<TRBDF2>(new TRBDF2(l, []));
            double[] u = c.Doubles("values");
            if (c.Bool("zeroCondition"))
            {
                model.Rollback(u, c.Double("from"), c.Double("to"), c.Int("steps"), new ZeroCondition());
            }
            else
            {
                model.Rollback(u, c.Double("from"), c.Double("to"), c.Int("steps"));
            }

            check.Output(c, "values", u);
        }

        check.Complete();
    }
}
