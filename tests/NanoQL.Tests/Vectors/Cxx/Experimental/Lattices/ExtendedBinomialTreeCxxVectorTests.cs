// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's extended binomial tree vectors, <c>tests/vectors/cxx/experimental/lattices/extendedbinomialtree.json</c>:
/// each of the seven trees on a constant and a time-dependent volatility, its columns, node values and up probabilities.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ExtendedBinomialTreeCxxVectorTests
{
    [Fact]
    public void ExtendedBinomialTreeCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/lattices/extendedbinomialtree");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var reference = new Date(c.Int("referenceDate"));
            DayCounter dc = new Actual365Fixed();
            double[] vols = c.Doubles("vols");
            BlackVolTermStructure vol = vols.Length == 1
                ? new BlackConstantVol(reference, new NullCalendar(), vols[0], dc)
                : new BlackVarianceCurve(reference, [reference + 365, reference + 730, reference + 1825], vols, dc);
            var process = new BlackScholesMertonProcess(
                c.Double("spot"), new FlatForward(reference, c.Double("q"), dc), new FlatForward(reference, c.Double("r"), dc), vol);
            double end = c.Double("end"), strike = c.Double("strike");
            int steps = c.Int("steps");
            Tree tree = c.String("type") switch
            {
                "ExtendedJarrowRudd" => new ExtendedJarrowRudd(process, end, steps, strike),
                "ExtendedCoxRossRubinstein" => new ExtendedCoxRossRubinstein(process, end, steps, strike),
                "ExtendedAdditiveEQPBinomialTree" => new ExtendedAdditiveEQPBinomialTree(process, end, steps, strike),
                "ExtendedTrigeorgis" => new ExtendedTrigeorgis(process, end, steps, strike),
                "ExtendedTian" => new ExtendedTian(process, end, steps, strike),
                "ExtendedLeisenReimer" => new ExtendedLeisenReimer(process, end, steps, strike),
                _ => new ExtendedJoshi4(process, end, steps, strike),
            };
            int columns = c.Int("columns");
            var underlying = new List<double>();
            var probabilities = new List<double>();
            for (int i = 0; i < columns && i + 1 < tree.Columns; ++i)
            {
                for (int j = 0; j < tree.Size(i); ++j)
                {
                    underlying.Add(tree.Underlying(i, j));
                }

                probabilities.Add(tree.Probability(i, 0, 1));
            }

            check.Output(c, "columns", tree.Columns);
            check.Output(c, "underlying", underlying.ToArray());
            check.Output(c, "probabilities", probabilities.ToArray());
        }

        check.Complete();
    }
}
