// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/utilities/dataformatters.hpp

#include "convert.hpp"

#include <ql/utilities/dataformatters.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        // io::ordinal's Size: a non-negative integer.
        Size size(const json::value& v) {
            if (v.is_uint64())
                return static_cast<Size>(v.get_uint64());
            if (!v.is_int64() || v.get_int64() < 0)
                throw InvalidParams("expected a Size, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        // Each method returns the text its manipulator streams.
        const Register unit({
            {"io::ordinal",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Size n = size(a[0]);
                 return [=] { return json::value(streamed(io::ordinal(n))); };
             }},
            {"io::power_of_two<Integer>",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Integer n = integer(a[0]);
                 return [=] { return json::value(streamed(io::power_of_two<Integer>(n))); };
             }},
            {"io::power_of_two<long long>",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 long long n = int64(a[0]);
                 return [=] { return json::value(streamed(io::power_of_two<long long>(n))); };
             }},
            {"io::checknull<Real>",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Real x = real(a[0]);
                 return [=] { return json::value(streamed(io::checknull<Real>(x))); };
             }},
            {"io::checknull<Integer>",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Integer n = integer(a[0]);
                 return [=] { return json::value(streamed(io::checknull<Integer>(n))); };
             }},
            {"io::sequence<Real>",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 std::vector<Real> xs = reals(a[0]);
                 return [=] { return json::value(streamed(io::sequence(xs))); };
             }},
            {"io::sequence<Integer>",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 std::vector<Integer> ns = integers(a[0]);
                 return [=] { return json::value(streamed(io::sequence(ns))); };
             }},
            {"io::percent",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Real x = real(a[0]);
                 return [=] { return json::value(streamed(io::percent(x))); };
             }},
            {"io::rate",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Rate r = real(a[0]);
                 return [=] { return json::value(streamed(io::rate(r))); };
             }},
            {"io::volatility",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Volatility x = real(a[0]);
                 return [=] { return json::value(streamed(io::volatility(x))); };
             }},
        });

    }

}
