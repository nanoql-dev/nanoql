# nanoql for Excel

nanoql's Excel add-in puts the library in worksheet formulas: `=nqlFlatForward(A1, 0.03, "Actual365Fixed")` builds a
curve, and `=nqlYieldTermStructureDiscount(A2, DATE(2030,1,1))` reads it. The documentation is at https://nanoql.org.

This zip holds:

- `nanoql64.xll`, the stable API;
- `nanoql-experimental64.xll`, the stable API and the experimental one: loading it is the consent to the experimental
  API's changes;
- `LICENSE`, `NOTICE` and `THIRD_PARTY_NOTICES.md`, nanoql's license and the notices of the code it carries;
- `examples/`: five workbooks on the stable add-in (a curve, swaps, a bond, an option and SOFR swaps), each on one
  sheet with its inputs in cells and its results named, and `market.xml`, the conventions file `sofr.xlsx` reads from
  its own folder. Unzip the folder before opening them, and recalculate each (Ctrl+Alt+F9) with the add-in loaded.

Both add-ins carry the same names, so a workbook built on the stable one opens unchanged in the experimental one. Load
one of them per Excel process.

## What it needs

- 64-bit Excel for Microsoft 365 on Windows x64, whose dynamic arrays the add-in spills into. Microsoft 365 installs
  the 64-bit Excel unless the 32-bit one was chosen.
- The .NET 10 Desktop Runtime, x64, from https://dotnet.microsoft.com/download/dotnet/10.0. Without it, the add-in says
  so when it loads.

## Installing

1. Unzip the zip to a folder of your own.
2. Unblock the `.xll` you'll load: Excel blocks an add-in downloaded from the internet. In File Explorer, open its
   Properties and tick Unblock on the General tab; or put the folder in one of Excel's trusted locations (File >
   Options > Trust Center > Trust Center Settings > Trusted Locations).
3. In Excel, File > Options > Add-ins, Manage: Excel Add-ins, Go, then Browse to the `.xll`. Excel loads it now and at
   each start. Opening the `.xll` instead loads it for this session only.

## Unsigned add-ins

The add-ins are unsigned. A firm whose Excel enforces "Require Application Add-ins to be signed by Trusted Publisher"
(a Trust Center setting, often set by group policy) disables an unsigned `.xll` without a prompt. There, the firm signs
the `.xll` with its own code-signing certificate, and Excel loads it as its publisher's.

## Other .NET add-ins

Excel holds one .NET runtime per process, and these add-ins run on .NET 10. An add-in on another .NET version, loaded
in the same Excel, fails silently: its functions, or nanoql's, show `#NAME?`.

| Loaded first | Then | Result |
|---|---|---|
| nanoql | an add-in on .NET 8 | the other add-in's functions `#NAME?` |
| an add-in on .NET 8 | nanoql | nanoql's functions `#NAME?` |
| nanoql | an add-in on .NET 10 | both work |
| nanoql | an add-in compiled to native code (Native AOT) | both work |

## Debugging

nanoql's symbols ship apart, in `nanoql-excel-<version>-win-x64-symbols.zip`: `NanoQL.pdb` and `NanoQL.Extensions.pdb`,
with nanoql's source embedded. To step into nanoql from a cell in Visual Studio: give the symbols' folder once (Tools >
Options > Debugging > Symbols), turn on Suppress JIT optimization on module load and turn off Just My Code (Tools >
Options > Debugging > General), and attach to Excel before the add-in loads, or start Excel from Visual Studio with the
add-in.
