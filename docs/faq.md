<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# FAQ

## Which platforms does nanoql run on?

- **The .NET library:** wherever .NET 10 runs, on Windows, Linux and macOS. Windows x64 is the validated platform. On
  Windows Arm64, Linux and macOS some results differ in their last bits, from the C runtime's maths functions or the
  processor, and a calculation that magnifies them moves further: dozens of tests fall outside their tolerances there,
  a calibration by about 1e-7 relative. Each release's notes count them
  ([issue #21](https://github.com/nanoql-dev/nanoql/issues/21)).
- **Python, C and C++:** Windows x64 and Arm64, Linux x64 and Arm64 (glibc 2.28 or later), and macOS on Apple silicon
  (14 or later). Intel Macs have none, though the .NET library runs there.
- **Excel:** 64-bit Excel for Microsoft 365 on Windows x64, with the .NET 10 Desktop Runtime ([the add-in](excel.md)).

## Why does `pip install nanoql` find no version?

pip installs nanoql from a wheel, and the wheels are for Windows x64 and Arm64, Linux x64 and Arm64 (glibc 2.28 or
later), and macOS on Apple silicon (14 or later). On an Intel Mac, with an Intel Python on Apple silicon, on macOS older
than 14, or on Linux with musl (Alpine) or with a glibc older than 2.28, pip finds none and reports "No matching
distribution found for nanoql". With Python older than 3.11 on a platform that has wheels, it first names the
versions it skipped, each needing 3.11 or later.

Where there is no wheel, you have two options:

- run [the notebook](https://colab.research.google.com/github/nanoql-dev/nanoql/blob/main/docs/notebooks/first-steps.ipynb)
  in Google Colab, in the browser;
- use the .NET library, which runs wherever .NET 10 does.

## Does the Python package need .NET?

No. The wheel holds nanoql compiled to native code, behind its C interface, so `pip install nanoql` is all it takes.
[The bindings guide](bindings.md) gives the Python layer's rules.

## Which .NET versions does it support?

nanoql needs .NET 10 or later. It uses static abstract interface members, which .NET Framework can't run, so it has no
.NET Framework or .NET Standard build. A project on .NET Framework moves to .NET 10 first
([from QLNet](migration/from-qlnet.md)).

In a project on an older .NET, `dotnet add package NanoQL` stops with NuGet's NU1202, "Package NanoQL ... is not
compatible with net8.0 ... supports: net10.0". Set the project's `<TargetFramework>` to `net10.0`, with the .NET 10
SDK installed, and add it again.

## How does nanoql relate to QuantLib?

nanoql is derived from QuantLib 1.44. It has QuantLib's classes, names and mathematics, and its results are checked
against QuantLib's ([how nanoql is checked](checking.md)). Its design differs: nothing observes anything else, there
are no handles or lazy objects, and the evaluation date is an argument, not a global setting
([concepts](concepts.md)). Where nanoql's results depart from QuantLib's, usually where a QuantLib defect is fixed, the
departure is documented with its own test ([divergences](divergences.md)).

nanoql is an independent project: the QuantLib project isn't affiliated with it and doesn't endorse it. Guides for
moving code over: [from QuantLib's C++](migration/from-quantlib-cpp.md),
[from QuantLib-Python](migration/from-quantlib-python.md) and [from QLNet](migration/from-qlnet.md).

## Which licence is it under?

nanoql is under the Apache License 2.0. It is derived from QuantLib, under QuantLib's modified BSD licence, and ports
parts of Boost's maths, under the Boost Software License 1.0. Its extensions port parts of QLNet, under the BSD
3-Clause licence, and its conventions files carry codes from FpML's coding schemes, under the FpML Public License 3.0.
[THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md) holds the notices of nanoql's sources, among them those of the
third-party code that QuantLib itself carries. The wheels and the C archives add .NET's, whose runtime is compiled
into the library, and the add-in's zip adds Excel-DNA's and .NET's. [NOTICE](../NOTICE) is nanoql's own.

## Is the API stable?

A version is major and minor, with a third part only for a fix (2.0.1). A minor release adds to the API, and the
stable API is removed or changed only where it was deprecated at least two releases and a year before.

The experimental API is the exception: the types ported from QuantLib's experimental folder, and the types and
members nanoql adds as experimental. It may change in any release, its released names are never reused, and the
compiler flags its use ([experimental code](concepts.md#experimental-code)). Each release's changes are in
[CHANGELOG.md](../CHANGELOG.md).

## Why does Excel disable the add-in?

The add-ins are unsigned. An Excel set to require add-ins signed by a trusted publisher, which group policy often
sets, disables an unsigned `.xll` without asking. A firm that enforces it signs the `.xll` with its own code-signing
certificate. Excel also blocks an add-in downloaded from the internet, signed or not, until it is unblocked
([installing the add-in](excel.md#installing)).

## Where do I ask a question or report a bug?

- **Questions and ideas:** [GitHub Discussions](https://github.com/nanoql-dev/nanoql/discussions).
- **Bugs:** an [issue](https://github.com/nanoql-dev/nanoql/issues/new/choose). The bug template asks for the result
  you got and the one you expected, the smallest code that shows it, and QuantLib's result if you have it. A wrong
  numerical result is a bug.
- **Security:** privately, as [SECURITY.md](../SECURITY.md) says, never in a public issue.

Fixes go into the latest 2.x release.

## Can I contribute?

Yes. [CONTRIBUTING.md](../CONTRIBUTING.md) gives the faithfulness rule, how a change gets in, and the checks you can
run yourself. Commits are signed off under the Developer Certificate of Origin.
