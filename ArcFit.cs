using System;
using System.Collections.Generic;

namespace PiquageTube
{
    /// <summary>
    /// Polyligne dense -> chaîne d'arcs LONGS (bulges DXF) pour le grignotage continu
    /// (AutoPunch / Metalix) : un arc = un passage de poinçon continu au lieu d'un coup par mini-segment.
    /// Les coins francs (> CornerDeg) coupent la polyligne en sections lisses, ajustées séparément.
    /// Même algo que DeveloppeTube (validé : écart ~0,04 mm avec la courbe d'origine).
    /// </summary>
    public static class ArcFit
    {
        public const double DefaultTol = 0.08;                      // mm, écart max aux points d'origine
        const double MaxBulge = 0.8391;                             // tan(40°) : balayage max 160° par arc
        const double MaxKinkDeg = 4.0;                              // coude max entre 2 arcs consécutifs
        const double CornerDeg = 10.0;                              // coin franc au-delà de ce changement de direction

        /// <summary>Convertit une polyligne dense en sommets + bulges (bulge i = segment i -> i+1).</summary>
        public static void FitPolyline(IReadOnlyList<Pt2> src, bool closed,
                                       out List<Pt2> pts, out List<double> bulges, double tol = DefaultTol)
        {
            // nettoyage : points confondus
            var p = new List<Pt2>(src.Count);
            foreach (var q in src)
                if (p.Count == 0 || Dist(p[p.Count - 1], q) > 1e-6) p.Add(q);
            if (closed && p.Count > 1 && Dist(p[0], p[p.Count - 1]) < 1e-6) p.RemoveAt(p.Count - 1);

            int n = p.Count;
            pts = new List<Pt2>(); bulges = new List<double>();
            if (n < 3)
            {
                foreach (var q in p) { pts.Add(q); bulges.Add(0); }
                return;
            }

            // coins francs
            var corners = new List<int>();
            double cornerRad = CornerDeg * Math.PI / 180.0;
            for (int i = 0; i < n; i++)
            {
                if (!closed && (i == 0 || i == n - 1)) continue;
                var a = p[(i - 1 + n) % n]; var b = p[i]; var c = p[(i + 1) % n];
                if (Math.Abs(Turn(a, b, c)) > cornerRad) corners.Add(i);
            }

            // sections lisses
            var sections = new List<List<Pt2>>();
            if (!closed)
            {
                var bnd = new List<int> { 0 }; bnd.AddRange(corners); bnd.Add(n - 1);
                for (int k = 0; k + 1 < bnd.Count; k++)
                    sections.Add(p.GetRange(bnd[k], bnd[k + 1] - bnd[k] + 1));
            }
            else if (corners.Count == 0)
            {
                var s = new List<Pt2>(p) { p[0] };
                sections.Add(s);
            }
            else
            {
                int m = corners.Count;
                for (int k = 0; k < m; k++)
                {
                    int s0 = corners[k], e0 = corners[(k + 1) % m];
                    int cnt = ((e0 - s0) % n + n) % n; if (cnt == 0) cnt = n;
                    var s = new List<Pt2>(cnt + 1);
                    for (int t = 0; t <= cnt; t++) s.Add(p[(s0 + t) % n]);
                    sections.Add(s);
                }
            }

            for (int k = 0; k < sections.Count; k++)
            {
                var v = FitSection(sections[k], tol);
                int take = v.Count - 1;                                   // le dernier sommet = début de la section suivante
                if (!closed && k == sections.Count - 1) take = v.Count;   // polyligne ouverte : on garde le dernier
                for (int i = 0; i < take; i++) { pts.Add(new Pt2(v[i].X, v[i].Y)); bulges.Add(v[i].B); }
            }
        }

        // ---- ajustement d'une section lisse : arcs "libres" (extrémités sur la courbe, flèche optimisée) ----
        // peu d'arcs, longs ; coude max MaxKinkDeg entre deux arcs consécutifs
        static List<(double X, double Y, double B)> FitSection(List<Pt2> pts, double tol)
        {
            var res = new List<(double X, double Y, double B)>();
            int n = pts.Count;
            if (n == 0) return res;
            if (n == 1) { res.Add((pts[0].X, pts[0].Y, 0)); return res; }

            double maxKink = MaxKinkDeg * Math.PI / 180.0;
            double prevEnd = double.NaN;
            int i = 0;
            while (i < n - 1)
            {
                double x0 = pts[i].X, y0 = pts[i].Y;
                int bestJ = -1; double bestB = 0, bestEnd = 0;

                for (int j = i + 1; j < n; j++)
                {
                    double dx = pts[j].X - x0, dy = pts[j].Y - y0;
                    double c = Math.Sqrt(dx * dx + dy * dy);
                    if (c < 1e-9) { if (bestJ < 0) continue; else break; }
                    double b = 0, dev = 0;
                    if (j > i + 1) b = BestBulge(pts, i, j, out dev);
                    if (dev > tol) break;
                    double al = 2 * Math.Atan(b), ch = Math.Atan2(dy, dx);
                    if (!double.IsNaN(prevEnd) && AngDiff(ch - al, prevEnd) > maxKink) continue;
                    bestJ = j; bestB = b; bestEnd = ch + al;
                }

                if (bestJ < 0)
                {
                    bestJ = i + 1;
                    while (bestJ < n - 1 && Math.Abs(pts[bestJ].X - x0) + Math.Abs(pts[bestJ].Y - y0) < 1e-9) bestJ++;
                    bestB = 0;
                    bestEnd = Math.Atan2(pts[bestJ].Y - y0, pts[bestJ].X - x0);
                }
                res.Add((x0, y0, bestB));
                prevEnd = bestEnd;
                i = bestJ;
            }
            res.Add((pts[n - 1].X, pts[n - 1].Y, 0));
            return res;
        }

        static double AngDiff(double a, double b)
        {
            double d = (a - b) % (2 * Math.PI);
            if (d > Math.PI) d -= 2 * Math.PI; else if (d < -Math.PI) d += 2 * Math.PI;
            return Math.Abs(d);
        }

        static double BestBulge(List<Pt2> p, int i, int j, out double dev)
        {
            double lo = -MaxBulge, hi = MaxBulge;
            for (int it = 0; it < 40; it++)
            {
                double m1 = lo + (hi - lo) / 3, m2 = hi - (hi - lo) / 3;
                if (Dev(p, i, j, m1) < Dev(p, i, j, m2)) hi = m2; else lo = m1;
            }
            double b = (lo + hi) / 2;
            dev = Dev(p, i, j, b);
            return b;
        }

        static double Dev(List<Pt2> p, int i, int j, double b)
        {
            double x0 = p[i].X, y0 = p[i].Y, dx = p[j].X - x0, dy = p[j].Y - y0;
            double c = Math.Sqrt(dx * dx + dy * dy), m = 0;
            double sweep = 4 * Math.Atan(b);
            if (Math.Abs(sweep) < 1e-9)
            {
                for (int k = i + 1; k < j; k++)
                {
                    double d = Math.Abs(dx * (p[k].Y - y0) - dy * (p[k].X - x0)) / c;
                    if (d > m) m = d;
                }
                return m;
            }
            double r = c / (2 * Math.Sin(sweep / 2));
            double t = Math.Atan2(dy, dx) - sweep / 2;
            double cx = x0 - r * Math.Sin(t), cy = y0 + r * Math.Cos(t), ar = Math.Abs(r);
            for (int k = i + 1; k < j; k++)
            {
                double ex = p[k].X - cx, ey = p[k].Y - cy;
                double d = Math.Abs(Math.Sqrt(ex * ex + ey * ey) - ar);
                if (d > m) m = d;
            }
            return m;
        }

        static double Dist(Pt2 a, Pt2 b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        static double Turn(Pt2 a, Pt2 b, Pt2 c)
        {
            double v1x = b.X - a.X, v1y = b.Y - a.Y, v2x = c.X - b.X, v2y = c.Y - b.Y;
            return Math.Atan2(v1x * v2y - v1y * v2x, v1x * v2x + v1y * v2y);
        }
    }
}
