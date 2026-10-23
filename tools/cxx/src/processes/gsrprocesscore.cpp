// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/processes/gsrprocesscore.hpp, .cpp: detail::GsrProcessCore, which the wheel doesn't bind, built from its times,
// volatilities, reversions and forward measure time, one member per call:
//     GsrProcessCore(times, vols, reversions, T, member, x, y, z)
// with the member expectation_x0dep_part (w, xw, dt), expectation_rn_part (w, dt), expectation_tf_part (w, dt),
// variance (w, dt), y (t), G (t, w), sigma (t) or reversion (t), its arguments from x, y and z in that order. Each call
// builds a fresh core, so no cached value from another call is read.

#include "convert.hpp"

#include <ql/math/array.hpp>
#include <ql/processes/gsrprocesscore.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        Array makeArray(const std::vector<Real>& x) { return Array(x.begin(), x.end()); }

        const Register unit({
            {"GsrProcessCore",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 std::vector<Real> times = reals(a[0]), vols = reals(a[1]), reversions = reals(a[2]);
                 Real T = real(a[3]);
                 std::string member = text(a[4]);
                 Real x = real(a[5]), y = real(a[6]), z = real(a[7]);
                 return [=]() -> json::value {
                     detail::GsrProcessCore core(makeArray(times), makeArray(vols), makeArray(reversions), T);
                     if (member == "expectation_x0dep_part")
                         return json::value(core.expectation_x0dep_part(x, y, z));
                     if (member == "expectation_rn_part")
                         return json::value(core.expectation_rn_part(x, y));
                     if (member == "expectation_tf_part")
                         return json::value(core.expectation_tf_part(x, y));
                     if (member == "variance")
                         return json::value(core.variance(x, y));
                     if (member == "y")
                         return json::value(core.y(x));
                     if (member == "G")
                         return json::value(core.G(x, y));
                     if (member == "sigma")
                         return json::value(core.sigma(x));
                     if (member == "reversion")
                         return json::value(core.reversion(x));
                     throw InvalidParams("unknown GsrProcessCore member " + member);
                 };
             }},
        });

    }

}
