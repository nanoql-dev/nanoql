// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/matrixutilities/sparsematrix.hpp: SparseMatrix's +=, uBLAS's compressed_matrix, which the wheel doesn't
// expose. "SparseMatrix::plusAssign" takes (size1, size2, a, b), each matrix's elements a list of [row, column, value]
// written in order through the element reference (an assignment stores the element, zero included), and returns the
// first matrix after a += b as its storage: {"filled1", "index1" (the first filled1 entries of index1_data), "index2"
// and "values" (the stored elements' columns and values, filled2 of them)}.

#include "convert.hpp"

#include <ql/math/matrixutilities/sparsematrix.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct Element {
            Size row, column;
            Real value;
        };

        std::vector<Element> elements(const json::value& v, Integer size1, Integer size2) {
            if (!v.is_array())
                throw InvalidParams("expected [row, column, value] elements, got " + json::serialize(v));
            std::vector<Element> out;
            for (const json::value& e : v.get_array()) {
                if (!e.is_array() || e.get_array().size() != 3)
                    throw InvalidParams("expected a [row, column, value] element, got " + json::serialize(e));
                Integer i = integer(e.get_array()[0]), j = integer(e.get_array()[1]);
                if (i < 0 || j < 0 || i >= size1 || j >= size2)
                    throw InvalidParams("an element outside the matrix");
                out.push_back({static_cast<Size>(i), static_cast<Size>(j), real(e.get_array()[2])});
            }
            return out;
        }

        const Register unit({
            {"SparseMatrix::plusAssign",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Integer size1 = integer(a[0]), size2 = integer(a[1]);
                 if (size1 < 0 || size2 < 0)
                     throw InvalidParams("negative sizes");
                 std::vector<Element> x = elements(a[2], size1, size2), y = elements(a[3], size1, size2);
                 return [=] {
                     SparseMatrix m(size1, size2), n(size1, size2);
                     for (const Element& e : x)
                         m(e.row, e.column) = e.value;
                     for (const Element& e : y)
                         n(e.row, e.column) = e.value;
                     m += n;
                     json::array index1, index2, values;
                     for (Size i = 0; i < m.filled1(); ++i)
                         index1.emplace_back(static_cast<std::int64_t>(m.index1_data()[i]));
                     for (Size k = 0; k < m.filled2(); ++k) {
                         index2.emplace_back(static_cast<std::int64_t>(m.index2_data()[k]));
                         values.emplace_back(m.value_data()[k]);
                     }
                     return json::value(json::object{{"filled1", static_cast<std::int64_t>(m.filled1())},
                                                     {"index1", index1},
                                                     {"index2", index2},
                                                     {"values", values}});
                 };
             }},
        });

    }

}
