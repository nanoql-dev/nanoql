# Contributing to nanoql

## The faithfulness rule

The core mirrors QuantLib exactly. It changes only through ports of upstream QuantLib changes, or through fixes proven
against QuantLib. Anything QuantLib lacks (a new convention, model or feature) goes into the extensions layer,
`src/NanoQL.Extensions/`, with its own tests.

## Porting a QuantLib class

- One nanoql type per QuantLib class, with the same name where possible.
- Each ported file records the upstream path and the QuantLib release tag it came from.
- No observers, lazy objects or global state: inputs are immutable records; models and pricers are static functions.
- Results must match QuantLib's test vectors within the tolerance in docs/porting-playbook.md; the test suite
  blocks the merge otherwise.

## Code that may not be ported

Only code under an open-source license compatible with Apache 2.0 may be ported. Code from source-available
libraries, whose licenses don't permit it, may not; their methods may still be implemented from public sources such as
papers and documentation.

## How a change gets in

1. Fork the repository, and make your change on a branch of your fork.
2. Commit with `git commit -s` (below), and open a pull request against `main`.
3. CI runs the gates on the pull request, and checks that every commit is signed off. Windows is the reference
   system; results on Linux and macOS are reported, not yet required.
4. A maintainer reviews the change against the faithfulness rule and QuantLib's vectors, and runs any check CI can't.
5. The maintainer squash-merges it, editing the message to the repository's style and keeping your sign-off.

## Running the gates yourself

The gates are in docs/porting-playbook.md §8; each passes only with its denominator full. From the repository root:

- **Build:** `dotnet build NanoQL.slnx -c Release`: 0 errors and 0 warnings.
- **Tests and vectors:** `dotnet test --solution NanoQL.slnx -c Release`, then `python -I tools/vectors/agree.py`.
  Neither needs QuantLib: the vectors are committed under `tests/vectors/`.
- **Licensing:** `reuse lint`, then `python -I tools/check-upstream.py <QuantLib v1.44 worktree> --boost <Boost 1.90.0
  root>`, with the tools of `tools/vectors/requirements.txt`.
- **Reproducibility,** when the change adds or changes a vector generator under `tools/vectors/gen/`:
  `python -I tools/vectors/reproduce.py <generator>...` reruns them and compares their output byte for byte. They
  need QuantLib-Python 1.44, from the same requirements file; harness generators also need the C++ harness that
  `tools/cxx/build.py` builds.
- **Coverage,** when the change ports code: the coverage run and `tools/check-coverage.py`, as the playbook's §8 and
  §9.5 give them.

## Issues and questions

- **A bug:** open an issue with the inputs that reproduce it, and QuantLib's result for them if you have it.
- **A missing convention or feature:** use its issue form; a convention helps most with its published source.
- **A question or an idea:** GitHub Discussions.
- **A security vulnerability:** privately, as [SECURITY.md](SECURITY.md) says, never in a public issue.

Everyone taking part follows the [code of conduct](CODE_OF_CONDUCT.md).

## Sign your commits off (DCO)

nanoql uses the [Developer Certificate of Origin](https://developercertificate.org/) instead of a contributor license
agreement. Signing off a commit certifies that you wrote the change, or otherwise have the right to submit it under
the project's license:

    git commit -s

This adds a `Signed-off-by: Your Name <you@example.com>` line matching your git identity.
