// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmAmericanStepCondition vectors,
/// <c>tests/vectors/methods/finitedifferences/stepconditions/fdmamericanstepcondition.json</c>:
/// <see cref="FdmAmericanStepCondition.ApplyTo"/> on three meshers with four calculators, and the refusal of an array
/// of another size.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmAmericanStepConditionVectorTests
{
    [Fact]
    public void FdmAmericanStepConditionVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/stepconditions/fdmamericanstepcondition");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), c.Context());
            var condition = new FdmAmericanStepCondition(mesher, CalculatorOf(c.Input("calculator"), mesher));
            double t = c.Double("t");
            double[][] arrays = [.. c.Input("arrays").EnumerateArray().Select(Doubles)];
            check.Output(c, "applyTo", arrays.Select(a => (object?)Applied(condition, a, t)).ToArray());
            double[] shortArray = arrays[0][..^1];
            check.Output(c, "applyToShort", () =>
            {
                condition.ApplyTo(shortArray, t);
                return null;
            });
        }

        check.Complete();
    }
}
