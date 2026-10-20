// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="TqrEigenDecomposition"/> where QuantLib's iteration never returns. QuantLib loops while an off-diagonal
/// element is above its test (<c>tqreigendecomposition.cpp:47</c>), with no bound on the iterations: under
/// <c>NoShift</c> it never returns on eigenvalues in pairs of opposite sign, and under any strategy on a NaN, an
/// infinite off-diagonal element or entries whose squares overflow, which QuantLib's Gauss-Jacobi quadrature reaches
/// with parameters of 1e200 (its recurrence's alpha is ∞ − ∞, <c>gaussianorthogonalpolynomial.cpp:119</c>). While one
/// eigenvalue converges, the next step is a function of the diagonal and off-diagonal alone, so a state repeated bit
/// for bit proves the iteration endless: nanoql's cycle test refuses there, and every input QuantLib answers keeps
/// QuantLib's result. Each input below never returns in QuantLib (killed at 5 s in the C++ harness, 10 s on the wheel).
/// The test catches the inputs whose state repeats, as a zero diagonal's does, but not every pair: under
/// <c>NoShift</c> a pair whose magnitudes differ slightly converges at their ratio without repeating a state, and one
/// equal only to rounding may never repeat one either. Such an input runs as long as QuantLib's, without end once the
/// ratio rounds to 1: a QuantLib defect kept, since no bound on the iterations would separate it from slow inputs
/// QuantLib answers.
/// </summary>
public class TqrEigenDecompositionCycleTests
{
    private const string Repeats = "the QR iteration repeats a state, so it never converges";

    [Fact]
    public void PairedEigenvaluesUnderNoShiftAreRefused()
    {
        var e = Assert.Throws<NanoQLException>(() => new TqrEigenDecomposition(
            new Vector([0.0, 0.0]),
            new Vector([1.0]),
            TqrEigenDecompositionEigenVectorCalculation.WithoutEigenVector,
            TqrEigenDecompositionShiftStrategy.NoShift));
        Assert.Equal(Repeats, e.Message);

        // QuantLib's check: the same matrix under its shifted strategies, which QuantLib decomposes in 17 and 2 steps
        var shifted = new TqrEigenDecomposition(
            new Vector([0.0, 0.0]),
            new Vector([1.0]),
            TqrEigenDecompositionEigenVectorCalculation.WithoutEigenVector,
            TqrEigenDecompositionShiftStrategy.CloseEigenValue);
        Assert.Equal(2, shifted.Iterations);
        Assert.Equal([0.9999999999999998, -0.9999999999999998], shifted.Eigenvalues().ToArray());
    }

    [Fact]
    public void ANaNOffDiagonalIsRefused()
    {
        var e = Assert.Throws<NanoQLException>(
            () => new TqrEigenDecomposition(new Vector([1.0, 2.0]), new Vector([double.NaN])));
        Assert.Equal(Repeats, e.Message);
    }

    [Fact]
    public void AGaussJacobiQuadratureWithOverflowingParametersIsRefused()
    {
        var e = Assert.Throws<NanoQLException>(() => new GaussJacobiIntegration(3, 1e200, 1e200));
        Assert.Equal(Repeats, e.Message);
    }
}
