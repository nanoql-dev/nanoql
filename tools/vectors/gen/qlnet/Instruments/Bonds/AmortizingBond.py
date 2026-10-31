# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""QLNet 1.13.2's AmortizingBond (src/QLNet/Instruments/Bonds/AmortizingBond.cs): writes
tests/vectors/qlnet/Instruments/Bonds/AmortizingBond.json through QLNet's vector generator's amortizingbond subcommand
(tools/vectors/qlnet/Instruments/Bonds/AmortizingBondVectors.cs)."""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "..", "qlnet"))
import shim  # noqa: E402

if __name__ == "__main__":
    shim.main("amortizingbond")
