// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

[Trait("Oracle", "QuantLib")]
public class FdmArithmeticAverageConditionVectorTests
{
    [Fact]
    public void FdmArithmeticAverageConditionVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/stepconditions/fdmarithmeticaveragecondition");
        var check = new VectorAssert(file);
        double lo = Math.Log(50.0), hi = Math.Log(200.0);
        foreach (VectorCase c in file.Cases)
        {
            int[] sizes = c.Ints("sizes");
            var mesher = new FdmMesherComposite([new Uniform1dMesher(lo, hi, sizes[0]), new Uniform1dMesher(lo, hi, sizes[1])]);
            var condition = new FdmArithmeticAverageCondition(c.Doubles("averageTimes"), 0.0, c.Int("pastFixings"), mesher, c.Int("direction"));
            check.Output(c, "values", Applied(condition, c.Doubles("values"), c.Double("t")));
        }

        check.Complete();
    }
}
