# nanoql

nanoql is a stateless C# port of [QuantLib](https://www.quantlib.org/). The maths is ported faithfully and checked
against QuantLib itself; the structure is not. There are no observers, lazy objects or global evaluation date: every
model and pricer is a plain function of its inputs.

> **Status:** pre-release. Nothing is usable yet.

## Principles

- **Faithful in the maths.** Every result matches QuantLib to 1e-10, using test vectors that QuantLib generates in CI.
- **Stateless in structure.** Inputs are immutable records; models, calibration and pricers are static functions.
  The same inputs always give the same outputs, and calls are safe to run in parallel.
- **Traceable to upstream.** One nanoql type per QuantLib class, and each file records the QuantLib path and commit
  it was ported from.
- **Python without .NET** (planned). Wheels generated over Native AOT: `pip install nanoql`, no .NET runtime needed.

## License

Apache 2.0, see [LICENSE](LICENSE). nanoql is derived from QuantLib; QuantLib's copyright notices and license are kept
in [licenses/QuantLib-LICENSE.txt](licenses/QuantLib-LICENSE.txt), see [NOTICE](NOTICE).

Contributions are welcome under the [Developer Certificate of Origin](CONTRIBUTING.md#sign-your-commits-off-dco).

---

Sponsored and maintained by Masseef.
