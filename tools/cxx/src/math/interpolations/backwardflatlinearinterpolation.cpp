// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/interpolations/backwardflatlinearinterpolation.hpp: BackwardflatLinearInterpolation and its factory
// BackwardflatLinear, which the wheel doesn't expose. An interpolation is the spec {"x", "y", "z",
// "enableExtrapolation", "factory"}: z is the list of the matrix's rows, all of one size, a row per y value, at least as
// many rows as y values and columns as x values (QuantLib reads past a smaller matrix, so the harness refuses one);
// enableExtrapolation, whether enableExtrapolation() is called on the interpolation built; factory, whether it is
// built by BackwardflatLinear().interpolate rather than by its constructor. QuantLib's 2-D interpolations keep
// iterators into the grid and a reference to the matrix, so the call builds them over the spec's own vectors and a
// matrix that outlives them.
//
//     BackwardflatLinearInterpolation::operator()   (spec, x, y, allowExtrapolation): the value, with QuantLib's
//                                                   allowExtrapolation flag

#include "convert.hpp"

#include <ql/math/interpolations/backwardflatlinearinterpolation.hpp>
#include <ql/math/matrix.hpp>
#include <ql/shared_ptr.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct BackwardflatLinearSpec {
            std::vector<Real> x, y;
            Size rows = 0, columns = 0;
            std::vector<Real> z;  // row by row
            bool enableExtrapolation = false;
            bool factory = false;
        };

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("a backward-flat-linear interpolation spec needs ") + name);
            return *v;
        }

        BackwardflatLinearSpec interpolation(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a backward-flat-linear interpolation spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            for (const auto& kv : o) {
                if (kv.key() != "x" && kv.key() != "y" && kv.key() != "z" && kv.key() != "enableExtrapolation" &&
                    kv.key() != "factory")
                    throw InvalidParams("a backward-flat-linear interpolation takes no key " + std::string(kv.key()));
            }
            BackwardflatLinearSpec s;
            s.x = reals(key(o, "x"));
            s.y = reals(key(o, "y"));
            const json::value& z = key(o, "z");
            if (!z.is_array())
                throw InvalidParams("a backward-flat-linear interpolation needs z as a list of rows");
            for (const json::value& row : z.get_array()) {
                std::vector<Real> r = reals(row);
                if (s.rows == 0)
                    s.columns = r.size();
                else if (r.size() != s.columns)
                    throw InvalidParams("a backward-flat-linear interpolation needs z's rows of one size");
                s.z.insert(s.z.end(), r.begin(), r.end());
                ++s.rows;
            }
            // QuantLib reads zData_[j+1][i+1] for the grid's last cell, whatever the matrix's size.
            if (s.rows < s.y.size() || s.columns < s.x.size())
                throw InvalidParams("a backward-flat-linear interpolation's matrix is smaller than its grid");
            s.enableExtrapolation = boolean(key(o, "enableExtrapolation"));
            s.factory = boolean(key(o, "factory"));
            return s;
        }

        const Register unit({
            // (spec, x, y, allowExtrapolation)
            {"BackwardflatLinearInterpolation::operator()",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 BackwardflatLinearSpec s = interpolation(a[0]);
                 Real x = real(a[1]);
                 Real y = real(a[2]);
                 bool allow = boolean(a[3]);
                 return [=] {
                     Matrix z(s.rows, s.columns, s.z.begin(), s.z.end());
                     Interpolation2D f =
                         s.factory ? BackwardflatLinear().interpolate(s.x.begin(), s.x.end(), s.y.begin(), s.y.end(), z)
                                   : BackwardflatLinearInterpolation(s.x.begin(), s.x.end(), s.y.begin(), s.y.end(),
                                                                     z);
                     if (s.enableExtrapolation)
                         f.enableExtrapolation();
                     return json::value(f(x, y, allow));
                 };
             }},
        });

    }

}
