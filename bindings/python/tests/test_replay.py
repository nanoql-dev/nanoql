# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The Python replayer's own rule (docs/bindings.md, "The vector suite through each binding"): a new object's class is
compared without a file-local class's compiler prefix, whose hash is of its source file's path."""

import unittest

from replay import type_key


class TypeKey(unittest.TestCase):
    def test_file_local_classes(self):
        here = "NanoQL.TermStructures.<BlackVolTermStructure>F8C1D0E7A3B5__BlackVolSmileSectionAdapter"
        elsewhere = "NanoQL.TermStructures.<BlackVolTermStructure>F04F2A9B6C7D8__BlackVolSmileSectionAdapter"
        self.assertEqual(type_key(here), type_key(elsewhere))
        self.assertEqual(type_key(here), "NanoQL.TermStructures.BlackVolSmileSectionAdapter")
        self.assertEqual(type_key("NanoQL.TermStructures.FlatForward"), "NanoQL.TermStructures.FlatForward")
        self.assertNotEqual(type_key("NanoQL.A"), type_key("NanoQL.B"))


if __name__ == "__main__":
    unittest.main()
