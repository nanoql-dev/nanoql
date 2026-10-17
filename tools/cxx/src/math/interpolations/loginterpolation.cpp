// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/interpolations/loginterpolation.hpp: the LogLinear, LogCubic, MonotonicLogCubic and KrugerLog factories'
// interpolate and traits. A factory's interpolation is read back through its values at the given points,
// extrapolating where needed. LogCubic::interpolate takes the spec {"type": "LogCubic"} with derivativeApprox alone or
// all six parameters, or one of the presets {"type": "MonotonicLogCubic"} and {"type": "KrugerLog"}, copied into the
// LogCubic it derives from, as a call taking a LogCubic receives a preset.

#include "math/interpolationspec.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        // A stateless factory's interpolate: (x, y, points).
        template <class F>
        Call interpolate(const Args& a) {
            arity(a, 3);
            std::vector<Real> x = reals(a[0]), y = reals(a[1]), points = reals(a[2]);
            checkSizes(x, y);
            return [=] { return valuesAt(F().interpolate(x.begin(), x.end(), y.begin()), points); };
        }

        template <class F>
        Call global(const Args& a) {
            arity(a, 0);
            return [] { return json::value(F::global); };
        }

        template <class F>
        Call requiredPoints(const Args& a) {
            arity(a, 0);
            return [] { return json::value(static_cast<std::int64_t>(F::requiredPoints)); };
        }

        const Register unit({
            {"LogLinear::interpolate", interpolate<LogLinear>},
            {"LogLinear::global", global<LogLinear>},
            {"LogLinear::requiredPoints", requiredPoints<LogLinear>},
            // (spec, x, y, points).
            {"LogCubic::interpolate",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 InterpolatorSpec s = interpolatorSpec(a[0], {"LogCubic", "MonotonicLogCubic", "KrugerLog"});
                 std::vector<Real> x = reals(a[1]), y = reals(a[2]), points = reals(a[3]);
                 checkSizes(x, y);
                 return [=] {
                     const LogCubic factory = makeLogCubicInterpolator(s);
                     return valuesAt(factory.interpolate(x.begin(), x.end(), y.begin()), points);
                 };
             }},
            {"LogCubic::global", global<LogCubic>},
            {"LogCubic::requiredPoints", requiredPoints<LogCubic>},
            {"MonotonicLogCubic::interpolate", interpolate<MonotonicLogCubic>},
            {"MonotonicLogCubic::global", global<MonotonicLogCubic>},
            {"MonotonicLogCubic::requiredPoints", requiredPoints<MonotonicLogCubic>},
            {"KrugerLog::interpolate", interpolate<KrugerLog>},
            {"KrugerLog::global", global<KrugerLog>},
            {"KrugerLog::requiredPoints", requiredPoints<KrugerLog>},
        });

    }

}
