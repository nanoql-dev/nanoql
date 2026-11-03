<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# The stateless design

Every price nanoql computes is a function of its inputs: the instrument, the evaluation context and the market data
the engine takes. Nothing outside them reaches the result, and no call changes what a later one returns. So the same
inputs give the same result, bit for bit, whenever and on whichever thread they are priced.

Keeping a book's prices current as its market data moves is the application's part: it knows which inputs changed and
what depends on them, and calls the library again. This page shows the patterns it builds that layer from. Each
sample prices a ten-year Euribor swap on the curve of [the curves guide](rates/curves.md).

## Rebuild on change

When a quote moves, build the objects that depend on it again from the new inputs, and price on them. The old curve
and the prices made on it stand as they were.

[!code-csharp[](examples/Stateless/StatelessPatterns.cs#rebuild)]

[!INCLUDE [the swap before and after the move](_generated/stateless-rebuild-output.md)]

## Memoize by inputs

The same inputs build the same curve, so an application can keep what it has built under a key made of its inputs and
share it: an immutable object is safe to read from any number of threads.

[!code-csharp[](examples/Stateless/StatelessPatterns.cs#memoize)]

## Price scenarios in parallel

Scenarios are independent calls on immutable inputs, so they run side by side with no locks:

[!code-csharp[](examples/Stateless/StatelessPatterns.cs#parallel)]

[!INCLUDE [the swap under each shift](_generated/stateless-scenarios-output.md)]

Run again, in any order or on any number of threads, the scenarios give the same bits.

## The one global

`ConventionProvider.Default`, the conventions the library's presets and builders take when nothing else is given, is
the one global value ([conventions.md](conventions.md)). It is read when an object is built, never while it prices:
an object keeps the conventions it was built with, and a change of the default reaches the objects built after it.
Set it once at start-up, or open a scope with `ConventionProvider.Use` for one flow's builds; a scope flows with
asynchronous calls, as a culture does.
