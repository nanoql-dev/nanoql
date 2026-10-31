# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""QLNet 1.13.2's MBSFixedRateBond (src/QLNet/Instruments/Bonds/MBSFixedRateBond.cs): writes
tests/vectors/qlnet/Instruments/Bonds/MBSFixedRateBond.json through QLNet's vector generator's mbsfixedratebond
subcommand (tools/vectors/qlnet/Instruments/Bonds/MBSFixedRateBondVectors.cs)."""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "..", "qlnet"))
import shim  # noqa: E402

if __name__ == "__main__":
    shim.main("mbsfixedratebond")
