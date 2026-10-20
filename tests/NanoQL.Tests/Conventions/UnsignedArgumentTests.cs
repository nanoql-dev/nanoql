// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Where QuantLib takes an unsigned value (<c>Size</c>, <c>Natural</c>, <c>BigNatural</c>), nanoql takes an
/// <see cref="int"/> or a <see cref="long"/> and range-checks it on input (playbook §4), throwing
/// <see cref="ArgumentOutOfRangeException"/> with the parameter's name. QuantLib can't be given these values, so no
/// vector holds them.
/// </summary>
public class UnsignedArgumentTests
{
    [Fact]
    public void APoissonCountIsInTheUnsigned32BitRange()
    {
        var pmf = new PoissonDistribution(1.0);
        var cdf = new CumulativePoissonDistribution(1.0);

        foreach (long k in new[] { -1L, 4294967296L, long.MinValue })
        {
            Assert.Equal("k", Assert.Throws<ArgumentOutOfRangeException>(() => pmf.Evaluate(k)).ParamName);
            Assert.Equal("k", Assert.Throws<ArgumentOutOfRangeException>(() => cdf.Evaluate(k)).ParamName);
        }
    }

    [Fact]
    public void AFactorialOrAPrimeIndexIsNotNegative()
    {
        Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => Factorial.Get(-1)).ParamName);
        Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => Factorial.Ln(-1)).ParamName);
        Assert.Equal(
            "absoluteIndex",
            Assert.Throws<ArgumentOutOfRangeException>(() => PrimeNumbers.Get(-1)).ParamName);
    }

    [Fact]
    public void ASizeOrAnIndexIsNotNegative()
    {
        var v = new Vector(3, 1.0);
        var m = new Matrix(2, 2, 1.0);

        Assert.Equal("size", Assert.Throws<ArgumentOutOfRangeException>(() => new Vector(-1)).ParamName);
        Assert.Equal("size", Assert.Throws<ArgumentOutOfRangeException>(() => new Vector(-1, 0.0)).ParamName);
        Assert.Equal("size", Assert.Throws<ArgumentOutOfRangeException>(() => new Vector(-1, 0.0, 1.0)).ParamName);
        Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => v.Resize(-1)).ParamName);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => v.At(-1)).ParamName);
        Assert.Equal("rows", Assert.Throws<ArgumentOutOfRangeException>(() => new Matrix(-1, 2)).ParamName);
        Assert.Equal("columns", Assert.Throws<ArgumentOutOfRangeException>(() => new Matrix(2, -1)).ParamName);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => m.At(-1)).ParamName);
        Assert.Equal("j", Assert.Throws<ArgumentOutOfRangeException>(() => m.Column(-1)).ParamName);
        Assert.Equal("size", Assert.Throws<ArgumentOutOfRangeException>(() => new TridiagonalOperator(-1)).ParamName);
    }

    [Fact]
    public void ABinomialCountIsInTheUnsigned32BitRange()
    {
        foreach (long k in new[] { -1L, 4294967296L, long.MinValue })
        {
            Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => new BinomialDistribution(0.5, k)).ParamName);
            Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => new CumulativeBinomialDistribution(0.5, k)).ParamName);
            Assert.Equal("k", Assert.Throws<ArgumentOutOfRangeException>(() => new BinomialDistribution(0.5, 10).Evaluate(k)).ParamName);
            Assert.Equal("k", Assert.Throws<ArgumentOutOfRangeException>(() => new CumulativeBinomialDistribution(0.5, 10).Evaluate(k)).ParamName);
            Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => BinomialDistributionFunctions.BinomialCoefficientLn(k, 0)).ParamName);
            Assert.Equal("k", Assert.Throws<ArgumentOutOfRangeException>(() => BinomialDistributionFunctions.BinomialCoefficient(5, k)).ParamName);
            Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => BinomialDistributionFunctions.PeizerPrattMethod2Inversion(0.1, k)).ParamName);
        }

        // the range's top is a BigNatural's largest value
        Assert.Equal(1.0, new CumulativeBinomialDistribution(0.5, 4294967295L).Evaluate(4294967295L));
    }

    [Fact]
    public void AnIntegratorsOrSolversCountIsNotNegative()
    {
        Func<Vector, Vector> identity = x => x;

        Assert.Equal("maxEvaluations", Assert.Throws<ArgumentOutOfRangeException>(() => new SimpsonIntegral(1e-6, -1)).ParamName);
        Assert.Equal("maxEvaluations", Assert.Throws<ArgumentOutOfRangeException>(() => new GaussLobattoIntegral(-1, 1e-6)).ParamName);
        Assert.Equal("maxEvaluations", Assert.Throws<ArgumentOutOfRangeException>(() => new SimpsonIntegral(1e-6, 10).SetMaxEvaluations(-1)).ParamName);
        Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => new GaussLegendreIntegrator(-1)).ParamName);
        Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => new GaussLegendreIntegration(-1)).ParamName);
        Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => new GaussLegendrePolynomial().Value(-1, 0.5)).ParamName);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => new GaussLaguerrePolynomial().Alpha(-1)).ParamName);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => new GaussJacobiPolynomial(0.5, 0.5).Beta(-1)).ParamName);
        Assert.Equal("ns", Assert.Throws<ArgumentOutOfRangeException>(
            () => new MultiDimGaussianIntegration([2, -1], n => new GaussLegendreIntegration(n))).ParamName);
        Assert.Equal("maxIter", Assert.Throws<ArgumentOutOfRangeException>(() => new BiCGstab(identity, -1, 1e-8)).ParamName);
        Assert.Equal("maxIter", Assert.Throws<ArgumentOutOfRangeException>(() => new GMRES(identity, -1, 1e-8)).ParamName);
        Assert.Equal("restart", Assert.Throws<ArgumentOutOfRangeException>(
            () => new GMRES(identity, 2, 1e-8).SolveWithRestart(-1, new Vector([1.0, 1.0]))).ParamName);
        Assert.Equal("size1", Assert.Throws<ArgumentOutOfRangeException>(() => new SparseMatrix(-1, 2)).ParamName);
        Assert.Equal("size2", Assert.Throws<ArgumentOutOfRangeException>(() => new SparseMatrix(2, -1)).ParamName);
        Assert.Equal("nonZeros", Assert.Throws<ArgumentOutOfRangeException>(() => new SparseMatrix(2, 2, -1)).ParamName);
        Assert.Equal("maxEvaluations", Assert.Throws<ArgumentOutOfRangeException>(
            () => new InverseNonCentralCumulativeChiSquareDistribution(3.0, 1.0, -1)).ParamName);
    }

    [Fact]
    public void FixingAndSettlementDaysAreNotNegative()
    {
        var calendar = new TARGET();
        var dc = new Actual360();
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2024) };

        foreach (Func<object> create in new Func<object>[]
        {
            () => new IborIndex("Euribor", new Period(6, TimeUnit.Months), -1, null, calendar, BusinessDayConvention.Following, false, dc),
            () => new OvernightIndex("ESTR", int.MinValue, null, calendar, dc),
            () => FlatForward.Create(-1, calendar, 0.03, dc, context),
            () => ConstantOptionletVolatility.Create(-1, calendar, BusinessDayConvention.Following, 0.2, dc, context),
        })
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(create);
            Assert.Equal("settlementDays", e.ParamName);
            Assert.True(e.ActualValue is int days && days < 0);
        }
    }
}
