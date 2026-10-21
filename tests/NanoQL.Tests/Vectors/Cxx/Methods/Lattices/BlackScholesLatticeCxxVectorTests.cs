// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Black-Scholes lattice vectors, <c>tests/vectors/cxx/methods/lattices/bsmlattice.json</c>:
/// <see cref="BlackScholesLattice{T}"/> over three binomial trees, its
/// <see cref="BlackScholesLattice{T}.RiskFreeRate"/> and <see cref="BlackScholesLattice{T}.Dt"/>, every step's size,
/// every node's discount and underlying, every node's and branch's descendant and probability,
/// <see cref="BlackScholesLattice{T}.Stepback"/> from seeded values, and
/// <see cref="TreeLattice1D.Grid(double)"/> at each step's time.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackScholesLatticeCxxVectorTests
{
    [Fact]
    public void BlackScholesLatticeCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/lattices/bsmlattice");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("lattice");
            Tree tree = BinomialTreeCxxVectorTests.TreeOf(spec.GetProperty("tree"));
            double rate = spec.GetProperty("riskFreeRate").GetDouble(), end = spec.GetProperty("end").GetDouble();
            int steps = spec.GetProperty("steps").GetInt32();
            TreeLattice1D lattice = tree switch
            {
                CoxRossRubinstein t => new BlackScholesLattice<CoxRossRubinstein>(t, rate, end, steps),
                JarrowRudd t => new BlackScholesLattice<JarrowRudd>(t, rate, end, steps),
                LeisenReimer t => new BlackScholesLattice<LeisenReimer>(t, rate, end, steps),
                _ => throw new ArgumentException($"no lattice case on {tree.GetType().Name}", nameof(c)),
            };
            (double riskFreeRate, double dt) = lattice switch
            {
                BlackScholesLattice<CoxRossRubinstein> l => (l.RiskFreeRate, l.Dt),
                BlackScholesLattice<JarrowRudd> l => (l.RiskFreeRate, l.Dt),
                BlackScholesLattice<LeisenReimer> l => (l.RiskFreeRate, l.Dt),
                _ => throw new InvalidOperationException(),
            };
            int[] sizes = [.. Enumerable.Range(0, steps + 1).Select(lattice.Size)];
            check.Output(c, "riskFreeRate", riskFreeRate);
            check.Output(c, "dt", dt);
            check.Output(c, "size", sizes.Select(n => (object?)n).ToArray());
            check.Output(
                c,
                "discount",
                Enumerable.Range(0, steps + 1)
                    .Select(i => Enumerable.Range(0, sizes[i]).Select(j => (object?)lattice.Discount(i, j)).ToArray()).ToArray());
            check.Output(
                c,
                "underlying",
                Enumerable.Range(0, steps + 1)
                    .Select(i => Enumerable.Range(0, sizes[i]).Select(j => (object?)lattice.Underlying(i, j)).ToArray()).ToArray());
            check.Output(
                c,
                "descendant",
                Enumerable.Range(0, steps)
                    .Select(i => Enumerable.Range(0, sizes[i])
                        .Select(j => Enumerable.Range(0, 2).Select(b => (object?)lattice.Descendant(i, j, b)).ToArray()).ToArray())
                    .ToArray());
            check.Output(
                c,
                "probability",
                Enumerable.Range(0, steps)
                    .Select(i => Enumerable.Range(0, sizes[i])
                        .Select(j => Enumerable.Range(0, 2).Select(b => (object?)lattice.Probability(i, j, b)).ToArray()).ToArray())
                    .ToArray());
            JsonElement[] values = [.. c.Input("values").EnumerateArray()];
            check.Output(
                c,
                "stepback",
                Enumerable.Range(0, steps).Select(i =>
                {
                    double[] next = Doubles(values[i]);
                    double[] result = new double[sizes[i]];
                    lattice.Stepback(i, next, result);
                    return (object?)result;
                }).ToArray());
            check.Output(c, "grid", Doubles(c.Input("times")).Select(t => (object?)lattice.Grid(t).ToArray()).ToArray());
        }

        check.Complete();
    }
}
