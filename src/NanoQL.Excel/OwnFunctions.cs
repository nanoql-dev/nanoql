// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using ExcelDna.Integration;

namespace NanoQL.Excel;

/// <summary>
/// The add-in's own worksheet functions, under their own prefix, <c>nqlxl</c> (XLR13), registered from the runtime's
/// assembly beside the generated ones. <c>nqlxlStats</c> is generated into test builds alone (XLD25).
/// </summary>
public static class OwnFunctions
{
    /// <summary>The message of the error a cell's nanoql call returned, while the cell shows it.</summary>
    /// <param name="cell">The cell.</param>
    /// <returns>The message, or empty text.</returns>
    [ExcelFunction(Name = "nqlxlError", Category = "nanoql", Description = "The message of the error the cell's nanoql call returned, while the cell shows it")]
    public static object Error([ExcelArgument(Name = "cell", Description = "The cell showing the error (a reference)", AllowReference = true)] object cell)
        => cell is ExcelReference r && X.Host.ValueOf(r) is ExcelError ? X.ErrorOf(r) : string.Empty;

    /// <summary>The call that made a handle: its worksheet function and its arguments.</summary>
    /// <param name="handle">The handle.</param>
    /// <returns>The recipe's text, or #REF! for a handle this session doesn't hold.</returns>
    [ExcelFunction(Name = "nqlxlRecipe", Category = "nanoql", IsThreadSafe = true, Description = "The call that made a handle: its worksheet function and its arguments")]
    public static object Recipe([ExcelArgument(Name = "handle", Description = "The handle (text)")] object handle)
        => X.One(handle) is string h && X.RecipeText(h) is { } text ? text : ExcelError.ExcelErrorRef;

    /// <summary>nanoql's version and the .NET runtime's.</summary>
    /// <returns>The versions' text.</returns>
    [ExcelFunction(Name = "nqlxlVersion", Category = "nanoql", IsThreadSafe = true, Description = "nanoql's version and the .NET runtime the add-in runs on")]
    public static string Version()
    {
        Version v = typeof(OwnFunctions).Assembly.GetName().Version!;
        string nanoql = v.Build > 0 ? $"{v.Major}.{v.Minor}.{v.Build}" : $"{v.Major}.{v.Minor}";
        return string.Create(CultureInfo.InvariantCulture, $"nanoql {nanoql} runtime {Environment.Version}");
    }

    /// <summary>A batch of draws' values: a column, or a draw per row for sequences.</summary>
    /// <param name="draws">The draws' handle.</param>
    /// <returns>The values.</returns>
    [ExcelFunction(Name = "nqlxlDrawsValues", Category = "nanoql", IsThreadSafe = true, Description = "A batch of draws' values: a column, or a draw per row for sequences")]
    public static object DrawsValues([ExcelArgument(Name = "draws", Description = "The draws (a handle)")] object draws)
        => X.RunVal("nqlxlDrawsValues", [draws], static a => X.Obj<XlDraws>(a[0], "draws").Values);

    /// <summary>A batch of draws' weights, a column.</summary>
    /// <param name="draws">The draws' handle.</param>
    /// <returns>The weights.</returns>
    [ExcelFunction(Name = "nqlxlDrawsWeights", Category = "nanoql", IsThreadSafe = true, Description = "A batch of draws' weights, a column")]
    public static object DrawsWeights([ExcelArgument(Name = "draws", Description = "The draws (a handle)")] object draws)
        => X.RunVal("nqlxlDrawsWeights", [draws], static a => X.Obj<XlDraws>(a[0], "draws").Weights);

    /// <summary>The generator's snapshot after a batch of draws, from which the next batch draws.</summary>
    /// <param name="draws">The draws' handle.</param>
    /// <returns>The next snapshot's handle.</returns>
    [ExcelFunction(Name = "nqlxlDrawsNext", Category = "nanoql", IsThreadSafe = true, Description = "The generator's snapshot after a batch of draws, from which the next batch draws")]
    public static object DrawsNext([ExcelArgument(Name = "draws", Description = "The draws (a handle)")] object draws)
        => X.Run("nqlxlDrawsNext", [draws], static a => X.Obj<XlDraws>(a[0], "draws").Next);
}
