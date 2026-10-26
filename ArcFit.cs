using System;
using System.Collections.Generic;

namespace PiquageTube
{
    /// <summary>
    /// Polyligne dense -> chaîne d'arcs TANGENTS (bulges DXF) pour le grignotage continu
    /// (AutoPunch / Metalix) : un arc = un passage de poinçon continu au lieu d'un coup par mini-segment.
    /// Les coins francs (> CornerDeg) coupent la polyligne en sections lisses, ajustées séparément.
    /// Même algo que DeveloppeTube (validé : écart ~0,04 mm avec la courbe d'origine).
    /// </summary>
    public static class ArcFit
    {
        public const double DefaultTol = 0.03;                      // mm, écart max aux points d'origine
        const double MaxHalfSweep = 80.0 * Math.PI / 180.0;         // |alpha| max par arc (balayage < 160°)
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

        // ---- ajustement d'une section lisse : chaîne d'arcs tangents, greedy ----
        static List<(double X, double Y, double B)> FitSection(List<Pt2> pts, double tol)
        {
            var res = new List<(double X, double Y, double B)>();
            int n = pts.Count;
            if (n == 0) return res;
            if (n == 1) { res.Add((pts[0].X, pts[0].Y, 0)); return res; }
            if (n == 2) { res.Add((pts[0].X, pts[0].Y, 0)); res.Add((pts[1].X, pts[1].Y, 0)); return res; }

            // tangente de départ = direction du premier segment
            double tx = pts[1].X - pts[0].X, ty = pts[1].Y - pts[0].Y;
            double l0 = Math.Sqrt(tx * tx + ty * ty);
            if (l0 > 1e-12) { tx /= l0; ty /= l0; } else { tx = 1; ty = 0; }

            int i = 0;
            while (i < n - 1)
            {
                double x0 = pts[i].X, y0 = pts[i].Y;
                int best = -1; double bestAlpha = 0;

                for (int j = i + 1; j < n; j++)
                {
                    double dx = pts[j].X - x0, dy = pts[j].Y - y0;
                    double c = Math.Sqrt(dx * dx + dy * dy);
                    if (c < 1e-9) { if (best < 0) continue; else break; }
                    double alpha = Math.Atan2(tx * dy - ty * dx, tx * dx + ty * dy);
                    if (Math.Abs(alpha) > MaxHalfSweep) break;

                    bool ok = true;
                    if (Math.Abs(alpha) < 1e-7)
                    {
                        for (int k = i + 1; k < j && ok; k++)
                            if (Math.Abs(tx * (pts[k].Y - y0) - ty * (pts[k].X - x0)) > tol) ok = false;
                    }
                    else
                    {
                        double r = c / (2 * Math.Sin(alpha));              // signé : > 0 = arc vers la gauche
                        double cx = x0 - ty * r, cy = y0 + tx * r;
                        double ar = Math.Abs(r);
                        for (int k = i + 1; k < j && ok; k++)
                        {
                            double ex = pts[k].X - cx, ey = pts[k].Y - cy;
                            if (Math.Abs(Math.Sqrt(ex * ex + ey * ey) - ar) > tol) ok = false;
                        }
                    }
                    if (!ok) break;
                    best = j; bestAlpha = alpha;
                }

                if (best < 0)
                {
                    // aucun arc possible (angle vif) : segment droit, la tangente repart du segment
                    best = i + 1;
                    while (best < n - 1 && Math.Abs(pts[best].X - x0) + Math.Abs(pts[best].Y - y0) < 1e-9) best++;
                    double dx = pts[best].X - x0, dy = pts[best].Y - y0;
                    double c = Math.Sqrt(dx * dx + dy * dy);
                    res.Add((x0, y0, 0));
                    if (c > 1e-9) { tx = dx / c; ty = dy / c; }
                }
                else
                {
                    res.Add((x0, y0, Math.Tan(bestAlpha / 2)));
                    double ca = Math.Cos(2 * bestAlpha), sa = Math.Sin(2 * bestAlpha);
                    double ntx = tx * ca - ty * sa, nty = tx * sa + ty * ca;
                    tx = ntx; ty = nty;
                }
                i = best;
            }
            res.Add((pts[n - 1].X, pts[n - 1].Y, 0));
            return res;
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
