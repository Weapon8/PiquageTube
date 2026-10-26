using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace PiquageTube
{
    public class MainForm : Form
    {
        // palette standard TolTem
        static readonly Color CBack   = Color.FromArgb(20, 24, 31);   // #14181F zone de dessin / fond
        static readonly Color CPanel  = Color.FromArgb(27, 32, 39);   // #1B2027 panneau gauche
        static readonly Color CInput  = Color.FromArgb(38, 44, 53);   // #262C35 champs
        static readonly Color CText   = Color.FromArgb(230, 235, 240);// #E6EBF0 texte
        static readonly Color CAccent = Color.FromArgb(255, 122, 26); // #FF7A1A orange accent
        static readonly Color CBtn    = Color.FromArgb(43, 49, 59);   // #2B313B bouton secondaire
        static readonly Color CInk    = Color.FromArgb(18, 21, 27);   // texte sombre sur orange
        static readonly Color CGrey   = Color.FromArgb(70, 78, 90);   // bordures / séparateurs
        static readonly Color CRed    = Color.FromArgb(150, 70, 70);  // suppression

        NumericUpDown nDRun, nDRunHaut, nLRun, nDBr, nDTop, nAngle, nDeport, nEp, nRec, nL, nSeg, nPosTrou, nCouture;
        CheckBox chkRun, chkCone, chkRunCone;
        ComboBox cbProfil;
        Button btnCalc, btnDxf, btnLoad, btnSave, btnDel;
        Button btnVBr, btnVTr, btnV3D;
        Label lblInfo, lblDTop, lblDRunHaut, lblLRun;
        PreviewPanel pBranche, pTrou, p3D;
        PiquageResult _res;
        ProfilStore _store;
        bool _suspend;   // gèle le recalcul temps réel pendant un chargement de profil

        public MainForm()
        {
            Text = "PiquageTube — TolTem";
            Width = 1200; Height = 780;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = CBack; ForeColor = CText;
            Font = new Font("Segoe UI", 9);
            TryLoadIcon();
            _store = ProfilStore.Load();
            BuildUi();
            RefreshProfils();
            Calc();
        }

        void TryLoadIcon()
        {
            try { using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("PiquageTube.toltem.ico"); if (s != null) Icon = new Icon(s); } catch { }
        }

        // -------- styling helpers --------
        NumericUpDown Num(double v, double min, double max, double inc, int dec)
        {
            var n = new NumericUpDown { Minimum = (decimal)min, Maximum = (decimal)max, Increment = (decimal)inc, DecimalPlaces = dec, Value = (decimal)v,
                BackColor = CInput, ForeColor = CText, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9) };
            n.ValueChanged += (s, e) => Calc();   // temps réel
            return n;
        }
        Button Btn(string t, int w, Color? bg = null, Color? fg = null)
        {
            var b = new Button { Text = t, Width = w, Height = 30, BackColor = bg ?? CBtn, ForeColor = fg ?? CText,
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            b.FlatAppearance.BorderSize = 1; b.FlatAppearance.BorderColor = CGrey;
            return b;
        }

        // bouton de barre de vue segmentée (pilule plate)
        Button MakeSeg(string txt, int left, int width)
        {
            var b = new Button { Text = txt, Left = left, Top = 6, Width = width, Height = 30,
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold), BackColor = CBtn, ForeColor = CText };
            b.FlatAppearance.BorderSize = 1; b.FlatAppearance.BorderColor = CGrey;
            return b;
        }

        void BuildUi()
        {
            var panel = new Panel { Dock = DockStyle.Left, Width = 332, Padding = new Padding(14), BackColor = CPanel, AutoScroll = true };
            Controls.Add(panel);
            var tip = new ToolTip();
            int y = 6;
            Label Lab(string t, int top, Color col, float sz = 9, FontStyle st = FontStyle.Regular)
            { var l = new Label { Text = t, Left = 0, Top = top, Width = 300, AutoSize = false, Height = 20, ForeColor = col, Font = new Font("Segoe UI", sz, st) }; panel.Controls.Add(l); return l; }
            void Row(string lbl, Control c) { var l = Lab(lbl, y + 4, CText); l.Width = 150; c.Left = 160; c.Top = y; c.Width = 145; panel.Controls.Add(c); y += 33; }

            Lab("PIQUAGE / CULOTTE", y, CAccent, 10.5f, FontStyle.Bold); y += 30;

            // --- profils JSON ---
            Lab("Profil standard", y, CAccent, 9, FontStyle.Bold); y += 22;
            cbProfil = new ComboBox { Left = 0, Top = y, Width = 305, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = CInput, ForeColor = CText, FlatStyle = FlatStyle.Flat };
            panel.Controls.Add(cbProfil); y += 30;
            btnLoad = Btn("Charger", 98); btnLoad.Left = 0; btnLoad.Top = y;
            btnSave = Btn("Enregistrer…", 110); btnSave.Left = 102; btnSave.Top = y;
            btnDel = Btn("Suppr.", 90, CRed, Color.White); btnDel.Left = 216; btnDel.Top = y;
            btnLoad.Click += (s, e) => LoadProfil();
            btnSave.Click += (s, e) => SaveProfil();
            btnDel.Click += (s, e) => DelProfil();
            panel.Controls.Add(btnLoad); panel.Controls.Add(btnSave); panel.Controls.Add(btnDel);
            y += 40;
            var sep = new Panel { Left = 0, Top = y, Width = 305, Height = 1, BackColor = CGrey }; panel.Controls.Add(sep); y += 12;

            // === TUBE RÉCEPTEUR (principal) = la DESCENTE ===
            Lab("TUBE RÉCEPTEUR (DESCENTE)", y, CAccent, 9, FontStyle.Bold); y += 22;
            nDRun = Num(200, 5, 5000, 1, 2); Row("Ø récepteur (bas)", nDRun);
            chkRunCone = new CheckBox { Text = "Récepteur conique (Ø bas ≠ haut)", Left = 0, Top = y, Width = 300, ForeColor = CText, Checked = false };
            chkRunCone.CheckedChanged += (s, e) => { UpdateConeState(); Calc(); };
            panel.Controls.Add(chkRunCone); y += 28;
            nDRunHaut = Num(150, 5, 5000, 1, 2); lblDRunHaut = Lab("Ø récepteur (haut)", y + 4, CText); lblDRunHaut.Width = 150;
            nDRunHaut.Left = 160; nDRunHaut.Top = y; nDRunHaut.Width = 145; panel.Controls.Add(nDRunHaut); y += 33;
            nLRun = Num(400, 10, 20000, 10, 0); lblLRun = Lab("Long. descente", y + 4, CText); lblLRun.Width = 150;
            nLRun.Left = 160; nLRun.Top = y; nLRun.Width = 145; panel.Controls.Add(nLRun); y += 33;
            nPosTrou = Num(0, 0, 20000, 10, 1); Row("Centre trou (0=milieu)", nPosTrou);
            tip.SetToolTip(nPosTrou, "Distance du CENTRE DU TROU au BAS de la descente (le bas = côté opposé à l'inclinaison du moignon). 0 = au milieu de la longueur.");
            nCouture = Num(180, 0, 360, 5, 0); Row("Couture (° / piquage)", nCouture);
            tip.SetToolTip(nCouture, "Position de la couture de la descente, en degrés autour du tube à partir de l'axe du piquage. 180 = à l'opposé.");
            var sep2 = new Panel { Left = 0, Top = y, Width = 305, Height = 1, BackColor = CGrey }; panel.Controls.Add(sep2); y += 12;

            // === TUBE AJOUTÉ (branche / manchon) = le MOIGNON ===
            Lab("TUBE AJOUTÉ (MOIGNON)", y, CAccent, 9, FontStyle.Bold); y += 22;
            nDBr = Num(100, 5, 5000, 1, 2); Row("Ø ajouté (base)", nDBr);
            chkCone = new CheckBox { Text = "Tube ajouté conique (réduction)", Left = 0, Top = y, Width = 300, ForeColor = CText, Checked = false };
            chkCone.CheckedChanged += (s, e) => { UpdateConeState(); Calc(); };
            panel.Controls.Add(chkCone); y += 28;
            nDTop = Num(95, 5, 5000, 1, 2); lblDTop = Lab("Ø ajouté (haut)", y + 4, CText); lblDTop.Width = 150;
            nDTop.Left = 160; nDTop.Top = y; nDTop.Width = 145; panel.Controls.Add(nDTop); y += 33;
            nAngle = Num(90, 2, 178, 1, 1); Row("Angle d'arrivée (°)", nAngle);
            nDeport = Num(0, -2000, 2000, 1, 2); Row("Déport latéral (mm)", nDeport);
            nL = Num(80, 1, 5000, 5, 1); Row("Long. tube ajouté", nL);
            var sep3 = new Panel { Left = 0, Top = y, Width = 305, Height = 1, BackColor = CGrey }; panel.Controls.Add(sep3); y += 12;

            // === COMMUN ===
            nEp = Num(0.8, 0.1, 10, 0.1, 2); Row("Épaisseur (mm)", nEp);
            nRec = Num(10, 0, 100, 1, 1); Row("Recouvrement (mm)", nRec);
            nSeg = Num(360, 60, 2000, 60, 0); Row("Segments", nSeg);

            chkRun = new CheckBox { Text = "Exporter aussi la descente (tube + trou)", Left = 0, Top = y, Width = 300, ForeColor = CText, Checked = true };
            panel.Controls.Add(chkRun); y += 34;

            btnCalc = Btn("Calculer / Aperçu", 305); btnCalc.Left = 0; btnCalc.Top = y; btnCalc.Height = 34; btnCalc.Click += (s, e) => Calc(); panel.Controls.Add(btnCalc); y += 42;
            btnDxf = Btn("⤓  Exporter les DXF", 305, CAccent, CInk); btnDxf.Left = 0; btnDxf.Top = y; btnDxf.Height = 36;
            btnDxf.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            btnDxf.FlatAppearance.BorderSize = 0;
            btnDxf.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 140, 55);
            btnDxf.FlatAppearance.MouseDownBackColor = Color.FromArgb(225, 100, 15);
            btnDxf.Click += (s, e) => ExportDxf(); panel.Controls.Add(btnDxf); y += 48;

            lblInfo = new Label { Left = 0, Top = y, Width = 305, Height = 190, ForeColor = Color.FromArgb(170, 200, 230), Font = new Font("Consolas", 8.5f) };
            panel.Controls.Add(lblInfo); y += 196;
            Lab("Made with love by weapon pour TolTem", y, Color.Gray, 8, FontStyle.Italic);

            // --- zone de dessin : barre de vue segmentée (haut) + aperçus empilés ---
            var rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = CBack };
            Controls.Add(rightPanel); rightPanel.BringToFront();

            var content = new Panel { Dock = DockStyle.Fill, BackColor = CBack };
            pBranche = new PreviewPanel { Dock = DockStyle.Fill };
            pTrou    = new PreviewPanel { Dock = DockStyle.Fill, Visible = false };
            p3D      = new PreviewPanel { Dock = DockStyle.Fill, Mode3D = true, Visible = false };
            content.Controls.Add(pBranche); content.Controls.Add(pTrou); content.Controls.Add(p3D);
            rightPanel.Controls.Add(content);

            var viewBar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = CBack };
            btnVBr = MakeSeg("Développé tube ajouté", 8,   160);
            btnVTr = MakeSeg("Trou récepteur",       172, 130);
            btnV3D = MakeSeg("Vue 3D",               306, 100);
            btnVBr.Click += (s, e) => ShowView(0);
            btnVTr.Click += (s, e) => ShowView(1);
            btnV3D.Click += (s, e) => ShowView(2);
            viewBar.Controls.Add(btnVBr); viewBar.Controls.Add(btnVTr); viewBar.Controls.Add(btnV3D);
            rightPanel.Controls.Add(viewBar); viewBar.BringToFront();

            UpdateConeState();
            ShowView(0);
        }

        // bascule de vue : l'onglet actif passe en orange plein
        void ShowView(int v)
        {
            pBranche.Visible = v == 0; pTrou.Visible = v == 1; p3D.Visible = v == 2;
            if (v == 0) pBranche.BringToFront(); else if (v == 1) pTrou.BringToFront(); else p3D.BringToFront();
            void St(Button b, bool on)
            {
                if (b == null) return;
                b.BackColor = on ? CAccent : CBtn; b.ForeColor = on ? CInk : CText;
                b.FlatAppearance.BorderColor = on ? CAccent : CGrey;
            }
            St(btnVBr, v == 0); St(btnVTr, v == 1); St(btnV3D, v == 2);
        }

        void UpdateConeState()
        {
            bool on = chkCone.Checked;
            nDTop.Enabled = on; lblDTop.ForeColor = on ? CText : Color.FromArgb(110, 110, 110);
            bool ron = chkRunCone.Checked;
            nDRunHaut.Enabled = ron; lblDRunHaut.ForeColor = ron ? CText : Color.FromArgb(110, 110, 110);
            // la longueur de la descente sert aussi en cylindrique (position du trou, longueur du flan exporté)
            nLRun.Enabled = true; lblLRun.ForeColor = CText;
        }

        // -------- profils --------
        void RefreshProfils()
        {
            cbProfil.Items.Clear();
            foreach (var p in _store.Profils) cbProfil.Items.Add(p);
            if (cbProfil.Items.Count > 0) cbProfil.SelectedIndex = 0;
        }
        void LoadProfil()
        {
            if (cbProfil.SelectedItem is not Profil p) return;
            _suspend = true;
            nDRun.Value = (decimal)p.DRun; chkRunCone.Checked = p.RunConique; nDRunHaut.Value = (decimal)p.DRunHaut; nLRun.Value = (decimal)p.LRun;
            nPosTrou.Value = (decimal)p.TrouDepuisBas; nCouture.Value = (decimal)p.CoutureDeg;
            nDBr.Value = (decimal)p.DBranche; chkCone.Checked = p.Conique;
            nDTop.Value = (decimal)p.DTop; nAngle.Value = (decimal)p.AngleDeg; nDeport.Value = (decimal)p.Deport;
            nEp.Value = (decimal)p.Epaisseur; nRec.Value = (decimal)p.Recouvr; nL.Value = (decimal)p.LDroite; nSeg.Value = p.Segments;
            _suspend = false;
            UpdateConeState(); Calc();
        }
        void SaveProfil()
        {
            string nom = Prompt("Nom du profil :", (cbProfil.SelectedItem as Profil)?.Nom ?? "Nouveau profil");
            if (string.IsNullOrWhiteSpace(nom)) return;
            var pr = new Profil { Nom = nom, DRun = (double)nDRun.Value, RunConique = chkRunCone.Checked, DRunHaut = (double)nDRunHaut.Value, LRun = (double)nLRun.Value,
                TrouDepuisBas = (double)nPosTrou.Value, CoutureDeg = (double)nCouture.Value,
                DBranche = (double)nDBr.Value, Conique = chkCone.Checked,
                DTop = (double)nDTop.Value, AngleDeg = (double)nAngle.Value, Deport = (double)nDeport.Value,
                Epaisseur = (double)nEp.Value, Recouvr = (double)nRec.Value, LDroite = (double)nL.Value, Segments = (int)nSeg.Value };
            int idx = _store.Profils.FindIndex(x => x.Nom == nom);
            if (idx >= 0) _store.Profils[idx] = pr; else _store.Profils.Add(pr);
            _store.Save(); RefreshProfils();
            cbProfil.SelectedIndex = _store.Profils.FindIndex(x => x.Nom == nom);
        }
        void DelProfil()
        {
            if (cbProfil.SelectedItem is not Profil p) return;
            if (MessageBox.Show($"Supprimer « {p.Nom} » ?", "PiquageTube", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _store.Profils.Remove(p); _store.Save(); RefreshProfils();
        }
        string Prompt(string text, string def)
        {
            using var f = new Form { Text = "PiquageTube", Width = 380, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, BackColor = CPanel, ForeColor = CText };
            var l = new Label { Text = text, Left = 12, Top = 12, Width = 340, ForeColor = CText };
            var tb = new TextBox { Left = 12, Top = 38, Width = 344, Text = def, BackColor = CInput, ForeColor = CText, BorderStyle = BorderStyle.FixedSingle };
            var ok = new Button { Text = "OK", Left = 196, Top = 72, Width = 75, DialogResult = DialogResult.OK, BackColor = CAccent, ForeColor = CInk, FlatStyle = FlatStyle.Flat };
            var ca = new Button { Text = "Annuler", Left = 281, Top = 72, Width = 75, DialogResult = DialogResult.Cancel, BackColor = CBtn, ForeColor = CText, FlatStyle = FlatStyle.Flat };
            f.Controls.AddRange(new Control[] { l, tb, ok, ca }); f.AcceptButton = ok; f.CancelButton = ca;
            return f.ShowDialog(this) == DialogResult.OK ? tb.Text.Trim() : null;
        }

        // -------- calcul --------
        PiquageParams ReadParams() => new PiquageParams
        { DRun = (double)nDRun.Value, RunConique = chkRunCone.Checked, DRunHaut = (double)nDRunHaut.Value, LRun = (double)nLRun.Value,
          DBranche = (double)nDBr.Value, Conique = chkCone.Checked, DTop = (double)nDTop.Value,
          AngleDeg = (double)nAngle.Value, Deport = (double)nDeport.Value, Epaisseur = (double)nEp.Value,
          Recouvr = (double)nRec.Value, LDroite = (double)nL.Value, Segments = (int)nSeg.Value,
          TrouDepuisBas = (double)nPosTrou.Value, CoutureDeg = (double)nCouture.Value };

        // le trou doit tenir dans la descente et ne pas être coupé par la couture
        string HoleWarning(PiquageParams p, PiquageResult r)
        {
            var w = new List<string>();
            double L = Math.Max(1, p.LRun);
            double lo = r.AxeBas + (r.TrouXMin - r.TrouXMid), hi = r.AxeBas + (r.TrouXMax - r.TrouXMid);
            if (lo < 0 || hi > L)
                w.Add($"⚠ Le trou sort de la descente : de {lo:0} à {hi:0} mm pour L={L:0}.");
            double phiS = p.CoutureDeg * Math.PI / 180.0;
            if (r.TrouPsiMin - Math.PI / 2 + phiS < 0 || r.TrouPsiMax - Math.PI / 2 + phiS > 2 * Math.PI)
                w.Add("⚠ Le trou chevauche la couture : change la position de la couture.");
            return string.Join("\n", w);
        }

        void Calc()
        {
            if (_suspend) return;
            var p = ReadParams();
            _res = Geometry.Compute(p);
            if (!_res.Ok)
            {
                lblInfo.Text = "ERREUR : " + _res.Message; btnDxf.Enabled = false;
                pBranche.SetData(null, false); pTrou.SetData(null, false); p3D.Set3D(null); return;
            }
            btnDxf.Enabled = true;
            var ci = CultureInfo.InvariantCulture;
            string s = $"Tube ajouté : L={_res.BrancheLargeur.ToString("0.0", ci)} mm  (+rec {p.Recouvr:0})\n" +
                       $"Hauteur flan : {_res.BrancheHauteur.ToString("0.0", ci)} mm\n";
            if (p.RunConique) s += $"Récepteur conique : Ø{p.DRun:0}→{p.DRunHaut:0}  (trou en secteur)\n";
            if (_res.IsCone) s += $"Ajouté CÔNE demi-angle {_res.DemiAngleDeg.ToString("0.000", ci)}°\n  slant base {_res.SlantBase:0} / haut {_res.SlantTop:0}\n";
            s += $"Sabot : {_res.SabotMax.ToString("0.0", ci)} mm\n" +
                 $"Trou récepteur : {_res.TrouLargeur.ToString("0.0", ci)} x {_res.TrouHauteur.ToString("0.0", ci)} mm\n" +
                 $"Descente : centre du trou à {_res.AxeBas.ToString("0.#", ci)} mm du bas{(p.TrouDepuisBas <= 0 ? " (milieu)" : "")}, couture à {p.CoutureDeg:0}°\n";
            string hw = HoleWarning(p, _res);
            if (hw.Length > 0) s += "\n" + hw + "\n";
            if (!string.IsNullOrEmpty(_res.Message)) s += "\n" + _res.Message;
            lblInfo.Text = s;
            pBranche.SetData(_res.BrancheContour, true, _res.BrancheSabot, _res.IsCone ? _res.BrancheTop : null);
            pTrou.SetData(_res.TrouContour, true);
            p3D.Set3D(_res);
        }

        // -------- DXF --------
        // contour de découpe -> chaîne d'arcs tangents (grignotage continu)
        static DxfPolyline CutPoly(IReadOnlyList<Pt2> pts, bool closed)
        {
            ArcFit.FitPolyline(pts, closed, out var v, out var b);
            return new DxfPolyline { Layer = "DECOUPE", Closed = closed, Points = v, Bulges = b };
        }

        void ExportDxf()
        {
            if (_res == null || !_res.Ok) return;
            var p = ReadParams();
            var ci = CultureInfo.InvariantCulture;
            using var sfd = new SaveFileDialog
            {
                Filter = "DXF (*.dxf)|*.dxf",
                Title = "Nom de base : _MOIGNON.dxf et _DESCENTE.dxf (avec le trou) seront créés",
                FileName = $"PIQUAGE_D{p.DRun:0}_D{p.DBranche:0}_{p.AngleDeg:0}deg{(p.Conique ? "_cone" : "")}.dxf"
            };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            string dir = Path.GetDirectoryName(sfd.FileName) ?? "";
            string stem = Path.GetFileNameWithoutExtension(sfd.FileName);
            string fMoi = Path.Combine(dir, stem + "_MOIGNON.dxf");
            string fDes = Path.Combine(dir, stem + "_DESCENTE.dxf");
            bool withDes = chkRun.Checked;
            if (File.Exists(fMoi) || (withDes && File.Exists(fDes)))
            {
                if (MessageBox.Show("Un ou plusieurs fichiers existent déjà. Les remplacer ?", "PiquageTube",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            }

            string info = _res.IsCone
                ? $"PIQUAGE CONE D{p.DRun:0} / branche {p.DBranche:0}->{p.DTop:0} {p.AngleDeg:0}deg dep{p.Deport:0} ep{p.Epaisseur.ToString("0.##", ci)} rec{p.Recouvr:0} fibre neutre"
                : $"PIQUAGE D{p.DRun:0}/D{p.DBranche:0} {p.AngleDeg:0}deg dep{p.Deport:0} ep{p.Epaisseur.ToString("0.##", ci)} rec{p.Recouvr:0} fibre neutre";

            try
            {
                // ---------- pièce 1 : MOIGNON (le bout rapporté) — rien d'autre que lui ----------
                var moi = new List<DxfEntity>();
                moi.Add(CutPoly(_res.BrancheContour, true));
                moi.Add(new DxfPolyline { Layer = "TRACAGE", Closed = false, Points = _res.BrancheSabot });
                if (_res.IsCone && _res.BrancheTop.Count > 1) moi.Add(new DxfPolyline { Layer = "TRACAGE", Closed = false, Points = _res.BrancheTop });
                else { double W = _res.BrancheLargeur; moi.Add(new DxfLine { Layer = "TRACAGE", X1 = W, Y1 = 0, X2 = W, Y2 = _res.BrancheHauteur }); }
                moi.Add(new DxfText { Layer = "REPERE", X = 4, Y = _res.BrancheHauteur + 6, Height = 6, Value = "HAUT" });
                moi.Add(new DxfText { Layer = "INFOS", X = 0, Y = -14, Height = 5, Value = info });
                moi.Add(new DxfText { Layer = "INFOS", X = 0, Y = -24, Height = 4, Value = "MOIGNON - Made with love by weapon pour TolTem" });
                File.WriteAllText(fMoi, new DxfWriter().Build(moi));

                // ---------- pièce 2 : DESCENTE (le tube de base) avec le trou à sa vraie place ----------
                string warn = HoleWarning(p, _res);
                if (withDes)
                {
                    var des = BuildDescente(p, info);
                    File.WriteAllText(fDes, new DxfWriter().Build(des));
                }

                string msg = "DXF exportés :\n" + fMoi + (withDes ? "\n" + fDes : "");
                if (withDes && warn.Length > 0) msg += "\n\n" + warn;
                MessageBox.Show(msg, "PiquageTube", MessageBoxButtons.OK, warn.Length > 0 && withDes ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show("Erreur export : " + ex.Message, "PiquageTube", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // Descente = tube récepteur déroulé (rectangle si cylindre, secteur si cône) + trou du piquage
        List<DxfEntity> BuildDescente(PiquageParams p, string info)
        {
            var ents = new List<DxfEntity>();
            var ci = CultureInfo.InvariantCulture;
            double L = Math.Max(1, p.LRun);
            double phiS = p.CoutureDeg * Math.PI / 180.0;       // couture : angle depuis l'axe du piquage
            double ep = p.Epaisseur;
            double Rb = (p.DRun - ep) / 2, Rh = p.RunConique ? (p.DRunHaut - ep) / 2 : Rb;
            double kr = _res.RunKr, sa = Math.Sin(Math.Atan(Math.Abs(kr)));
            bool cone = p.RunConique && sa > 1e-4;

            if (!cone)
            {
                // ----- cylindre : flan rectangulaire, x = arc depuis la couture, y = depuis le bas -----
                double R = Rb, W = 2 * Math.PI * R, lap = p.Recouvr;
                double yAx = _res.AxeBas, xAx = R * phiS;       // centre du trou sur le flan
                ents.Add(CutPoly(new List<Pt2> { new(0, 0), new(W + lap, 0), new(W + lap, L), new(0, L) }, true));

                var hole = new List<Pt2>(_res.TrouParam.Count);
                foreach (var q in _res.TrouParam)                // q = (psi, x, R local)
                    hole.Add(new Pt2(q.Z * (q.X - Math.PI / 2) + xAx, yAx + (q.Y - _res.TrouXMid)));
                ents.Add(CutPoly(hole, true));

                ents.Add(new DxfLine { Layer = "TRACAGE", X1 = W, Y1 = 0, X2 = W, Y2 = L });                         // fin du tube nominal
                ents.Add(new DxfLine { Layer = "TRACAGE", X1 = 0, Y1 = yAx, X2 = W, Y2 = yAx });                      // ligne passant par le centre du trou
                ents.Add(new DxfLine { Layer = "TRACAGE", X1 = xAx, Y1 = yAx - 10, X2 = xAx, Y2 = yAx + 10 });       // croix au centre du trou
                ents.Add(new DxfText { Layer = "REPERE", X = W + lap + 8, Y = L - 6, Height = 6, Value = "HAUT" });
                ents.Add(new DxfText { Layer = "REPERE", X = W + lap + 8, Y = 0, Height = 6, Value = "BAS" });
                ents.Add(new DxfText { Layer = "INFOS", X = 0, Y = -14, Height = 5,
                    Value = $"DESCENTE D{p.DRun:0} L{L:0} ep{ep.ToString("0.##", ci)} rec{lap:0} dev{W:0.0} - centre trou a {yAx:0.#} du bas - couture a {p.CoutureDeg:0}deg" });
                ents.Add(new DxfText { Layer = "INFOS", X = 0, Y = -24, Height = 4, Value = "DESCENTE + trou piquage D" + p.DBranche.ToString("0") + " " + p.AngleDeg.ToString("0") + "deg - Made with love by weapon pour TolTem" });
            }
            else
            {
                // ----- cône : secteur annulaire, apex = repère ; angle = psi·sin(alpha) ; rayon = R(x)/sin(alpha) -----
                double Lb = Rb / sa, Lh = Rh / sa, Lmin = Math.Min(Lb, Lh), Lmax = Math.Max(Lb, Lh);
                double span = 2 * Math.PI * sa, lapAng = p.Recouvr / Lmax;
                double a0 = Math.PI / 2 - phiS * sa;            // couture
                double a1 = a0 + span + lapAng;
                int ns = Math.Max(24, p.Segments / 6);
                Pt2 At(double Lr, double a) => new(Lr * Math.Cos(a), -Lr * Math.Sin(a));   // apex en (0,0), y vers le bas du dessin

                var sec = new List<Pt2>();
                for (int i = 0; i <= ns; i++) sec.Add(At(Lmax, a0 + (a1 - a0) * i / ns));
                for (int i = ns; i >= 0; i--) sec.Add(At(Lmin, a0 + (a1 - a0) * i / ns));

                var hole = new List<Pt2>(_res.TrouParam.Count);
                foreach (var q in _res.TrouParam)                // q = (psi, x, R local)
                    hole.Add(At(q.Z / sa, Math.PI / 2 + (q.X - Math.PI / 2) * sa));

                // recalage : tout en x,y >= 0
                double minX = double.MaxValue, minY = double.MaxValue;
                foreach (var q in sec) { minX = Math.Min(minX, q.X); minY = Math.Min(minY, q.Y); }
                Pt2 Sh(Pt2 q) => new(q.X - minX, q.Y - minY);
                var secS = sec.ConvertAll(Sh); var holeS = hole.ConvertAll(Sh);
                ents.Add(CutPoly(secS, true));
                ents.Add(CutPoly(holeS, true));

                var s0 = Sh(At(Lmin, a0 + span)); var s1 = Sh(At(Lmax, a0 + span));                 // fin du tube nominal
                ents.Add(new DxfLine { Layer = "TRACAGE", X1 = s0.X, Y1 = s0.Y, X2 = s1.X, Y2 = s1.Y });
                var ax = Sh(At((_res.R + kr * _res.TrouXMid) / sa, Math.PI / 2));                      // centre du trou
                ents.Add(new DxfLine { Layer = "TRACAGE", X1 = ax.X - 10, Y1 = ax.Y, X2 = ax.X + 10, Y2 = ax.Y });
                ents.Add(new DxfLine { Layer = "TRACAGE", X1 = ax.X, Y1 = ax.Y - 10, X2 = ax.X, Y2 = ax.Y + 10 });
                ents.Add(new DxfText { Layer = "INFOS", X = 0, Y = -14, Height = 5,
                    Value = $"DESCENTE CONIQUE D{p.DRun:0}->{p.DRunHaut:0} L{L:0} ep{ep.ToString("0.##", ci)} rec{p.Recouvr:0} - centre trou a {_res.AxeBas:0.#} du bas (D{p.DRun:0}) - couture a {p.CoutureDeg:0}deg" });
                ents.Add(new DxfText { Layer = "INFOS", X = 0, Y = -24, Height = 4, Value = "DESCENTE + trou piquage D" + p.DBranche.ToString("0") + " " + p.AngleDeg.ToString("0") + "deg - Made with love by weapon pour TolTem" });
            }
            return ents;
        }
    }

    // =================== PREVIEW ===================
    public class PreviewPanel : Panel
    {
        static readonly Color CBg = Color.FromArgb(20, 24, 31);        // #14181F canvas TolTem
        static readonly Color CGrid = Color.FromArgb(40, 46, 56);
        static readonly Color CCut = Color.FromArgb(40, 210, 220);     // cyan = découpe
        static readonly Color CTrace = Color.FromArgb(255, 168, 77);   // orange = sabot/tracage
        static readonly Color CTop = Color.FromArgb(110, 220, 120);    // vert = arc haut cône
        static readonly Color CRun3D = Color.FromArgb(90, 100, 115);
        static readonly Color CInter = Color.FromArgb(255, 92, 92);    // rouge = intersection
        static readonly Color CDim = Color.FromArgb(165, 185, 210);    // cotes encombrement

        List<Pt2> _contour, _sabot, _top; bool _closed;
        public bool Mode3D = false; PiquageResult _r3d;

        // --- rotation / zoom de la vue 3D (souris) ---
        const double Az0 = 0.7, El0 = 0.5;
        double _az = Az0, _el = El0, _zoom = 1.0;
        bool _drag; Point _lastMouse;

        public PreviewPanel()
        {
            DoubleBuffered = true; ResizeRedraw = true; BackColor = CBg;
            MouseDown += (s, e) => { if (Mode3D && e.Button == MouseButtons.Left) { _drag = true; _lastMouse = e.Location; Cursor = Cursors.SizeAll; } };
            MouseMove += (s, e) =>
            {
                if (!Mode3D || !_drag) return;
                _az += (e.X - _lastMouse.X) * 0.01;
                _el = Math.Max(-1.5, Math.Min(1.5, _el + (e.Y - _lastMouse.Y) * 0.01));
                _lastMouse = e.Location; Invalidate();
            };
            MouseUp += (s, e) => { _drag = false; Cursor = Cursors.Default; };
            MouseDoubleClick += (s, e) => { if (Mode3D) { _az = Az0; _el = El0; _zoom = 1.0; Invalidate(); } };   // double-clic = vue d'origine
            MouseEnter += (s, e) => { if (Mode3D) Focus(); };                                                    // pour recevoir la molette
            MouseWheel += (s, e) => { if (!Mode3D) return; _zoom = Math.Max(0.3, Math.Min(6.0, _zoom * (e.Delta > 0 ? 1.12 : 1 / 1.12))); Invalidate(); };
        }
        public void SetData(List<Pt2> c, bool closed, List<Pt2> sabot = null, List<Pt2> top = null) { _contour = c; _closed = closed; _sabot = sabot; _top = top; _r3d = null; Invalidate(); }
        public void Set3D(PiquageResult r) { _r3d = r; _contour = null; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(CBg);
            if (Mode3D) { Draw3D(g); return; }
            if (_contour == null || _contour.Count < 2) { Empty(g); return; }

            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            void Acc(List<Pt2> L) { if (L == null) return; foreach (var p in L) { minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X); minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y); } }
            Acc(_contour);
            double w = maxX - minX, h = maxY - minY; if (w <= 0) w = 1; if (h <= 0) h = 1;
            int m = 55; double s = Math.Min((Width - 2 * m) / w, (Height - 2 * m) / h);
            double ox = m + (Width - 2 * m - w * s) / 2, oy = m + (Height - 2 * m - h * s) / 2;
            PointF Tr(Pt2 p) => new((float)(ox + (p.X - minX) * s), (float)(Height - (oy + (p.Y - minY) * s)));

            using (var gr = new Pen(CGrid))
                for (int i = 0; i <= 10; i++)
                {
                    float gx = (float)(m + i * (Width - 2 * m) / 10.0), gy = (float)(m + i * (Height - 2 * m) / 10.0);
                    g.DrawLine(gr, gx, m, gx, Height - m); g.DrawLine(gr, m, gy, Width - m, gy);
                }

            void Poly(List<Pt2> L, Pen pen, bool closed) { if (L == null || L.Count < 2) return; var a = new PointF[L.Count]; for (int i = 0; i < L.Count; i++) a[i] = Tr(L[i]); if (closed) g.DrawPolygon(pen, a); else g.DrawLines(pen, a); }
            using (var pt = new Pen(CTrace, 1.3f) { DashStyle = DashStyle.Dash }) Poly(_sabot, pt, false);
            using (var pTop = new Pen(CTop, 1.3f) { DashStyle = DashStyle.Dash }) Poly(_top, pTop, false);
            using (var pc = new Pen(CCut, 2.2f)) Poly(_contour, pc, _closed);

            // --- cotes d'encombrement (recouvrement inclus) ---
            float xL = (float)ox, xR = (float)(ox + w * s);
            float yT = (float)(Height - (oy + h * s)), yB = (float)(Height - oy);
            using (var pd = new Pen(CDim, 1f))
            using (var bd = new SolidBrush(CDim))
            using (var fd = new Font("Segoe UI", 8f))
            {
                float yl = yB + 18;                                  // largeur en bas
                g.DrawLine(pd, xL, yl, xR, yl);
                g.DrawLine(pd, xL, yl - 4, xL, yl + 4); g.DrawLine(pd, xR, yl - 4, xR, yl + 4);
                string sw = $"{w:0} mm"; var zw = g.MeasureString(sw, fd);
                g.DrawString(sw, fd, bd, (xL + xR) / 2 - zw.Width / 2, yl + 2);

                float xr = xR + 18;                                  // hauteur à droite
                g.DrawLine(pd, xr, yT, xr, yB);
                g.DrawLine(pd, xr - 4, yT, xr + 4, yT); g.DrawLine(pd, xr - 4, yB, xr + 4, yB);
                string sh = $"{h:0} mm"; var zh = g.MeasureString(sh, fd);
                var st = g.Save(); g.TranslateTransform(xr + 6, (yT + yB) / 2); g.RotateTransform(-90);
                g.DrawString(sh, fd, bd, -zh.Width / 2, 0); g.Restore(st);
            }

            using var f = new Font("Segoe UI", 9);
            g.DrawString($"Encombrement {w:0} × {h:0} mm   ({w * h / 1e6:0.00} m²)", f, new SolidBrush(CDim), m, 10);
            g.DrawString("cyan = découpe   orange = sabot   gris = cotes" + (_top != null ? "   vert = haut cône" : ""), f, new SolidBrush(Color.FromArgb(140, 150, 160)), m, Height - 26);
        }

        void Draw3D(Graphics g)
        {
            if (_r3d == null || !_r3d.Ok) { Empty(g); return; }
            // k : 0 = tube récepteur (gris), 1 = sabot/intersection (rouge), 2 = tube ajouté (orange)
            var segs = new List<(Pt3 a, Pt3 b, int k)>();
            double R = _r3d.R; double kr = _r3d.RunKr;
            double xmin = -_r3d.AxeCross, xmax = xmin + Math.Max(1, _r3d.RunLength);   // descente à sa vraie longueur
            double xm = (xmin + xmax) / 2;
            double Rx(double x) => Math.Max(1.0, R + kr * x);
            for (double x = xmin; x <= xmax + 1e-6; x += (xmax - xmin) / 6)
            { Pt3 prev = default; bool first = true; for (int i = 0; i <= 48; i++) { double a = 2 * Math.PI * i / 48; var p = new Pt3(x, Rx(x) * Math.Cos(a), Rx(x) * Math.Sin(a)); if (!first) segs.Add((prev, p, 0)); prev = p; first = false; } }
            for (int i = 0; i < 12; i++) { double a = 2 * Math.PI * i / 12; segs.Add((new Pt3(xmin, Rx(xmin) * Math.Cos(a), Rx(xmin) * Math.Sin(a)), new Pt3(xmax, Rx(xmax) * Math.Cos(a), Rx(xmax) * Math.Sin(a)), 0)); }

            // courbe d'intersection (sabot)
            for (int i = 1; i < _r3d.Inter3D.Count; i++) segs.Add((_r3d.Inter3D[i - 1], _r3d.Inter3D[i], 1));

            // tube de descente : sabot (bas) -> génératrices -> anneau haut
            var sad = _r3d.Inter3D; var top = _r3d.BrancheTop3D;
            if (top != null && top.Count > 1)
            {
                for (int i = 1; i < top.Count; i++) segs.Add((top[i - 1], top[i], 2));          // anneau haut
                int cnt = Math.Min(sad.Count, top.Count); int stp = Math.Max(1, cnt / 28);
                for (int i = 0; i < cnt; i += stp) segs.Add((sad[i], top[i], 2));                // génératrices
            }

            double az = _az, el = _el, ca = Math.Cos(az), sa = Math.Sin(az), ce = Math.Cos(el), se = Math.Sin(el);
            PointF P(Pt3 p) { double X = p.X * ca - p.Y * sa; double Y = p.X * sa * se + p.Y * ca * se + p.Z * ce; return new PointF((float)X, (float)Y); }
            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            foreach (var (a, b, _) in segs) { var qa = P(a); var qb = P(b); minX = Math.Min(minX, Math.Min(qa.X, qb.X)); maxX = Math.Max(maxX, Math.Max(qa.X, qb.X)); minY = Math.Min(minY, Math.Min(qa.Y, qb.Y)); maxY = Math.Max(maxY, Math.Max(qa.Y, qb.Y)); }
            double w = maxX - minX, h = maxY - minY; if (w <= 0) w = 1; if (h <= 0) h = 1;
            int m = 50; double s = Math.Min((Width - 2 * m) / w, (Height - 2 * m) / h) * _zoom;
            double ox = m + (Width - 2 * m - w * s) / 2, oy = m + (Height - 2 * m - h * s) / 2;
            PointF Map(Pt3 p) { var q = P(p); return new PointF((float)(ox + (q.X - minX) * s), (float)(Height - (oy + (q.Y - minY) * s))); }

            using var pr = new Pen(CRun3D, 1f);
            using var pi = new Pen(CInter, 2.4f);
            using var pb = new Pen(CTrace, 1.7f);   // orange = branche
            foreach (var (a, b, k) in segs) g.DrawLine(k == 1 ? pi : k == 2 ? pb : pr, Map(a), Map(b));

            // --- cotes 3D (Ø récepteur, Ø tube ajouté, angle) ---
            using var pcote = new Pen(CDim, 1.3f);
            using var paxis = new Pen(CDim, 1f) { DashStyle = DashStyle.Dash };
            using var bcote = new SolidBrush(CDim);
            using var fcote = new Font("Segoe UI", 9, FontStyle.Bold);
            Pt3 Cen(List<Pt3> L) { double cx = 0, cy = 0, cz = 0; int nn = Math.Max(1, L.Count); foreach (var q in L) { cx += q.X; cy += q.Y; cz += q.Z; } return new Pt3(cx / nn, cy / nn, cz / nn); }

            // axes + angle
            g.DrawLine(paxis, Map(new Pt3(xmin, 0, 0)), Map(new Pt3(xmax, 0, 0)));   // axe récepteur
            if (sad.Count > 0 && top != null && top.Count > 0)
            {
                var sc = Cen(sad); var tc = Cen(top);
                var aEnd = new Pt3(sc.X + (tc.X - sc.X) * 1.15, sc.Y + (tc.Y - sc.Y) * 1.15, sc.Z + (tc.Z - sc.Z) * 1.15);
                g.DrawLine(paxis, Map(sc), Map(aEnd));                               // axe tube ajouté
                var pv = Map(sc);
                g.DrawString($"{_r3d.NomAngle:0}°", fcote, bcote, pv.X + 6, pv.Y - 6);
            }
            // Ø récepteur : diamètre sur l'anneau central
            {
                PointF a1 = Map(new Pt3(xm, 0, Rx(xm))), a2 = Map(new Pt3(xm, 0, -Rx(xm)));
                g.DrawLine(pcote, a1, a2);
                string rl = _r3d.RunIsCone ? $"Ø{_r3d.NomDRun:0}→{_r3d.NomDRunHaut:0}" : $"Ø{_r3d.NomDRun:0}";
                g.DrawString(rl, fcote, bcote, (a1.X + a2.X) / 2 + 6, (a1.Y + a2.Y) / 2);
            }
            // Ø tube ajouté : diamètre sur l'anneau haut
            if (top != null && top.Count > 3)
            {
                PointF b1 = Map(top[0]), b2 = Map(top[top.Count / 2]);
                g.DrawLine(pcote, b1, b2);
                string tl = _r3d.IsCone ? $"Ø{_r3d.NomDTop:0}" : $"Ø{_r3d.NomDBr:0}";
                g.DrawString(tl, fcote, bcote, (b1.X + b2.X) / 2 + 6, (b1.Y + b2.Y) / 2 - 14);
            }

            using var f = new Font("Segoe UI", 9);
            g.DrawString("gris = tube récepteur   orange = tube ajouté   rouge = piquage   cotes = Ø / angle",
                f, new SolidBrush(Color.FromArgb(150, 160, 170)), m, 10);
            g.DrawString("glisser = tourner   molette = zoom   double-clic = vue d'origine",
                f, new SolidBrush(Color.FromArgb(120, 130, 140)), m, Height - 26);
        }

        void Empty(Graphics g)
        {
            using var f = new Font("Segoe UI", 11);
            var sz = g.MeasureString("Aucun aperçu", f);
            g.DrawString("Aucun aperçu", f, new SolidBrush(Color.FromArgb(90, 90, 90)), (Width - sz.Width) / 2, (Height - sz.Height) / 2);
        }
    }
}
