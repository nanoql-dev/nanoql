// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/distributions/normaldistribution.hpp: the members the wheel doesn't expose, the two Maddock classes (over
// Boost.Math's normal cdf and quantile) and InverseCumulativeNormal::standard_value. A distribution argument is the
// spec {"average", "sigma"}, built inside the call, so that a constructor's refusal is QuantLib's.

#include "convert.hpp"

#include <ql/math/distributions/normaldistribution.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        struct NormalSpec {
            Real average, sigma;
        };

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("a normal distribution spec needs ") + name);
            return *v;
        }

        NormalSpec normal(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a normal distribution spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            for (const auto& kv : o) {
                if (kv.key() != "average" && kv.key() != "sigma")
                    throw InvalidParams("a normal distribution spec takes no key " + std::string(kv.key()));
            }
            return {real(key(o, "average")), real(key(o, "sigma"))};
        }

        const Register unit({
            {"MaddockCumulativeNormal::operator()",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 NormalSpec s = normal(a[0]);
                 Real x = real(a[1]);
                 return [=] { return json::value(MaddockCumulativeNormal(s.average, s.sigma)(x)); };
             }},
            {"MaddockInverseCumulativeNormal::operator()",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 NormalSpec s = normal(a[0]);
                 Real x = real(a[1]);
                 return [=] { return json::value(MaddockInverseCumulativeNormal(s.average, s.sigma)(x)); };
             }},
            {"InverseCumulativeNormal::standard_value",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Real x = real(a[0]);
                 return [=] { return json::value(InverseCumulativeNormal::standard_value(x)); };
             }},
        });

    }

}
