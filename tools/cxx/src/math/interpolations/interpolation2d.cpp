// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/interpolations/interpolation2d.hpp: Interpolation2D's members, which the wheel doesn't expose, on a
// BilinearInterpolation and on a FlatExtrapolator2D (ql/math/interpolations/flatextrapolation2d.hpp), which it
// doesn't expose either. An interpolation is the spec {"type", "x", "y", "z", "enableExtrapolation"}: type
// BilinearInterpolation, or FlatExtrapolator2D, which decorates the BilinearInterpolation of the same grid and adds
// "decoratedExtrapolation", whether enableExtrapolation() is called on the decorated interpolation before it is
// decorated; z is the list of the matrix's rows, all of one size, a row per y value; enableExtrapolation, whether
// enableExtrapolation() is called on the interpolation built. QuantLib's 2-D interpolations keep iterators into the
// grid and a reference to the matrix, so the call builds them over the spec's own vectors and a matrix that outlives
// them.
//
// The value at a point with QuantLib's allowExtrapolation flag; xMin, xMax, yMin, yMax; xValues and yValues, as lists
// of reals; zData, as the list of its rows; locateX and locateY at a point; and isInRange at a point.

#include "convert.hpp"

#include <ql/math/interpolations/bilinearinterpolation.hpp>
#include <ql/math/interpolations/flatextrapolation2d.hpp>
#include <ql/math/matrix.hpp>
#include <ql/shared_ptr.hpp>

#include <cstdint>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct Interpolation2DSpec {
            std::string type;
            std::vector<Real> x, y;
            Size rows = 0, columns = 0;
            std::vector<Real> z;  // row by row
            bool decoratedExtrapolation = false;
            bool enableExtrapolation = false;
        };

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " spec needs " + name);
            return *v;
        }

        Interpolation2DSpec interpolation2d(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a 2-D interpolation spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            const json::value* type = o.if_contains("type");
            if (type == nullptr || !type->is_string())
                throw InvalidParams("a 2-D interpolation spec needs a type: " + json::serialize(v));
            Interpolation2DSpec s;
            s.type = std::string(type->get_string());
            const std::string& what = s.type;
            bool flat = s.type == "FlatExtrapolator2D";
            if (!flat && s.type != "BilinearInterpolation")
                throw InvalidParams("unknown 2-D interpolation " + s.type);
            for (const auto& kv : o) {
                if (kv.key() != "type" && kv.key() != "x" && kv.key() != "y" && kv.key() != "z" &&
                    kv.key() != "enableExtrapolation" && !(flat && kv.key() == "decoratedExtrapolation"))
                    throw InvalidParams(what + " takes no key " + std::string(kv.key()));
            }
            s.x = reals(key(o, "x", what));
            s.y = reals(key(o, "y", what));
            const json::value& z = key(o, "z", what);
            if (!z.is_array())
                throw InvalidParams(what + " needs z as a list of rows");
            for (const json::value& row : z.get_array()) {
                std::vector<Real> r = reals(row);
                if (s.rows == 0)
                    s.columns = r.size();
                else if (r.size() != s.columns)
                    throw InvalidParams(what + " needs z's rows of one size");
                s.z.insert(s.z.end(), r.begin(), r.end());
                ++s.rows;
            }
            if (flat)
                s.decoratedExtrapolation = boolean(key(o, "decoratedExtrapolation", what));
            s.enableExtrapolation = boolean(key(o, "enableExtrapolation", what));
            return s;
        }

        // The interpolation over the spec's own x and y and the given matrix, which must outlive it.
        ext::shared_ptr<Interpolation2D> make(const Interpolation2DSpec& s, const Matrix& z) {
            auto bilinear = ext::make_shared<BilinearInterpolation>(s.x.begin(), s.x.end(), s.y.begin(), s.y.end(), z);
            ext::shared_ptr<Interpolation2D> out = bilinear;
            if (s.type == "FlatExtrapolator2D") {
                if (s.decoratedExtrapolation)
                    bilinear->enableExtrapolation();
                out = ext::make_shared<FlatExtrapolator2D>(bilinear);
            }
            if (s.enableExtrapolation)
                out->enableExtrapolation();
            return out;
        }

        Matrix matrix(const Interpolation2DSpec& s) { return Matrix(s.rows, s.columns, s.z.begin(), s.z.end()); }

        json::value realArray(const std::vector<Real>& v) {
            json::array out;
            for (Real x : v)
                out.emplace_back(x);
            return json::value(std::move(out));
        }

        // A member of the interpolation alone: (spec).
        Call whole(const Args& a, json::value (*f)(const Interpolation2D&)) {
            arity(a, 1);
            Interpolation2DSpec s = interpolation2d(a[0]);
            return [=] {
                Matrix z = matrix(s);
                return f(*make(s, z));
            };
        }

        // A member read at one coordinate: (spec, x or y).
        Call atCoordinate(const Args& a, json::value (*f)(const Interpolation2D&, Real)) {
            arity(a, 2);
            Interpolation2DSpec s = interpolation2d(a[0]);
            Real x = real(a[1]);
            return [=] {
                Matrix z = matrix(s);
                return f(*make(s, z), x);
            };
        }

        const Register unit({
            // (spec, x, y, allowExtrapolation)
            {"Interpolation2D::operator()",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Interpolation2DSpec s = interpolation2d(a[0]);
                 Real x = real(a[1]);
                 Real y = real(a[2]);
                 bool allow = boolean(a[3]);
                 return [=] {
                     Matrix z = matrix(s);
                     return json::value((*make(s, z))(x, y, allow));
                 };
             }},
            // (spec, x, y)
            {"Interpolation2D::isInRange",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Interpolation2DSpec s = interpolation2d(a[0]);
                 Real x = real(a[1]);
                 Real y = real(a[2]);
                 return [=] {
                     Matrix z = matrix(s);
                     return json::value(make(s, z)->isInRange(x, y));
                 };
             }},
            {"Interpolation2D::xMin",
             [](const Args& a) { return whole(a, [](const Interpolation2D& f) { return json::value(f.xMin()); }); }},
            {"Interpolation2D::xMax",
             [](const Args& a) { return whole(a, [](const Interpolation2D& f) { return json::value(f.xMax()); }); }},
            {"Interpolation2D::yMin",
             [](const Args& a) { return whole(a, [](const Interpolation2D& f) { return json::value(f.yMin()); }); }},
            {"Interpolation2D::yMax",
             [](const Args& a) { return whole(a, [](const Interpolation2D& f) { return json::value(f.yMax()); }); }},
            {"Interpolation2D::xValues",
             [](const Args& a) { return whole(a, [](const Interpolation2D& f) { return realArray(f.xValues()); }); }},
            {"Interpolation2D::yValues",
             [](const Args& a) { return whole(a, [](const Interpolation2D& f) { return realArray(f.yValues()); }); }},
            {"Interpolation2D::zData",
             [](const Args& a) {
                 return whole(a, [](const Interpolation2D& f) {
                     const Matrix& m = f.zData();
                     json::array out;
                     for (Size i = 0; i < m.rows(); ++i) {
                         json::array row;
                         for (Size j = 0; j < m.columns(); ++j)
                             row.emplace_back(m[i][j]);
                         out.emplace_back(std::move(row));
                     }
                     return json::value(std::move(out));
                 });
             }},
            {"Interpolation2D::locateX",
             [](const Args& a) {
                 return atCoordinate(a, [](const Interpolation2D& f, Real x) {
                     return json::value(static_cast<std::int64_t>(f.locateX(x)));
                 });
             }},
            {"Interpolation2D::locateY",
             [](const Args& a) {
                 return atCoordinate(a, [](const Interpolation2D& f, Real y) {
                     return json::value(static_cast<std::int64_t>(f.locateY(y)));
                 });
             }},
        });

    }

}
