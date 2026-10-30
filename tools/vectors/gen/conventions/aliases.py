# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
# The code below quotes licence tags as data; REUSE reads only the two lines above.
# REUSE-IgnoreStart
"""FpML's codes as aliases of nanoql's names, written from the curated table beside this generator,
fpml-aliases.toml: each alias's code, checked against its scheme's code list there, and the nanoql name it stands
for, in the table's order. The file carries FpML's licence beside nanoql's, the codes being FpML's and the mapping
nanoql's own work. ConventionSet.QuantLib reads the file; Vectors/Conventions/ConventionPresetTests.cs checks each alias
against its target and its code against the scheme's list."""
import os
import sys
import tomllib

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "conventions.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import conventions as c  # noqa: E402

OUTPUT = "src/NanoQL.Extensions/Conventions/Presets/aliases.xml"
TABLE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fpml-aliases.toml")
HEADER = [
    "SPDX-FileCopyrightText: 2026 The nanoql Authors",
    "SPDX-FileCopyrightText: International Swaps and Derivatives Association, Inc.",
    "SPDX-License-Identifier: Apache-2.0 AND LicenseRef-FpML-Public-License-3.0",
    "",
    "FpML's codes, under the FpML Public License (LICENSES/LicenseRef-FpML-Public-License-3.0.txt), each an alias",
    "of the nanoql name it maps to exactly; the mapping is nanoql's own work.",
]


def records():
    with open(TABLE, "rb") as f:
        table = tomllib.load(f)
    out = []
    for alias in table["alias"]:
        scheme = table["schemes"][alias["scheme"]]
        assert alias["code"] in scheme["codes"], alias
        out.append(c.record("Alias", alias["code"], base=alias["name"]))
    return out


if __name__ == "__main__":
    c.write(OUTPUT, records(), HEADER)
# REUSE-IgnoreEnd
