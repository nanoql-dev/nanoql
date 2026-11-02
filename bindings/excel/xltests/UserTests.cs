// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// A user's assembly built by the tool (XLR8, XLP27): its curve made by its own function in the user's prefix, read by
/// its own getter, and priced by nanoql's, exactly as its code computes the discount.
/// </summary>
public class UserTests
{
    /// <summary>Acme's exponential curve, its 5-year discount through nqlYieldTermStructureDiscount2.</summary>
    [Fact]
    public void AUsersCurve()
        => OwnExcel.Run(AddIns.Acme, app =>
        {
            Worksheet ws = Xl.Sheet(Xl.Retry(() => app.Workbooks.Add()));
            Xl.Put(
                ws,
                ("A1", "=DATE(2015,9,15)"),
                ("A3", "=nqlConventionSetDayCounter(nqlConventionSetQuantLib(),\"Actual365Fixed\")"),
                ("A4", "=acmeExponentialCurve(A1,0.03,0.001,A3)"),
                ("A5", "=acmeExponentialCurveRate(A4)"),
                ("A6", "=nqlYieldTermStructureDiscount2(A4,5)"),
                ("A7", "=nqlxlError(A6)"));
            Xl.Calc(app);
            Assert.StartsWith("ExponentialCurve#", Xl.Text(ws, "A4"), StringComparison.Ordinal);
            Assert.Equal(0.03, Xl.Get(ws, "A5"));
            double rate = 0.03, decay = 0.001, t = 5;
            Assert.Equal(Xl.Bits(Math.Exp((-rate * t) - (0.5 * decay * t * t))), Xl.Bits(Xl.Get(ws, "A6")));
        });
}
