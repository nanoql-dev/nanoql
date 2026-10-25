// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// A complex value as the vectors write it, [real, imaginary], for the units whose arguments or results are complex.

#ifndef NANOQL_CXX_COMPLEXVALUE_HPP
#define NANOQL_CXX_COMPLEXVALUE_HPP

#include "convert.hpp"

#include <complex>
#include <vector>

namespace harness {

    inline std::complex<QuantLib::Real> complexValue(const json::value& v) {
        std::vector<QuantLib::Real> c = reals(v);
        if (c.size() != 2)
            throw InvalidParams("expected a complex value [real, imaginary], got " + json::serialize(v));
        return {c[0], c[1]};
    }

    inline std::vector<std::complex<QuantLib::Real>> complexValues(const json::value& v) {
        if (!v.is_array())
            throw InvalidParams("expected an array of complex values, got " + json::serialize(v));
        std::vector<std::complex<QuantLib::Real>> out;
        for (const json::value& e : v.get_array())
            out.push_back(complexValue(e));
        return out;
    }

    inline json::value complexResult(const std::complex<QuantLib::Real>& z) {
        return json::value(json::array{z.real(), z.imag()});
    }

}

#endif
