// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmAffineModelSwapInnerValue vectors,
/// <c>tests/vectors/methods/finitedifferences/utilities/fdmaffinemodelswapinnervalue.json</c>: a swap's exercise
/// value at every point of the G2 and Hull-White engines' meshers at each exercise time, on a discounting and a
/// forwarding model, through the two named inner values.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmAffineModelSwapInnerValueVectorTests
{
    [Fact]
    public void FdmAffineModelSwapInnerValueVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/utilities/fdmaffinemodelswapinnervalue");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            ShortRateModel dis = ShortRateFdInputs.ModelOf(c.Input("model"));
            ShortRateModel fwd = ShortRateFdInputs.ModelOf(c.Input("forwardingModel"));
            FixedVsFloatingSwap swap = SwapInputs.SwapOf(c.Input("swap"), context);
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), context);
            YieldTermStructure curve = ((ITermStructureConsistentModel)dis).TermStructure;
            var t2d = new SortedDictionary<double, Date>();
            foreach (Date d in c.Input("exerciseDates").EnumerateArray().Select(d => new Date(d.GetInt32())))
            {
                t2d[curve.DayCounter.YearFraction(curve.ReferenceDate, d)] = d;
            }

            FdmInnerValueCalculator calculator = dis is G2 g2
                ? new FdmAffineG2ModelSwapInnerValue(g2, (G2)fwd, swap, t2d, mesher, 0, context)
                : new FdmAffineHullWhiteModelSwapInnerValue((HullWhite)dis, (HullWhite)fwd, swap, t2d, mesher, 0, context);
            int n = mesher.Layout.Size;
            check.Output(c, "innerValue", t2d.Keys.Select(t => (object?)Values(n, mesher, iter => calculator.InnerValue(iter, t))).ToArray());
            double first = t2d.Keys.First();
            check.Output(c, "avgInnerValue", Values(n, mesher, iter => calculator.AvgInnerValue(iter, first)));
        }

        check.Complete();
    }

    private static double[] Values(int n, FdmMesher mesher, Func<FdmLinearOpIterator, double> f)
    {
        double[] values = new double[n];
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int i = 0; i < n; i++, iter.Increment())
        {
            values[i] = f(iter);
        }

        return values;
    }
}
