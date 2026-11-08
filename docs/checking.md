<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# How nanoql is checked

nanoql's results are checked against QuantLib 1.44's own on every change.
[The validation report](https://nanoql.org/validation/) gives the figures; this page explains how they are measured.

## The references

- **QuantLib's test suite.** For each class nanoql ports, the cases from QuantLib's `test-suite/` are ported with
  their expected values and their tolerances.
- **Vectors from QuantLib's Python package.** One generator per QuantLib source file, a Python script under
  `tools/vectors/gen/`, builds cases through QuantLib-Python 1.44 with a fixed seed. It writes each case's inputs and
  QuantLib's outputs to a JSON file under `tests/vectors/`, and nanoql's tests read the same inputs and compare every
  output. Each case sets its evaluation date and QuantLib's global settings explicitly, so a vector doesn't depend on
  the day it was made. A case where QuantLib refuses its inputs is a vector too, and nanoql must refuse them as well,
  unless it fixes the refusal as a departure (below).
- **Vectors from QuantLib built from source.** Some classes and members aren't exposed by the Python package. For
  these, a C++ harness answers the generators' calls: QuantLib 1.44 built from its own sources, with the compiler and
  build options the Python package uses. The harness carries a digest of its own sources and refuses to run against
  any other QuantLib, Boost or compiler. Some of its vectors have twins from the Python package, which must agree, so
  the harness is itself checked.
- **QLNet's package,** for the parts of QLNet that nanoql's extensions port: vectors from nuget.org's QLNet 1.13.2.

## The tolerance

An output passes when |a − b| ≤ 1e-10 × max(|a|, |b|, scale):

- the scale is 1 for a rate, a discount factor or a year fraction;
- for an amount, the scale is its notional, so an NPV on $10 million agrees within $0.001;
- for a basis-point value, the scale is the notional × 1e-4.

Integers, dates, booleans and names compare exactly. An output may have a looser tolerance only with a recorded reason
that names the measured cause; the validation report counts them.

nanoql's outputs needn't match QuantLib's bit for bit. .NET's mathematical functions and the C runtime's, `pow` and
`hypot` among them, can differ in the last bit, and a long calculation carries the difference along. Where a
calculation magnifies it (a second difference, a gradient by differences, an ill-conditioned fit), the output's
bound is set at least ten times above the largest difference measured on Windows, Linux and macOS, with its reason,
where Windows x64 needs one; a result that moves past its bound on another system alone is listed among that system's
known differences (below).

A calibration's parameters can move more than its inputs do. So a calibrated output is recorded only where QuantLib's
own value holds, within 1e-12, when every input moves one unit in its last place, up and then down. The outputs left
out are checked by QuantLib's ported tests, at QuantLib's tolerances.

## Every public type's code is run

A coverage check runs the tests whose expected values come from QuantLib, or from QLNet for its additions. Every public
type with executable code must have lines those tests run. The report lists every exception by name:

- the types with no executable code (enums, interfaces, results records);
- the types whose only code is the equality nanoql adds, each with its own test of it;
- the few checked by their own tests, each with its reason.

It gives line coverage per module, with no threshold.

## Every departure is a test

When porting finds a defect in QuantLib, nanoql usually fixes it, so the result departs from QuantLib's. Each such
departure has its own test class under `tests/NanoQL.Tests/Divergences/`. The class states QuantLib's value, as
measured, nanoql's value and the reason. [The divergences page](divergences.md) is generated from those classes, one
entry each, and a check fails if the page and the tests disagree.

## Reproducible and traceable

- **The vectors are committed,** so the tests need neither Python nor QuantLib. Rerun in their pinned environment
  (Python 3.12 on Windows x64, every package pinned by its hash), the generators reproduce the committed files byte for
  byte. A change to a generator is checked by rerunning it.
- **Each ported file names its source:** the file it was ported from, QuantLib's, Boost's or QLNet's, and the release.
  A check confirms that the path exists at that release's tag, and that the file carries the source's copyright lines
  word for word.

## The other languages

The Python, C and C++ layers call one C interface over nanoql compiled to native code. A recorder runs the vector
suite in C# and records each call the tests make into nanoql as a call to the matching C function, with its arguments
and its result. Each language's replayer then makes the same calls through its own layer and compares every result
with C#'s, bit for bit, on each of the five platforms the native library ships for. Each layer also has its own
conformance suite. The Excel add-in's suite runs inside Excel before each release.

## Every change and every release

- **Every change:** the build, the tests and the vectors run on Windows, Linux and macOS. Windows x64 must pass. On
  Linux and macOS the tests that fail from the platform's floating point (a result's last bits, which a calculation
  can magnify, or on Arm64 a NaN's sign) are listed for each system
  ([`tests/known-differences/`](https://github.com/nanoql-dev/nanoql/tree/main/tests/known-differences)), and a
  failure not on its system's list turns that check red.
- **Every release:** the release is built from one commit, and a failure on Windows x64 stops it. The tests also
  run on Windows Arm64 and Linux Arm64, and each language's replay on all five platforms. The release notes count the
  test differences on each platform beside Windows x64. The SOFR example's trades ([the example](sofr-example.md)) are
  priced again by an independent pricer, each within $0.01 on $10 million. Once the packages are published, each is
  installed from its registry and checked.
- **Every week:** each language's replay runs on all five platforms, and
  [the Python notebook](https://colab.research.google.com/github/nanoql-dev/nanoql/blob/main/docs/notebooks/first-steps.ipynb)
  runs against the latest release on PyPI.

## Running the checks yourself

`dotnet test --solution NanoQL.slnx -c Release` runs the tests and the vectors, and
`python -I tools/vectors/agree.py` checks the twins. [CONTRIBUTING.md](../CONTRIBUTING.md#running-the-gates-yourself)
lists the rest: the licence and source checks, and rerunning a generator.
