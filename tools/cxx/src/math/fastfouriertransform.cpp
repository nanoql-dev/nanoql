// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/fastfouriertransform.hpp, which the wheel doesn't expose. "FastFourierTransform.min_order" takes (n).
// "FastFourierTransform.output_size" takes (order). "FastFourierTransform.transform" takes (order, inputs, inverse):
// the transform of complex inputs [re, im] into an output of output_size() zeros, or the inverse transform.
// "FastFourierTransform.transformReal" takes (order, inputs, inverse), the same on real inputs.

#include "complexvalue.hpp"
#include "convert.hpp"

#include <ql/math/fastfouriertransform.hpp>

#include <complex>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        json::value complexes(const std::vector<std::complex<Real>>& v) {
            json::array out;
            for (const auto& z : v)
                out.push_back(complexResult(z));
            return out;
        }

        Size order(const json::value& v) {
            Integer o = integer(v);
            if (o < 0 || o > 20)
                throw InvalidParams("expected an order in [0, 20]");
            return static_cast<Size>(o);
        }

        const Register unit({
            {"FastFourierTransform.min_order",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Integer n = integer(a[0]);
                 if (n < 1)
                     throw InvalidParams("expected a positive size");
                 return [=] { return json::value(static_cast<Integer>(FastFourierTransform::min_order(static_cast<Size>(n)))); };
             }},
            {"FastFourierTransform.output_size",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Size o = order(a[0]);
                 return [=] { return json::value(static_cast<Integer>(FastFourierTransform(o).output_size())); };
             }},
            {"FastFourierTransform.transform",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Size o = order(a[0]);
                 std::vector<std::complex<Real>> in = complexValues(a[1]);
                 bool inverse = boolean(a[2]);
                 if (in.size() > (static_cast<Size>(1) << o))
                     throw InvalidParams("more inputs than the transform's points");
                 return [=] {
                     FastFourierTransform fft(o);
                     std::vector<std::complex<Real>> out(fft.output_size());
                     if (inverse)
                         fft.inverse_transform(in.begin(), in.end(), out.begin());
                     else
                         fft.transform(in.begin(), in.end(), out.begin());
                     return complexes(out);
                 };
             }},
            {"FastFourierTransform.transformReal",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Size o = order(a[0]);
                 std::vector<Real> in = reals(a[1]);
                 bool inverse = boolean(a[2]);
                 if (in.size() > (static_cast<Size>(1) << o))
                     throw InvalidParams("more inputs than the transform's points");
                 return [=] {
                     FastFourierTransform fft(o);
                     std::vector<std::complex<Real>> out(fft.output_size());
                     if (inverse)
                         fft.inverse_transform(in.begin(), in.end(), out.begin());
                     else
                         fft.transform(in.begin(), in.end(), out.begin());
                     return complexes(out);
                 };
             }},
        });

    }

}
