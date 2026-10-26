// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BasketInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The basket payoff vectors, <c>tests/vectors/instruments/basketoption.json</c>: the minimum, maximum, average and
/// spread payoffs over three base payoffs, at single prices (<see cref="BasketPayoff.Evaluate(double)"/>) and on the
/// assets' prices at every point of a predefined mesher of their logs
/// (<see cref="BasketPayoff.Evaluate(ReadOnlySpan{double})"/>, through <see cref="FdmLogBasketInnerValue"/>).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BasketOptionVectorTests
{
    [Fact]
    public void BasketOptionVector()
    {
        var file = VectorFile.Load("instruments/basketoption");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            BasketPayoff payoff = BasketPayoffOf(c.Input("payoff"));
            check.Output(c, "value", c.Doubles("prices").Select(payoff.Evaluate).ToArray());

            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), c.Context());
            var calc = new FdmLogBasketInnerValue(payoff, mesher);
            double[] basket = new double[mesher.Layout.Size];
            FdmLinearOpIterator iter = mesher.Layout.Begin();
            for (int k = 0; k < mesher.Layout.Size; ++k, iter.Increment())
            {
                basket[k] = calc.InnerValue(iter, 0.0);
            }

            check.Output(c, "basket", basket);
        }

        check.Complete();
    }
}
