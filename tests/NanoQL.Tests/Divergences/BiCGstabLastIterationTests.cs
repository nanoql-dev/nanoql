// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="BiCGstab"/> converging in its last allowed iteration. QuantLib's final checks test the iteration count
/// before the error (<c>bicgstab.cpp:87-88</c>): a full step that meets the tolerance at the bottom of the last
/// iteration still increments the count to the maximum, so the solve is refused, "max number of iterations exceeded"
/// (a half step that meets it breaks out before the increment, and passes). nanoql tests the error first, QuantLib's
/// own criterion; the count's refusal keeps its text, and a breakdown short of the tolerance keeps QuantLib's "could
/// not converge".
/// </summary>
public class BiCGstabLastIterationTests
{
    // A 6×6 system whose sixth full step meets 1e-8, at 8.31e-09
    private static readonly double[][] Rows =
    [
        [1.0315206099561902, 0.33005181210921886, 0.2993061708089244, -0.794221240019648, 0.6959638263752614, -0.8862895565621092],
        [-0.8356327587005159, 0.5139556671629724, 0.9817481534532106, -0.8702058525315075, -0.4730117840540282, -0.30539653981797366],
        [-0.36440076003707644, -0.5779728400889022, 1.7783347680977801, -0.3670318408327351, -0.8597809723095946, 0.656232740338073],
        [0.18994388432922915, -0.065980201421459, 0.38374729959391507, 0.5125024345903282, 0.1836359494679951, 0.5740903116647347],
        [-0.8806321960938746, -0.11440890288049066, 0.982719268018803, -0.23134781635714186, 1.7487664403878873, 0.49495149833350394],
        [-0.855450653495627, 0.17507347240125926, -0.04727938324777847, -0.4381260502264199, -0.2704117453852175, 1.2605579700996814],
    ];

    private static readonly double[] B =
        [-0.47297916496488845, 0.346055967474415, -0.6485406395881999, 0.29604490443693776, -0.549559879859314, 0.9604498918785667];

    [Fact]
    public void ASystemSolvedInTheLastIterationIsSolved()
    {
        var a = new Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                a[i, j] = Rows[i][j];
            }
        }

        // QuantLib throws "max number of iterations exceeded" with 6 iterations allowed
        BiCGStabResult result = new BiCGstab(x => a * x, 6, 1e-8).Solve(new Vector(B));

        // QuantLib's check: its own solve with 7 allowed stops at the same step, and returns this result
        Assert.Equal(6, result.Iterations);
        Assert.Equal(8.31045251463793e-09, result.Error);
        double[] quantLibWithSeven =
            [0.001984823370365766, 1.570464225102459, -0.1345893370918415, 0.3104383272570386, -0.26134809970241035, 0.5919421594509393];
        Assert.Equal(quantLibWithSeven, result.X.ToArray());
    }

    [Fact]
    public void ABreakdownShortOfTheToleranceIsRefused()
    {
        // The first step's omega is 0 and the second's rho is, the error still 1; QuantLib throws the same (the
        // wheel's vectors, case "breakdown")
        var a = new Matrix(2, 2);
        a[0, 0] = 1.0;
        a[0, 1] = 1.0;
        a[1, 0] = -1.0;
        var e = Assert.Throws<NanoQLException>(() => new BiCGstab(x => a * x, 5, 1e-8).Solve(new Vector([1.0, 0.0])));
        Assert.Equal("could not converge", e.Message);
    }
}
