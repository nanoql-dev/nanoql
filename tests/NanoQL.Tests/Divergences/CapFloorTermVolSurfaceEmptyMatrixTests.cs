// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A cap/floor term-volatility surface on an empty matrix. QuantLib's constructors on quotes size their matrix from
/// the first row, <c>vols_(vols.size(), vols[0].size())</c>, before any check (<c>capfloortermvolsurface.cpp:45,
/// 77</c>), which is undefined for no rows: the wheel ends the process (0xC0000005 in all 18 recorded runs, with or
/// without tenors). Its constructors on a matrix check it first, in <c>checkInputs</c>
/// (<c>capfloortermvolsurface.cpp:154-177</c>): "empty option tenor vector", or "mismatch between number of option tenors
/// (2) and number of volatility rows (0)", the wheel's texts. nanoql's quote and matrix forms are one (R1's quote row),
/// so an empty matrix reaches QuantLib's own checks, refused with <see cref="ArgumentException"/>.
/// </summary>
public class CapFloorTermVolSurfaceEmptyMatrixTests
{
    [Fact]
    public void AnEmptyMatrixReachesQuantLibsChecks()
    {
        var reference = new Date(16, Month.June, 2025);
        double[] strikes = [0.02, 0.03];

        ArgumentException noTenors = Assert.Throws<ArgumentException>(() => new CapFloorTermVolSurface(
            reference, new TARGET(), BusinessDayConvention.Following, [], strikes, new Matrix(0, 0)));
        Assert.StartsWith("empty option tenor vector", noTenors.Message, StringComparison.Ordinal);

        ArgumentException noRows = Assert.Throws<ArgumentException>(() => new CapFloorTermVolSurface(
            reference, new TARGET(), BusinessDayConvention.Following, [new Period(1, TimeUnit.Years), new Period(2, TimeUnit.Years)],
            strikes, new Matrix(0, 0)));
        Assert.StartsWith(
            "mismatch between number of option tenors (2) and number of volatility rows (0)", noRows.Message, StringComparison.Ordinal);
    }
}
