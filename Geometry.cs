using System;
using System.Collections.Generic;

namespace PiquageTube
{
    /// <summary>
    /// Piquage / culotte : tube AJOUTÉ (cylindrique ou conique) piqué sur un tube RÉCEPTEUR
    /// cylindrique OU CONIQUE (Ø bas ≠ Ø haut). Calcul fibre neutre.
    /// Récepteur conique => le trou se déroule en SECTEUR de cône (pas en rectangle plat).
    /// </summary>
    public class PiquageParams
    {
        // --- tube récepteur (principal) ---
        public double DRun = 200;        // Ø récepteur BAS (ou Ø unique si cylindrique)
        public bool   RunConique = false;
        public double DRunHaut = 200;    // Ø récepteur HAUT
        public double LRun = 400;        // longueur du récepteur (sur laquelle il se réduit)
        // --- tube ajouté (branche / manchon) ---
        public double DBranche = 100;    // Ø ajouté BASE (côté piqué)
        public bool   Conique = false;   // tube ajouté conique
        public double DTop = 100;        // Ø ajouté HAUT
        public double AngleDeg = 90;
        public double Deport = 0;
        public double Epaisseur = 0.8;
        public double Recouvr = 10;
        public double LDroite = 80;      // longueur (axiale) du tube ajouté
        public int    Segments = 360;
    }

    public struct Pt2 { public double X, Y; public Pt2(double x, double y){X=x;Y=y;} }
    public struct Pt3 { public double X, Y, Z; public Pt3(double x,double y,double z){X=x;Y=y;Z=z;} }

    public class PiquageResult
    {
        public bool Ok;
        public string Message = "";
        public bool IsCone;               // tube ajouté conique
        public bool RunIsCone;            // récepteur conique
        public double RunKr;              // pente de rayon du récepteur (dR/dx)
        public List<Pt2> BrancheContour = new();
        public List<Pt2> BrancheSabot   = new();
        public List<Pt2> BrancheTop     = new();
        public double BrancheLargeur, BrancheHauteur;
        public double SabotMax, SabotMin;
        public double DemiAngleDeg, SlantBase, SlantTop;
        public List<Pt2> TrouContour = new();
        public double TrouLargeur, TrouHauteur;
        public List<Pt3> Inter3D = new();        // courbe de sabot (3D, repère récepteur)
        public List<Pt3> BrancheTop3D = new();   // anneau haut du tube ajouté (3D)
        public double R, r;                       // R = rayon moyen récepteur, r = rayon base ajouté
        // valeurs nominales (Ø extérieurs / angle) pour les cotes 3D
        public double NomDRun, NomDRunHaut, NomDBr, NomDTop, NomAngle, NomDeport;
    }

    public static class Geometry
    {
        public static PiquageResult Compute(PiquageParams p)
        {
            double ep = p.Epaisseur;
            double Rbas = (p.DRun - ep) / 2.0;
            double Rhaut = p.RunConique ? (p.DRunHaut - ep) / 2.0 : Rbas;
            double Lrun = Math.Max(1e-3, p.LRun);
            double kr = p.RunConique ? (Rhaut - Rbas) / Lrun : 0.0;   // pente rayon récepteur
            double Rmid = (Rbas + Rhaut) / 2.0;

            double rb = (p.DBranche - ep) / 2.0, rt = (p.DTop - ep) / 2.0;
            bool coneBr = p.Conique && Math.Abs(rb - rt) > 1e-4 && p.LDroite > 1e-6;
            double tana = coneBr ? (rb - rt) / p.LDroite : 0.0;

            var res = new PiquageResult { R = Rmid, r = rb, IsCone = coneBr, RunIsCone = p.RunConique, RunKr = kr,
                NomDRun = p.DRun, NomDRunHaut = p.DRunHaut, NomDBr = p.DBranche, NomDTop = p.DTop, NomAngle = p.AngleDeg, NomDeport = p.Deport };
            if (Rbas <= 0 || Rhaut <= 0 || rb <= 0 || rt <= 0) { res.Message = "Diamètres invalides."; return res; }
            if (p.AngleDeg <= 1 || p.AngleDeg >= 179) { res.Message = "Angle hors plage (2°..178°)."; return res; }
            double th = p.AngleDeg * Math.PI / 180.0, sinth = Math.Sin(th), costh = Math.Cos(th), dep = p.Deport;

            int N = Math.Max(120, p.Segments);
            double lap = p.Recouvr / rb;
            double betaEnd = 2 * Math.PI + lap;
            int M = (int)Math.Round(N * betaEnd / (2 * Math.PI));
            int M2 = (int)Math.Round(M * (2 * Math.PI) / betaEnd);

            // --- intersection tube ajouté x récepteur(cône) ---
            var tArr = new double[M + 1]; var bA = new double[M + 1];
            var X = new double[M + 1]; var Y = new double[M + 1]; var Z = new double[M + 1];
            int nan = 0;
            for (int i = 0; i <= M; i++)
            {
                double b = betaEnd * i / M; bA[i] = b;
                double t = SolveT(b, rb, tana, dep, Rmid, kr, sinth, costh, Rbas, Rhaut);
                tArr[i] = t;
                if (double.IsNaN(t)) { nan++; int j = Math.Max(0, i - 1); X[i] = X[j]; Y[i] = Y[j]; Z[i] = Z[j]; continue; }
                double r = rb - t * tana;
                X[i] = t * costh - r * Math.Sin(b) * sinth;
                Y[i] = dep + r * Math.Cos(b);
                Z[i] = t * sinth + r * Math.Sin(b) * costh;
            }
            if (nan > M * 0.15)
            {
                res.Ok = false;
                res.Message =
                    "Intersection introuvable avec ces réglages.\n" +
                    "→ Décoche le conique (récepteur ou ajouté), ou remets Angle 90° / Déport 0,\n" +
                    "   ou réduis l'écart de diamètres.";
                return res;
            }
            if (nan > 0) res.Message = $"⚠ {nan} points hors intersection : contour légèrement partiel.";

            // --- développé du TUBE AJOUTÉ (sabot) ---
            if (coneBr) BuildSabotCone(res, p, rb, tana, tArr, bA, M, M2);
            else        BuildSabotCyl(res, p, rb, tArr, bA, M, M2);
            if (coneBr && res.DemiAngleDeg < 3.0)
                res.Message = (res.Message.Length > 0 ? res.Message + "\n" : "") +
                    $"ℹ Tube ajouté très peu conique (demi-angle {res.DemiAngleDeg:0.00}°) : développé en grand secteur quasi droit.";

            // --- développé du TROU sur le récepteur (secteur si conique) + Inter3D ---
            BuildTrou(res, Rmid, kr, X, Y, Z, M2);

            // --- 3D : anneau haut du tube ajouté ---
            BuildBranche3D(res, dep, sinth, costh, tArr, rb, tana, M2, p.LDroite);

            res.Ok = true; return res;
        }

        // Intersection : pour un angle b, paramètre axial t sur l'axe du tube ajouté tel que
        // le point de surface tombe sur le récepteur Y²+Z² = (Rmid + kr·X)².  Balayage + bissection.
        private static double SolveT(double b, double rb, double tana, double dep,
                                     double Rmid, double kr, double sinth, double costh,
                                     double Rbas, double Rhaut)
        {
            double sb = Math.Sin(b), cb = Math.Cos(b);
            double lBound = (Rbas + Rhaut + rb + Math.Abs(dep)) / Math.Max(0.08, Math.Abs(sinth)) + 2 * rb;
            double lo = -0.5 * rb - Math.Abs(dep), hi = lBound;
            int steps = 700;
            double G(double t)
            {
                double r = rb - t * tana;
                double Xc = t * costh - r * sb * sinth;
                double Yc = dep + r * cb;
                double Zc = t * sinth + r * sb * costh;
                double Rx = Rmid + kr * Xc;
                return Yc * Yc + Zc * Zc - Rx * Rx;
            }
            double pf = double.NaN, pl = double.NaN;
            for (int i = 0; i <= steps; i++)
            {
                double t = lo + (hi - lo) * i / steps; double f = G(t);
                if (!double.IsNaN(pf) && pf * f < 0)
                {
                    double a0 = pl, a1 = t;
                    for (int k = 0; k < 60; k++)
                    { double m = (a0 + a1) / 2; double fm = G(m); if (pf * fm <= 0) a1 = m; else a0 = m; }
                    return (a0 + a1) / 2;
                }
                pl = t; pf = f;
            }
            return double.NaN;
        }

        // --- développé tube ajouté CYLINDRIQUE : flan déroulé (sabot ondulé + haut droit) ---
        private static void BuildSabotCyl(PiquageResult res, PiquageParams p, double rb, double[] t, double[] bA, int M, int M2)
        {
            double tmin = double.MaxValue, tmax = double.MinValue;
            for (int i = 0; i <= M2; i++) if (!double.IsNaN(t[i])) { tmin = Math.Min(tmin, t[i]); tmax = Math.Max(tmax, t[i]); }
            if (tmax == double.MinValue) tmax = 0;
            double gmax = 0; var sabot = new List<Pt2>(M + 1);
            for (int i = 0; i <= M; i++) { double g = double.IsNaN(t[i]) ? 0 : tmax - t[i]; if (g < 0) g = 0; if (g > gmax) gmax = g; sabot.Add(new Pt2(rb * bA[i], g)); }
            double ytop = gmax + p.LDroite;
            res.BrancheSabot = sabot; res.BrancheLargeur = 2 * Math.PI * rb; res.BrancheHauteur = ytop;
            res.SabotMax = gmax; res.SabotMin = Math.Max(0, gmax - (tmax - tmin));
            var c = new List<Pt2>(M + 4) { new Pt2(0, ytop), new Pt2(sabot[M].X, ytop) };
            for (int i = M; i >= 0; i--) c.Add(sabot[i]);
            res.BrancheContour = c;
        }

        // --- développé tube ajouté CONIQUE : secteur de tronc de cône ---
        private static void BuildSabotCone(PiquageResult res, PiquageParams p, double rb, double tana, double[] t, double[] bA, int M, int M2)
        {
            double alpha = Math.Atan(Math.Abs(tana)); double sina = Math.Sin(alpha), cosa = Math.Cos(alpha);
            double lApex = rb / tana;
            res.DemiAngleDeg = alpha * 180.0 / Math.PI;
            double psi0 = bA[M / 2] * sina;
            double Lt = (lApex - p.LDroite) / cosa;
            var baseArc = new List<Pt2>(M + 1); var topArc = new List<Pt2>(M + 1);
            for (int i = 0; i <= M; i++)
            {
                double psi = bA[i] * sina;
                double Lg = double.IsNaN(t[i]) ? Lt : (lApex - t[i]) / cosa;
                baseArc.Add(new Pt2(Lg * Math.Cos(psi - psi0), Lg * Math.Sin(psi - psi0)));
                topArc.Add(new Pt2(Lt * Math.Cos(psi - psi0), Lt * Math.Sin(psi - psi0)));
            }
            var c = new List<Pt2>(2 * M + 2);
            for (int i = 0; i <= M; i++) c.Add(baseArc[i]);
            for (int i = M; i >= 0; i--) c.Add(topArc[i]);
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var q in c) { minX = Math.Min(minX, q.X); minY = Math.Min(minY, q.Y); maxX = Math.Max(maxX, q.X); maxY = Math.Max(maxY, q.Y); }
            void Shift(List<Pt2> L) { for (int i = 0; i < L.Count; i++) L[i] = new Pt2(L[i].X - minX, L[i].Y - minY); }
            Shift(c); Shift(baseArc); Shift(topArc);
            res.BrancheContour = c; res.BrancheSabot = baseArc; res.BrancheTop = topArc;
            res.BrancheLargeur = 2 * Math.PI * rb; res.BrancheHauteur = maxY - minY;
            res.SlantBase = lApex / cosa; res.SlantTop = Lt;
            double gmin = double.MaxValue, gmax = double.MinValue;
            for (int i = 0; i <= M2; i++) if (!double.IsNaN(t[i])) { double Lg = (lApex - t[i]) / cosa; gmin = Math.Min(gmin, Lg); gmax = Math.Max(gmax, Lg); }
            res.SabotMax = (gmax == double.MinValue) ? 0 : (gmax - gmin);
        }

        // --- développé du TROU sur le récepteur + remplissage Inter3D ---
        // récepteur cylindrique : déroulé plat (arc = R·ψ ; axe = X)
        // récepteur conique    : déroulé en SECTEUR (slant L = R(x)/sin αr ; angle = ψ·sin αr)
        private static void BuildTrou(PiquageResult res, double Rmid, double kr, double[] X, double[] Y, double[] Z, int M2)
        {
            double alpha = Math.Atan(Math.Abs(kr)), sa = Math.Sin(alpha);
            var trou = new List<Pt2>();
            double uMin = double.MaxValue, uMax = double.MinValue, vMin = double.MaxValue, vMax = double.MinValue;
            for (int i = 0; i <= M2 && i < X.Length; i++)
            {
                double x = X[i], y = Y[i], z = Z[i];
                res.Inter3D.Add(new Pt3(x, y, z));
                double psi = Math.Atan2(z, y); double Rx = Rmid + kr * x;
                double u, v;
                if (sa < 1e-6) { u = Rx * psi; v = x; }                       // cylindre : plat
                else { double L = Rx / sa; u = L * Math.Cos(psi * sa); v = L * Math.Sin(psi * sa); } // cône : secteur
                trou.Add(new Pt2(u, v));
                if (u < uMin) uMin = u; if (u > uMax) uMax = u; if (v < vMin) vMin = v; if (v > vMax) vMax = v;
            }
            double cx = (uMin + uMax) / 2, cy = (vMin + vMax) / 2;
            for (int i = 0; i < trou.Count; i++) trou[i] = new Pt2(trou[i].X - cx, trou[i].Y - cy);
            res.TrouContour = trou; res.TrouLargeur = uMax - uMin; res.TrouHauteur = vMax - vMin;
        }

        // Anneau HAUT du tube ajouté en 3D (repère récepteur), aligné par index sur Inter3D.
        private static void BuildBranche3D(PiquageResult res, double deport, double sinth, double costh,
                                           double[] axial, double rb, double tana, int M2, double LDroite)
        {
            res.BrancheTop3D = new List<Pt3>();
            if (res.Inter3D.Count == 0) return;
            double maxT = double.MinValue;
            for (int i = 0; i <= M2 && i < axial.Length; i++)
                if (!double.IsNaN(axial[i])) maxT = Math.Max(maxT, axial[i]);
            if (maxT == double.MinValue) return;
            double tTop = maxT + LDroite;
            double rTop = Math.Max(0.5, rb - tTop * tana);
            Pt3 Center(double t) => new Pt3(t * costh, deport, t * sinth);
            Pt3 ct = Center(tTop);
            int n = Math.Min(res.Inter3D.Count, M2 + 1);
            for (int i = 0; i < n; i++)
            {
                double ti = (i < axial.Length && !double.IsNaN(axial[i])) ? axial[i] : maxT;
                double ri = Math.Max(0.5, rb - ti * tana);
                var c = Center(ti); var s = res.Inter3D[i];
                double dx = (s.X - c.X) / ri, dy = (s.Y - c.Y) / ri, dz = (s.Z - c.Z) / ri;
                res.BrancheTop3D.Add(new Pt3(ct.X + rTop * dx, ct.Y + rTop * dy, ct.Z + rTop * dz));
            }
        }
    }
}
