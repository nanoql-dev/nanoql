// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The sparse ILU's fill levels (the S-core plan's CR7 (xxiv)): QuantLib records each new entry's level from the levels'
/// nonzero entries by the entries' rank among the row's nonzero values, which a value that cancels to 0 shifts, so a
/// later row reads another entry's level; nanoql records each entry's own. On the first of the planner's random 8 × 8
/// matrices with exact cancellations whose factors move at fill level 1 (Mersenne Twister seed 5, the 36th), QuantLib
/// drops U's fill entry (4, 7) and keeps U(7, 7) at 4; the levels at their own positions keep (4, 7) at −0.5 and give
/// U(7, 7) 4.1875. QuantLib's factors and those of its own constructor with the one read changed are its own (this run's
/// <c>probes\c4_facts.cpp</c>, <c>c4_facts.out</c>).
/// </summary>
public class SparseIluTests
{
    private static readonly double[,] A =
    {
        { 4, 0, 2, 0, 2, 1, 0, 0 },
        { 0, 4, 0, 0, 1, 1, 0, 0 },
        { 0, 0, 4, 0, 0, 0, 1, 0 },
        { 2, 0, 0, 4, 1, 0, 0, 1 },
        { 0, 0, 0, 2, 4, 1, 0, 0 },
        { 2, 2, 2, 0, 0, 4, 0, 0 },
        { 0, 0, 0, 0, 0, 2, 4, 0 },
        { 0, 2, 0, 0, 2, 0, 0, 4 },
    };

    // QuantLib's constructor with each level at its own position: its L, which QuantLib's also is, and its U
    private static readonly double[,] FixedL =
    {
        { 1, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 1, 0, 0, 0, 0, 0, 0 },
        { 0, 0, 1, 0, 0, 0, 0, 0 },
        { 0.5, 0, -0.25, 1, 0, 0, 0, 0 },
        { 0, 0, 0, 0.5, 1, 0, 0, 0 },
        { 0.5, 0.5, 0.25, 0, -0.375, 1, 0, 0 },
        { 0, 0, 0, 0, 0, 0.57657657657657657, 1, 0 },
        { 0, 0.5, 0, 0, 0.375, -0.27927927927927926, 0, 1 },
    };

    private static readonly double[,] FixedU =
    {
        { 4, 0, 2, 0, 2, 1, 0, 0 },
        { 0, 4, 0, 0, 1, 1, 0, 0 },
        { 0, 0, 4, 0, 0, 0, 1, 0 },
        { 0, 0, 0, 4, 0, -0.5, 0, 1 },
        { 0, 0, 0, 0, 4, 1.25, 0, -0.5 },
        { 0, 0, 0, 0, 0, 3.46875, -0.25, 0 },
        { 0, 0, 0, 0, 0, 0, 4.1441441441441444, 0 },
        { 0, 0, 0, 0, 0, 0, 0, 4.1875 },
    };

    private static SparseMatrix Sparse(double[,] m)
    {
        var s = new SparseMatrix(m.GetLength(0), m.GetLength(1));
        for (int i = 0; i < m.GetLength(0); ++i)
        {
            for (int j = 0; j < m.GetLength(1); ++j)
            {
                if (m[i, j] != 0.0)
                {
                    s[i, j] = m[i, j];
                }
            }
        }

        return s;
    }

    [Fact]
    public void EachFillEntryKeepsItsOwnLevel()
    {
        var ilu = new SparseILUPreconditioner(Sparse(A), 1);
        SparseMatrix l = ilu.L(), u = ilu.U();
        for (int i = 0; i < 8; ++i)
        {
            for (int j = 0; j < 8; ++j)
            {
                Assert.Equal(FixedL[i, j], l[i, j]);
                Assert.Equal(FixedU[i, j], u[i, j]);
            }
        }

        // QuantLib's U(4, 7) 0 and U(7, 7) 4
        Assert.Equal(-0.5, u[4, 7]);
        Assert.Equal(4.1875, u[7, 7]);
    }
}
