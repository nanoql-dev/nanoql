// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using ExcelDna.Integration;

namespace NanoQL.Excel;

/// <summary>Excel itself, behind the runtime's seam: the C API for the cell, its values and the date system, RTD for a handle.</summary>
internal sealed class ExcelHost : IXlHost
{
    /// <inheritdoc/>
    public ExcelReference? Caller()
    {
        try
        {
            return XlCall.Excel(XlCall.xlfCaller) as ExcelReference;
        }
        catch (XlCallException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public object ValueOf(ExcelReference r) => r.GetValue();

    /// <inheritdoc/>
    /// <remarks>
    /// Through Excel's object model, on Excel's main thread (nqlxlError isn't registered thread-safe): the cell's
    /// <c>Range.HasSpill</c> and <c>Range.SpillParent</c>, the state Excel itself shows. Each object the read takes is
    /// released before it returns: one left to the finalizer kept Excel from closing (XLP79).
    /// </remarks>
    public ExcelReference? SpillParent(ExcelReference cell)
    {
        string sheet = (string)XlCall.Excel(XlCall.xlSheetNm, cell);
        int b = sheet.LastIndexOf(']');  // a workbook's name may hold "]", a sheet's can't
        var taken = new List<object>();
        object Take(object o)
        {
            taken.Add(o);
            return o;
        }

        try
        {
            object book = Take(Get(Take(Get(ExcelDnaUtil.Application, "Workbooks")), "Item", sheet[1..b]));
            object ws = Take(Get(Take(Get(book, "Worksheets")), "Item", sheet[(b + 1)..]));
            object range = Take(Get(Take(Get(ws, "Cells")), "Item", cell.RowFirst + 1, cell.ColumnFirst + 1));
            if (!Convert.ToBoolean(Get(range, "HasSpill"), CultureInfo.InvariantCulture))
            {
                return null;
            }

            object anchor = Take(Get(range, "SpillParent"));
            int row = Convert.ToInt32(Get(anchor, "Row"), CultureInfo.InvariantCulture) - 1;
            int col = Convert.ToInt32(Get(anchor, "Column"), CultureInfo.InvariantCulture) - 1;
            return new ExcelReference(row, row, col, col, cell.SheetId);
        }
        finally
        {
            for (int k = taken.Count - 1; k >= 0; k--)
            {
                if (taken[k] != null && Marshal.IsComObject(taken[k]))
                {
                    Marshal.ReleaseComObject(taken[k]);
                }
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>Excel gives <c>DATE(1904,1,1)</c> as 0 in the 1904 system and 1,462 in the 1900 one (XLP16).</remarks>
    public int DateShift()
    {
        try
        {
            return XlCall.Excel(XlCall.xlfDate, 1904, 1, 1) is double d && d == 0 ? 1462 : 0;
        }
        catch (XlCallException)
        {
            return 0;
        }
    }

    /// <inheritdoc/>
    public object Present(XlEntry entry) => ExcelAsyncUtil.Observe("nqlxl", new object[] { entry.Key }, () => new XlHolder(entry));

    private static object Get(object target, string name, params object[] args)
        => target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, args, CultureInfo.InvariantCulture)!;
}
