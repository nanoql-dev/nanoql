// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/matrix.hpp: inverse and determinant, which run Boost uBLAS's LU factorization, and the members of Matrix
// the wheel doesn't expose. A Matrix argument is the spec {"rows", "columns", "data"}, its elements row by row, from
// which the call builds the Matrix through its iterator-range constructor; a Matrix result is the list of its rows,
// each a list of reals.

#include "convert.hpp"

#include <ql/math/matrix.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        struct MatrixSpec {
            Size rows, columns;
            std::vector<Real> data;
        };

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("a Matrix spec needs ") + name);
            return *v;
        }

        Size size(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > INT_MAX)
                throw InvalidParams("expected a size, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        MatrixSpec matrix(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a Matrix spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            for (const auto& kv : o) {
                if (kv.key() != "rows" && kv.key() != "columns" && kv.key() != "data")
                    throw InvalidParams("Matrix takes no key " + std::string(kv.key()));
            }
            MatrixSpec s{size(key(o, "rows")), size(key(o, "columns")), reals(key(o, "data"))};
            // A range larger than the matrix overruns it, and a smaller one leaves elements uninitialized.
            if (s.data.size() != s.rows * s.columns)
                throw InvalidParams("a Matrix spec's data holds rows x columns elements");
            return s;
        }

        Matrix make(const MatrixSpec& s) { return Matrix(s.rows, s.columns, s.data.begin(), s.data.end()); }

        json::value matrixResult(const Matrix& m) {
            json::array out;
            for (Size i = 0; i < m.rows(); ++i) {
                json::array row;
                for (Size j = 0; j < m.columns(); ++j)
                    row.emplace_back(m(i, j));
                out.emplace_back(std::move(row));
            }
            return json::value(std::move(out));
        }

        template <class I>
        json::value rangeResult(I begin, I end) {
            json::array out;
            for (; begin != end; ++begin)
                out.emplace_back(*begin);
            return json::value(std::move(out));
        }

        // A function of one Matrix.
        Call unary(const Args& a, json::value (*f)(const Matrix&)) {
            arity(a, 1);
            MatrixSpec s = matrix(a[0]);
            return [=] { return f(make(s)); };
        }

        // A function of two Matrices.
        Call binary(const Args& a, json::value (*f)(Matrix&, const Matrix&)) {
            arity(a, 2);
            MatrixSpec s1 = matrix(a[0]), s2 = matrix(a[1]);
            return [=] {
                Matrix m1 = make(s1);
                return f(m1, make(s2));
            };
        }

        // A function of a Matrix and a Real.
        Call withScalar(const Args& a, json::value (*f)(Matrix&, Real)) {
            arity(a, 2);
            MatrixSpec s = matrix(a[0]);
            Real x = real(a[1]);
            return [=] {
                Matrix m = make(s);
                return f(m, x);
            };
        }

        json::value integerResult(Size n) { return json::value(static_cast<std::int64_t>(n)); }

        const Register unit({
            {"inverse",
             [](const Args& a) { return unary(a, [](const Matrix& m) { return matrixResult(inverse(m)); }); }},
            {"determinant",
             [](const Args& a) { return unary(a, [](const Matrix& m) { return json::value(determinant(m)); }); }},
            {"Matrix(Size,Size)",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 Size rows = size(a[0]), columns = size(a[1]);
                 // The elements are left uninitialized, so only the dimensions are read.
                 return [=] {
                     Matrix m(rows, columns);
                     return json::value(json::array{integerResult(m.rows()), integerResult(m.columns())});
                 };
             }},
            {"Matrix::rows",
             [](const Args& a) { return unary(a, [](const Matrix& m) { return integerResult(m.rows()); }); }},
            {"Matrix::columns",
             [](const Args& a) { return unary(a, [](const Matrix& m) { return integerResult(m.columns()); }); }},
            {"Matrix::size1",
             [](const Args& a) { return unary(a, [](const Matrix& m) { return integerResult(m.size1()); }); }},
            {"Matrix::size2",
             [](const Args& a) { return unary(a, [](const Matrix& m) { return integerResult(m.size2()); }); }},
            {"Matrix::empty",
             [](const Args& a) { return unary(a, [](const Matrix& m) { return json::value(m.empty()); }); }},
            {"Matrix::diagonal",
             [](const Args& a) -> Call {
                 return unary(a, [](const Matrix& m) {
                     Array d = m.diagonal();
                     return rangeResult(d.begin(), d.end());
                 });
             }},
            {"Matrix::begin",
             [](const Args& a) -> Call {
                 return unary(a, [](const Matrix& m) { return rangeResult(m.begin(), m.end()); });
             }},
            {"outerProduct(const Array&,const Array&)",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 std::vector<Real> x = reals(a[0]), y = reals(a[1]);
                 return [=] {
                     const Array v1(x.begin(), x.end()), v2(y.begin(), y.end());
                     return matrixResult(outerProduct(v1, v2));
                 };
             }},
            {"Matrix::row_begin",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 MatrixSpec s = matrix(a[0]);
                 Size i = size(a[1]);
                 if (i >= s.rows)
                     throw InvalidParams("row_begin's row is unchecked: it must be less than the rows");
                 return [=] {
                     const Matrix m = make(s);
                     return rangeResult(m.row_begin(i), m.row_end(i));
                 };
             }},
            {"Matrix::at",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 MatrixSpec s = matrix(a[0]);
                 Size i = size(a[1]);
                 return [=] {
                     const Matrix m = make(s);
                     Matrix::const_row_iterator row = m.at(i);
                     return rangeResult(row, row + m.columns());
                 };
             }},
            {"Matrix::column_begin",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 MatrixSpec s = matrix(a[0]);
                 Size j = size(a[1]);
                 if (j >= s.columns)
                     throw InvalidParams("column_begin's column is unchecked: it must be less than the columns");
                 return [=] {
                     const Matrix m = make(s);
                     return rangeResult(m.column_begin(j), m.column_end(j));
                 };
             }},
            {"Matrix::swap",
             [](const Args& a) -> Call {
                 return binary(a, [](Matrix& m1, const Matrix& m2) {
                     Matrix other = m2;
                     m1.swap(other);
                     return json::value(json::array{matrixResult(m1), matrixResult(other)});
                 });
             }},
            {"Matrix::operator+=(const Matrix&)",
             [](const Args& a) -> Call {
                 return binary(a, [](Matrix& m1, const Matrix& m2) {
                     m1 += m2;
                     return matrixResult(m1);
                 });
             }},
            {"Matrix::operator-=(const Matrix&)",
             [](const Args& a) -> Call {
                 return binary(a, [](Matrix& m1, const Matrix& m2) {
                     m1 -= m2;
                     return matrixResult(m1);
                 });
             }},
            {"Matrix::operator*=(Real)",
             [](const Args& a) -> Call {
                 return withScalar(a, [](Matrix& m, Real x) {
                     m *= x;
                     return matrixResult(m);
                 });
             }},
            {"Matrix::operator/=(Real)",
             [](const Args& a) -> Call {
                 return withScalar(a, [](Matrix& m, Real x) {
                     m /= x;
                     return matrixResult(m);
                 });
             }},
            {"Matrix::operator==",
             [](const Args& a) -> Call {
                 return binary(a, [](Matrix& m1, const Matrix& m2) { return json::value(m1 == m2); });
             }},
            {"Matrix::operator!=",
             [](const Args& a) -> Call {
                 return binary(a, [](Matrix& m1, const Matrix& m2) { return json::value(m1 != m2); });
             }},
            {"operator-(const Matrix&)",
             [](const Args& a) { return unary(a, [](const Matrix& m) { return matrixResult(-m); }); }},
            {"operator/(const Matrix&,Real)",
             [](const Args& a) -> Call {
                 return withScalar(a, [](Matrix& m, Real x) {
                     const Matrix& c = m;
                     return matrixResult(c / x);
                 });
             }},
            {"operator<<(Matrix)",
             [](const Args& a) { return unary(a, [](const Matrix& m) { return json::value(streamed(m)); }); }},
        });

    }

}
