using System;
using System.Collections.Generic;

namespace PiquageTube
{
    /// <summary>
    /// Piquage cylindre/cylindre OU cône/cylindre (branche conique = descente télescopique).
    /// Repère : axe run = X, surface run y²+z²=R². Axe branche plan X-Z incliné de theta,
    /// déporté de "deport" suivant Y. Calcul fibre neutre. Racine haute (sabot sur le dessus).
    /// </summary>
    public class PiquageParams
    {
        public double DRun = 200;
        public double DBranche = 100;   // Ø branche côté piqué (base du sabot)
        public bool   Conique = false;  // branche conique (descente)
        public double DTop = 100;       // Ø branche côté opposé (haut)
        public double AngleDeg = 90;
        public double Deport = 0;
        public double Epaisseur = 0.8;
        public double Recouvr = 10;
        public double LDroite = 80;     // longueur (axiale) de branche
        public int    Segments = 360;
    }

    public struct Pt2 { public double X, Y; public Pt2(double x, double y){X=x;Y=y;} }
    public struct Pt3 { public double X, Y, Z; public Pt3(double x,double y,double z){X=x;Y=y;Z=z;} }

    public class PiquageResult
    {
        public bool Ok;
        public string Message = "";
        public bool IsCone;
        public List<Pt2> BrancheContour = new();
        public List<Pt2> BrancheSabot   = new();
        public List<Pt2> BrancheTop     = new();
        public double BrancheLargeur, BrancheHauteur;
        public double SabotMax, SabotMin;
        public double DemiAngleDeg, SlantBase, SlantTop;
        public List<Pt2> TrouContour = new();
        public double TrouLargeur, TrouHauteur;
        public List<Pt3> Inter3D = new();
        public double R, r;
    }

    public static class Geometry
    {
        public static PiquageResult Compute(PiquageParams p)
        {
            double R  = (p.DRun - p.Epaisseur) / 2.0;
            double rb = (p.DBranche - p.Epaisseur) / 2.0;
            double rt = (p.DTop - p.Epaisseur) / 2.0;
            bool cone = p.Conique && Math.Abs(rb - rt) > 1e-4 && p.LDroite > 1e-6;
            return cone ? Cone(p, R, rb, rt) : Cyl(p, R, rb);
        }

        // ---------- BRANCHE CYLINDRIQUE ----------
        private static PiquageResult Cyl(PiquageParams p, double R, double r)
        {
            var res = new PiquageResult { R = R, r = r, IsCone = false };
            if (R <= 0 || r <= 0) { res.Message = "Diamètres invalides."; return res; }
            if (r > R + 1e-6)     { res.Message = "Branche plus grosse que le run : impossible."; return res; }
            double th = p.AngleDeg * Math.PI / 180.0;
            if (p.AngleDeg <= 1 || p.AngleDeg >= 179) { res.Message = "Angle hors plage (2°..178°)."; return res; }
            double sinth = Math.Sin(th), costh = Math.Cos(th);
            if (Math.Abs(p.Deport) + r > R + 1e-6)
                res.Message = $"⚠ Déport+rayon ({Math.Abs(p.Deport)+r:F1}) > R run ({R:F1}) : sabot tronqué.";

            int N = Math.Max(60, p.Segments);
            double lapArc = p.Recouvr / r;
            double betaEnd = 2 * Math.PI + lapArc;
            int M = (int)Math.Round(N * betaEnd / (2 * Math.PI));
            var t = new double[M + 1]; var yA = new double[M + 1]; var zA = new double[M + 1]; var xA = new double[M + 1]; var bA = new double[M + 1];
            double tmin = double.MaxValue, tmax = double.MinValue;
            for (int i = 0; i <= M; i++)
            {
                double b = betaEnd * i / M; bA[i] = b;
                double y = p.Deport + r * Math.Cos(b);
                double inside = R * R - y * y; if (inside < 0) inside = 0;
                double z = Math.Sqrt(inside);
                double ti = (z - r * Math.Sin(b) * costh) / sinth;
                t[i] = ti; yA[i] = y; zA[i] = z; xA[i] = ti * costh - r * Math.Sin(b) * sinth;
                if (b <= 2 * Math.PI + 1e-9) { if (ti < tmin) tmin = ti; if (ti > tmax) tmax = ti; }
            }
            double gmax = 0; var sabot = new List<Pt2>(M + 1);
            for (int i = 0; i <= M; i++) { double g = tmax - t[i]; if (g < 0) g = 0; if (g > gmax) gmax = g; sabot.Add(new Pt2(r * bA[i], g)); }
            double ytop = gmax + p.LDroite;
            res.BrancheSabot = sabot; res.BrancheLargeur = 2 * Math.PI * r; res.BrancheHauteur = ytop;
            res.SabotMax = gmax; res.SabotMin = Math.Max(0, gmax - (tmax - tmin));
            var c = new List<Pt2>(M + 4) { new Pt2(0, ytop), new Pt2(sabot[M].X, ytop) };
            for (int i = M; i >= 0; i--) c.Add(sabot[i]);
            res.BrancheContour = c;
            FillTrou(res, R, yA, zA, xA, (int)Math.Round(M * (2 * Math.PI) / betaEnd));
            res.Ok = true; return res;
        }

        // ---------- BRANCHE CONIQUE (tronc de cône) ----------
        private static PiquageResult Cone(PiquageParams p, double R, double rb, double rt)
        {
            var res = new PiquageResult { R = R, r = rb, IsCone = true };
            if (R <= 0 || rb <= 0 || rt <= 0) { res.Message = "Diamètres invalides."; return res; }
            double th = p.AngleDeg * Math.PI / 180.0;
            if (p.AngleDeg <= 1 || p.AngleDeg >= 179) { res.Message = "Angle hors plage (2°..178°)."; return res; }
            double sinth = Math.Sin(th), costh = Math.Cos(th);
            double tana = (rb - rt) / p.LDroite;
            double alpha = Math.Atan(Math.Abs(tana)); double sina = Math.Sin(alpha), cosa = Math.Cos(alpha);
            double lApex = rb / tana;
            res.DemiAngleDeg = alpha * 180.0 / Math.PI;

            int N = Math.Max(120, p.Segments);
            double lap = p.Recouvr / rb;
            double betaEnd = 2 * Math.PI + lap;
            int M = (int)Math.Round(N * betaEnd / (2 * Math.PI));
            var ls = new double[M + 1]; var yA = new double[M + 1]; var zA = new double[M + 1]; var xA = new double[M + 1]; var bA = new double[M + 1];
            int nan = 0;
            for (int i = 0; i <= M; i++)
            {
                double b = betaEnd * i / M; bA[i] = b;
                double root = SolveL(b, rb, tana, p.Deport, R, sinth, costh, lApex);
                ls[i] = root;
                if (double.IsNaN(root)) { nan++; yA[i] = yA[Math.Max(0, i - 1)]; zA[i] = zA[Math.Max(0, i - 1)]; xA[i] = xA[Math.Max(0, i - 1)]; continue; }
                double rho = rb - root * tana;
                double y = p.Deport + rho * Math.Cos(b);
                double z = Math.Sqrt(Math.Max(0, R * R - y * y));
                yA[i] = y; zA[i] = z; xA[i] = root * costh - rho * Math.Sin(b) * sinth;
            }
            if (nan > 0) res.Message = $"⚠ {nan} points hors intersection (déport/cône trop fort) : contour partiel.";

            double psi0 = bA[M / 2] * sina;
            double Lt = (lApex - p.LDroite) / cosa;
            var baseArc = new List<Pt2>(M + 1); var topArc = new List<Pt2>(M + 1);
            for (int i = 0; i <= M; i++)
            {
                double psi = bA[i] * sina;
                double Lg = double.IsNaN(ls[i]) ? Lt : (lApex - ls[i]) / cosa;
                baseArc.Add(new Pt2(Lg * Math.Cos(psi - psi0), Lg * Math.Sin(psi - psi0)));
                topArc.Add(new Pt2(Lt * Math.Cos(psi - psi0), Lt * Math.Sin(psi - psi0)));
            }
            // contour fermé : arc base (sabot) -> côté -> arc haut (retour) -> côté
            var c = new List<Pt2>(2 * M + 2);
            for (int i = 0; i <= M; i++) c.Add(baseArc[i]);
            for (int i = M; i >= 0; i--) c.Add(topArc[i]);
            // recentrage bbox -> origine
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var q in c) { minX = Math.Min(minX, q.X); minY = Math.Min(minY, q.Y); maxX = Math.Max(maxX, q.X); maxY = Math.Max(maxY, q.Y); }
            void Shift(List<Pt2> L) { for (int i = 0; i < L.Count; i++) L[i] = new Pt2(L[i].X - minX, L[i].Y - minY); }
            Shift(c); Shift(baseArc); Shift(topArc);
            res.BrancheContour = c; res.BrancheSabot = baseArc; res.BrancheTop = topArc;
            res.BrancheLargeur = 2 * Math.PI * rb; res.BrancheHauteur = maxY - minY;
            res.SlantBase = (lApex) / cosa; res.SlantTop = Lt;
            double gmin = double.MaxValue, gmax = double.MinValue;
            for (int i = 0; i <= (int)(M * 2 * Math.PI / betaEnd); i++) { if (!double.IsNaN(ls[i])) { double Lg = (lApex - ls[i]) / cosa; gmin = Math.Min(gmin, Lg); gmax = Math.Max(gmax, Lg); } }
            res.SabotMax = (gmax - gmin);

            FillTrou(res, R, yA, zA, xA, (int)Math.Round(M * (2 * Math.PI) / betaEnd));
            res.Ok = true; return res;
        }

        private static double SolveL(double b, double rb, double tana, double deport, double R, double sinth, double costh, double lApex)
        {
            double lo = -0.5 * rb, hi = lApex * 0.9;
            int steps = 400; double prevL = double.NaN, prevF = double.NaN;
            Func<double, double> F = (l) =>
            {
                double rho = rb - l * tana;
                double y = deport + rho * Math.Cos(b);
                double inside = R * R - y * y;
                if (inside < 0) return double.NaN;
                double z = Math.Sqrt(inside);
                double zc = l * sinth + rho * Math.Sin(b) * costh;
                return zc - z;
            };
            for (int i = 0; i <= steps; i++)
            {
                double l = lo + (hi - lo) * i / steps;
                double f = F(l);
                if (double.IsNaN(f)) { prevL = double.NaN; prevF = double.NaN; continue; }
                if (!double.IsNaN(prevF) && prevF * f < 0)
                {
                    double a0 = prevL, a1 = l;
                    for (int k = 0; k < 60; k++)
                    {
                        double m = (a0 + a1) / 2; double fm = F(m);
                        if (double.IsNaN(fm)) break;
                        if (prevF * fm <= 0) a1 = m; else a0 = m;
                    }
                    return (a0 + a1) / 2;
                }
                prevL = l; prevF = f;
            }
            return double.NaN;
        }

        private static void FillTrou(PiquageResult res, double R, double[] yA, double[] zA, double[] xA, int M2)
        {
            var trou = new List<Pt2>();
            double aMin = double.MaxValue, aMax = double.MinValue, xMin = double.MaxValue, xMax = double.MinValue;
            for (int i = 0; i <= M2 && i < yA.Length; i++)
            {
                double phi = Math.Atan2(zA[i], yA[i]); double arc = R * phi; double x = xA[i];
                trou.Add(new Pt2(arc, x)); res.Inter3D.Add(new Pt3(x, yA[i], zA[i]));
                if (arc < aMin) aMin = arc; if (arc > aMax) aMax = arc; if (x < xMin) xMin = x; if (x > xMax) xMax = x;
            }
            double cx = (aMin + aMax) / 2, cy = (xMin + xMax) / 2;
            for (int i = 0; i < trou.Count; i++) trou[i] = new Pt2(trou[i].X - cx, trou[i].Y - cy);
            res.TrouContour = trou; res.TrouLargeur = aMax - aMin; res.TrouHauteur = xMax - xMin;
        }
    }
}
