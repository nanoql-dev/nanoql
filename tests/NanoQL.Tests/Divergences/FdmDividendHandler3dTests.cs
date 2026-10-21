// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Methods;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="FdmDividendHandler"/> on a grid of three dimensions. At a dividend's time QuantLib moves, for each
/// direction other than the equity's, only the lines along the equity direction through that direction's points with
/// every further coordinate 0 (<c>fdmdividendhandler.cpp:84-103</c>): all of a two-dimensional grid, but in three
/// dimensions the lines with two nonzero other coordinates stay as they were, 6 of a 3 × 5 × 4 grid's 12, while the
/// lines it moves equal its one-dimensional handler's. nanoql moves every line as the one-dimensional handler does.
/// </summary>
public class FdmDividendHandler3dTests
{
    [Fact]
    public void EveryLineMovesAsTheOneDimensionalHandlerMovesIt()
    {
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        var equity = new Uniform1dMesher(Math.Log(50.0), Math.Log(200.0), 5);
        var grid = new FdmMesherComposite(new Uniform1dMesher(0.0, 1.0, 3), equity, new Uniform1dMesher(0.0, 1.0, 4));
        ImmutableArray<Dividend> dividends = [new FixedDividend(10.0, today + 91)];
        var handler = new FdmDividendHandler(dividends, grid, today, new Actual365Fixed(), 1, context);
        var line = new FdmDividendHandler(dividends, new FdmMesherComposite(equity), today, new Actual365Fixed(), 0, context);
        double t = handler.DividendTimes[0];

        double[] a0 = [.. Enumerable.Range(0, 60).Select(i => (i % 7) + (0.1 * i))];
        double[] a = [.. a0];
        handler.ApplyTo(a, t);

        int movedWhereQuantLibDoesnt = 0;
        for (int c0 = 0; c0 < 3; c0++)
        {
            for (int c2 = 0; c2 < 4; c2++)
            {
                int[] index = [.. Enumerable.Range(0, 5).Select(k => c0 + (3 * k) + (15 * c2))];

                // QuantLib's check: its one-dimensional handler on the same line
                double[] expected = [.. index.Select(i => a0[i])];
                line.ApplyTo(expected, t);
                Assert.Equal(expected, index.Select(i => a[i]).ToArray());
                if (c0 != 0 && c2 != 0)
                {
                    Assert.NotEqual(index.Select(i => a0[i]).ToArray(), index.Select(i => a[i]).ToArray());
                    movedWhereQuantLibDoesnt++;
                }
            }
        }

        Assert.Equal(6, movedWhereQuantLibDoesnt);
    }
}
