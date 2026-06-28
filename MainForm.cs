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

        NumericUpDown nDRun, nDRunHaut, nLRun, nDBr, nDTop, nAngle, nDeport, nEp, nRec, nL, nSeg;
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

            // === TUBE RÉCEPTEUR (principal) ===
            Lab("TUBE RÉCEPTEUR", y, CAccent, 9, FontStyle.Bold); y += 22;
            nDRun = Num(200, 5, 5000, 1, 2); Row("Ø récepteur (bas)", nDRun);
            chkRunCone = new CheckBox { Text = "Récepteur conique (Ø bas ≠ haut)", Left = 0, Top = y, Width = 300, ForeColor = CText, Checked = false };
            chkRunCone.CheckedChanged += (s, e) => { UpdateConeState(); Calc(); };
            panel.Controls.Add(chkRunCone); y += 28;
            nDRunHaut = Num(150, 5, 5000, 1, 2); lblDRunHaut = Lab("Ø récepteur (haut)", y + 4, CText); lblDRunHaut.Width = 150;
            nDRunHaut.Left = 160; nDRunHaut.Top = y; nDRunHaut.Width = 145; panel.Controls.Add(nDRunHaut); y += 33;
            nLRun = Num(400, 10, 20000, 10, 0); lblLRun = Lab("Long. récepteur", y + 4, CText); lblLRun.Width = 150;
            nLRun.Left = 160; nLRun.Top = y; nLRun.Width = 145; panel.Controls.Add(nLRun); y += 33;
            var sep2 = new Panel { Left = 0, Top = y, Width = 305, Height = 1, BackColor = CGrey }; panel.Controls.Add(sep2); y += 12;

            // === TUBE AJOUTÉ (branche / manchon) ===
            Lab("TUBE AJOUTÉ", y, CAccent, 9, FontStyle.Bold); y += 22;
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

            chkRun = new CheckBox { Text = "Exporter aussi le tube récepteur", Left = 0, Top = y, Width = 300, ForeColor = CText, Checked = false };
            panel.Controls.Add(chkRun); y += 34;

            btnCalc = Btn("Calculer / Aperçu", 305); btnCalc.Left = 0; btnCalc.Top = y; btnCalc.Height = 34; btnCalc.Click += (s, e) => Calc(); panel.Controls.Add(btnCalc); y += 42;
            btnDxf = Btn("⤓  Exporter le DXF", 305, CAccent, CInk); btnDxf.Left = 0; btnDxf.Top = y; btnDxf.Height = 36;
            btnDxf.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            btnDxf.FlatAppearance.BorderSize = 0;
            btnDxf.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 140, 55);
            btnDxf.FlatAppearance.MouseDownBackColor = Color.FromArgb(225, 100, 15);
            btnDxf.Click += (s, e) => ExportDxf(); panel.Controls.Add(btnDxf); y += 48;

            lblInfo = new Label { Left = 0, Top = y, Width = 305, Height = 150, ForeColor = Color.FromArgb(170, 200, 230), Font = new Font("Consolas", 8.5f) };
            panel.Controls.Add(lblInfo); y += 156;
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
            nLRun.Enabled = ron; lblLRun.ForeColor = ron ? CText : Color.FromArgb(110, 110, 110);
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
          Recouvr = (double)nRec.Value, LDroite = (double)nL.Value, Segments = (int)nSeg.Value };

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
                 $"Trou récepteur : {_res.TrouLargeur.ToString("0.0", ci)} x {_res.TrouHauteur.ToString("0.0", ci)} mm\n";
            if (!string.IsNullOrEmpty(_res.Message)) s += "\n" + _res.Message;
            lblInfo.Text = s;
            pBranche.SetData(_res.BrancheContour, true, _res.BrancheSabot, _res.IsCone ? _res.BrancheTop : null);
            pTrou.SetData(_res.TrouContour, true);
            p3D.Set3D(_res);
        }

        // -------- DXF --------
        void ExportDxf()
        {
            if (_res == null || !_res.Ok) return;
            var p = ReadParams();
            using var sfd = new SaveFileDialog { Filter = "DXF (*.dxf)|*.dxf", FileName = $"PIQUAGE_D{p.DRun:0}_D{p.DBranche:0}_{p.AngleDeg:0}deg{(p.Conique ? "_cone" : "")}.dxf" };
            if (sfd.ShowDialog() != DialogResult.OK) return;
            var ci = CultureInfo.InvariantCulture;
            var ents = new List<DxfEntity>();
            ents.Add(new DxfPolyline { Layer = "DECOUPE", Closed = true, Points = _res.BrancheContour });
            ents.Add(new DxfPolyline { Layer = "TRACAGE", Closed = false, Points = _res.BrancheSabot });
            if (_res.IsCone && _res.BrancheTop.Count > 1) ents.Add(new DxfPolyline { Layer = "TRACAGE", Closed = false, Points = _res.BrancheTop });
            else { double W = _res.BrancheLargeur; ents.Add(new DxfLine { Layer = "TRACAGE", X1 = W, Y1 = 0, X2 = W, Y2 = _res.BrancheHauteur }); }
            ents.Add(new DxfText { Layer = "REPERE", X = 4, Y = _res.BrancheHauteur + 6, Height = 6, Value = "HAUT" });
            string info = _res.IsCone
                ? $"PIQUAGE CONE D{p.DRun:0} / branche {p.DBranche:0}->{p.DTop:0} {p.AngleDeg:0}deg dep{p.Deport:0} ep{p.Epaisseur.ToString("0.##", ci)} rec{p.Recouvr:0} fibre neutre"
                : $"PIQUAGE D{p.DRun:0}/D{p.DBranche:0} {p.AngleDeg:0}deg dep{p.Deport:0} ep{p.Epaisseur.ToString("0.##", ci)} rec{p.Recouvr:0} fibre neutre";
            ents.Add(new DxfText { Layer = "INFOS", X = 0, Y = -14, Height = 5, Value = info });
            ents.Add(new DxfText { Layer = "INFOS", X = 0, Y = -24, Height = 4, Value = "BRANCHE - Made with love by weapon pour TolTem" });

            // trou décalé à droite
            double offX = _res.BrancheLargeur + p.Recouvr + 120, offY = _res.BrancheHauteur / 2;
            var trou = new List<Pt2>(_res.TrouContour.Count);
            foreach (var pt in _res.TrouContour) trou.Add(new Pt2(pt.X + offX, pt.Y + offY));
            ents.Add(new DxfPolyline { Layer = "DECOUPE", Closed = true, Points = trou });
            ents.Add(new DxfText { Layer = "INFOS", X = offX - _res.TrouLargeur / 2, Y = offY - _res.TrouHauteur / 2 - 14, Height = 5, Value = "TROU SUR TUBE DROIT" });
            ents.Add(new DxfLine { Layer = "TRACAGE", X1 = offX - 10, Y1 = offY, X2 = offX + 10, Y2 = offY });
            ents.Add(new DxfLine { Layer = "TRACAGE", X1 = offX, Y1 = offY - 10, X2 = offX, Y2 = offY + 10 });

            if (chkRun.Checked && !p.RunConique)
            {
                double runLen = Math.Max(150, _res.TrouHauteur + 80), runDev = Math.PI * (p.DRun - p.Epaisseur);
                double rx = offX - runDev / 2, ry = offY + _res.TrouHauteur / 2 + 90;
                ents.Add(new DxfPolyline { Layer = "DECOUPE", Closed = true, Points = new List<Pt2> { new(rx, ry), new(rx + runDev + p.Recouvr, ry), new(rx + runDev + p.Recouvr, ry + runLen), new(rx, ry + runLen) } });
                ents.Add(new DxfLine { Layer = "TRACAGE", X1 = rx + runDev, Y1 = ry, X2 = rx + runDev, Y2 = ry + runLen });
                ents.Add(new DxfText { Layer = "INFOS", X = rx, Y = ry - 12, Height = 5, Value = $"VIROLE TUBE DROIT dev={runDev:0.0}" });
            }
            else if (chkRun.Checked && p.RunConique)
            {
                // récepteur conique -> développé en SECTEUR de cône (annulaire)
                double ep = p.Epaisseur, Rb = (p.DRun - ep) / 2, Rh = (p.DRunHaut - ep) / 2, Lr = Math.Max(1, p.LRun);
                double kr = (Rh - Rb) / Lr, alpha = Math.Atan(Math.Abs(kr)), sa = Math.Sin(alpha);
                if (sa > 1e-4)
                {
                    double Lb = Rb / sa, Lh = Rh / sa, Lmin = Math.Min(Lb, Lh), Lmax = Math.Max(Lb, Lh);
                    double span = 2 * Math.PI * sa, lapAng = p.Recouvr / Lmax;
                    double cxs = offX, cys = offY + _res.TrouHauteur / 2 + 110 + Lmax;
                    var sec = new List<Pt2>(); int ns = Math.Max(24, p.Segments / 6);
                    for (int i = 0; i <= ns; i++) { double a = Math.PI / 2 - (span + lapAng) / 2 + (span + lapAng) * i / ns; sec.Add(new Pt2(cxs + Lmax * Math.Cos(a), cys - Lmax * Math.Sin(a))); }
                    for (int i = ns; i >= 0; i--) { double a = Math.PI / 2 - (span + lapAng) / 2 + (span + lapAng) * i / ns; sec.Add(new Pt2(cxs + Lmin * Math.Cos(a), cys - Lmin * Math.Sin(a))); }
                    ents.Add(new DxfPolyline { Layer = "DECOUPE", Closed = true, Points = sec });
                    ents.Add(new DxfText { Layer = "INFOS", X = cxs - Lmax / 2, Y = cys - Lmax - 14, Height = 5, Value = $"RECEPTEUR CONIQUE Ø{p.DRun:0}->{p.DRunHaut:0} (secteur, +rec {p.Recouvr:0})" });
                }
            }

            try { File.WriteAllText(sfd.FileName, new DxfWriter().Build(ents)); MessageBox.Show("DXF exporté :\n" + sfd.FileName, "PiquageTube", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            catch (Exception ex) { MessageBox.Show("Erreur export : " + ex.Message, "PiquageTube", MessageBoxButtons.OK, MessageBoxIcon.Error); }
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

        public PreviewPanel() { DoubleBuffered = true; ResizeRedraw = true; BackColor = CBg; }
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
            double R = _r3d.R; double kr = _r3d.RunKr; double xmin = -R * 1.7, xmax = R * 1.7;
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

            double az = 0.7, el = 0.5, ca = Math.Cos(az), sa = Math.Sin(az), ce = Math.Cos(el), se = Math.Sin(el);
            PointF P(Pt3 p) { double X = p.X * ca - p.Y * sa; double Y = p.X * sa * se + p.Y * ca * se + p.Z * ce; return new PointF((float)X, (float)Y); }
            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            foreach (var (a, b, _) in segs) { var qa = P(a); var qb = P(b); minX = Math.Min(minX, Math.Min(qa.X, qb.X)); maxX = Math.Max(maxX, Math.Max(qa.X, qb.X)); minY = Math.Min(minY, Math.Min(qa.Y, qb.Y)); maxY = Math.Max(maxY, Math.Max(qa.Y, qb.Y)); }
            double w = maxX - minX, h = maxY - minY; if (w <= 0) w = 1; if (h <= 0) h = 1;
            int m = 50; double s = Math.Min((Width - 2 * m) / w, (Height - 2 * m) / h);
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
                PointF a1 = Map(new Pt3(0, 0, Rx(0))), a2 = Map(new Pt3(0, 0, -Rx(0)));
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
        }

        void Empty(Graphics g)
        {
            using var f = new Font("Segoe UI", 11);
            var sz = g.MeasureString("Aucun aperçu", f);
            g.DrawString("Aucun aperçu", f, new SolidBrush(Color.FromArgb(90, 90, 90)), (Width - sz.Width) / 2, (Height - sz.Height) / 2);
        }
    }
}
