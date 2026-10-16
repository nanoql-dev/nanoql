# Contributing to nanoql

## The faithfulness rule

The core mirrors QuantLib exactly. It changes only through ports of upstream QuantLib changes, or through fixes proven
against QuantLib. Anything QuantLib lacks (a new convention, model or feature) goes into the extensions layer,
`src/NanoQL.Extensions/`, with its own tests.

## Porting a QuantLib class

- One nanoql type per QuantLib class, with the same name where possible.
- Each ported file records the upstream path and the QuantLib commit it came from.
- No observers, lazy objects or global state: inputs are immutable records; models and pricers are static functions.
- Results must match QuantLib's test vectors to 1e-10. CI blocks the merge otherwise.

## Code that may not be ported

Only code under an open-source license compatible with Apache 2.0 may be ported. Code from source-available
libraries, whose licenses don't permit it, may not; their methods may still be implemented from public sources such as
papers and documentation.

## Sign your commits off (DCO)

nanoql uses the [Developer Certificate of Origin](https://developercertificate.org/) instead of a contributor license
agreement. Signing off a commit certifies that you wrote the change, or otherwise have the right to submit it under
the project's license:

    git commit -s

This adds a `Signed-off-by: Your Name <you@example.com>` line matching your git identity.
