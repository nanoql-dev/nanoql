// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Wiener operator in a direction past its count. QuantLib's <c>apply_direction</c> and <c>solve_splitting</c>
/// read <c>ops_[direction]</c> unchecked (<c>fdmwienerop.cpp:71-79</c>), past its operators' vector, undefined. nanoql
/// refuses such a direction (the playbook's undefined-behaviour row).
/// </summary>
public class FdmWienerOpDirectionTests
{
    [Fact]
    public void ADirectionPastTheOperatorsIsRefused()
    {
        var mesher = new FdmMesherComposite(new Uniform1dMesher(-1.0, 1.0, 5), new Uniform1dMesher(-1.0, 1.0, 4));
        var op = new FdmWienerOp(mesher, null, [0.04, 0.01]);
        double[] r = new double[20], result = new double[20];
        Assert.Throws<ArgumentOutOfRangeException>(() => op.ApplyDirection(2, r, result));
        Assert.Throws<ArgumentOutOfRangeException>(() => op.SolveSplitting(-1, r, 0.1, result));
    }
}
