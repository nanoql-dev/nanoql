// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The inner-value calculators' vectors,
/// <c>tests/vectors/methods/finitedifferences/utilities/fdminnervaluecalculator.json</c>:
/// <see cref="FdmLogInnerValue"/>, <see cref="FdmCellAveragingInnerValue"/> and <see cref="FdmZeroInnerValue"/>, their
/// inner and cell-averaged values at every layout point for five payoffs on log-spot, spot and two-dimensional meshers.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmInnerValueCalculatorVectorTests
{
    [Fact]
    public void FdmInnerValueCalculatorVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/utilities/fdminnervaluecalculator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), c.Context());
            FdmInnerValueCalculator calculator = CalculatorOf(c.Input("calculator"), mesher);
            double t = c.Double("t");
            var inner = new double[mesher.Layout.Size];
            var avg = new double[mesher.Layout.Size];
            FdmLinearOpIterator iter = mesher.Layout.Begin();
            for (int i = 0; i < mesher.Layout.Size; i++, iter.Increment())
            {
                inner[i] = calculator.InnerValue(iter, t);
                avg[i] = calculator.AvgInnerValue(iter, t);
            }

            check.Output(c, "innerValue", inner);
            check.Output(c, "avgInnerValue", avg);
        }

        check.Complete();
    }
}
