using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PiquageTube
{
    /// <summary>
    /// Writer DXF AC1015 (R2000) — structure validée en audit ezdxf strict (0 erreur / 0 fix).
    /// Couches TolTem : DECOUPE(7), TRACAGE(6), REPERE(5), INFOS(8), COTATION(4).
    /// Metalix ne lit que les contours blancs extérieurs -> contour de découpe sur DECOUPE (ACI 7).
    /// LWPOLYLINE : supporte les bulges (arcs) pour le grignotage continu.
    /// </summary>
    public class DxfWriter
    {
        private readonly StringBuilder _sb = new();
        private int _handle = 0x100;
        private string _msRec; // handle du BLOCK_RECORD *Model_Space (owner des entités)

        private string NextHandle() => (_handle++).ToString("X", CultureInfo.InvariantCulture);
        private static string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
        private static string FB(double v) => v.ToString("0.########", CultureInfo.InvariantCulture);   // bulge : plus de décimales
        private void G(int code, string val) { _sb.Append(code).Append('\n').Append(val).Append('\n'); }
        private void G(int code, double val) => G(code, F(val));
        private void G(int code, int val) => G(code, val.ToString(CultureInfo.InvariantCulture));

        private static readonly (string name, int aci)[] Layers =
        {
            ("0", 7), ("DECOUPE", 7), ("TRACAGE", 6), ("REPERE", 5), ("INFOS", 8), ("COTATION", 4)
        };

        public string Build(IEnumerable<DxfEntity> entities)
        {
            // pré-allocation des handles structurels
            string hBlkrecTab = NextHandle();
            string hLtypeTab = NextHandle();
            string hLayerTab = NextHandle();
            string hStyleTab = NextHandle();
            string hVportTab = NextHandle();
            string hViewTab = NextHandle();
            string hUcsTab = NextHandle();
            string hAppidTab = NextHandle();
            string hDimstyTab = NextHandle();
            string hMsRec = NextHandle(); string hPsRec = NextHandle();
            string hLtByBlock = NextHandle(), hLtByLayer = NextHandle(), hLtCont = NextHandle();
            var layerHandles = new List<string>();
            foreach (var _ in Layers) layerHandles.Add(NextHandle());
            string hStyleStd = NextHandle();
            string hVportAct = NextHandle();
            string hAppidAcad = NextHandle();
            string hDimstyStd = NextHandle();
            string hMsBlk = NextHandle(), hMsEnd = NextHandle(), hPsBlk = NextHandle(), hPsEnd = NextHandle();
            string hDictRoot = NextHandle(), hDictGrp = NextHandle();
            _msRec = hMsRec;

            // HEADER
            G(0, "SECTION"); G(2, "HEADER");
            G(9, "$ACADVER"); G(1, "AC1015");
            G(9, "$HANDSEED"); G(5, "FFFF");
            G(9, "$INSUNITS"); G(70, 4);
            G(9, "$LTSCALE"); G(40, 1.0);
            G(9, "$INSBASE"); G(10, 0.0); G(20, 0.0); G(30, 0.0);
            G(0, "ENDSEC");
            // CLASSES
            G(0, "SECTION"); G(2, "CLASSES"); G(0, "ENDSEC");
            // TABLES
            G(0, "SECTION"); G(2, "TABLES");
            // VPORT
            G(0, "TABLE"); G(2, "VPORT"); G(5, hVportTab); G(330, "0"); G(100, "AcDbSymbolTable"); G(70, 1);
            G(0, "VPORT"); G(5, hVportAct); G(330, hVportTab); G(100, "AcDbSymbolTableRecord"); G(100, "AcDbViewportTableRecord");
            G(2, "*Active"); G(70, 0); G(10, 0.0); G(20, 0.0); G(11, 1.0); G(21, 1.0);
            G(12, 0.0); G(22, 0.0); G(13, 0.0); G(23, 0.0); G(14, 10.0); G(24, 10.0);
            G(15, 10.0); G(25, 10.0); G(16, 0.0); G(26, 0.0); G(36, 1.0); G(17, 0.0); G(27, 0.0); G(37, 0.0);
            G(40, 297.0); G(41, 1.5); G(42, 50.0); G(43, 0.0); G(44, 0.0); G(50, 0.0); G(51, 0.0);
            G(71, 0); G(72, 100); G(73, 1); G(74, 3); G(75, 0); G(76, 0); G(77, 0); G(78, 0);
            G(0, "ENDTAB");
            // LTYPE
            G(0, "TABLE"); G(2, "LTYPE"); G(5, hLtypeTab); G(330, "0"); G(100, "AcDbSymbolTable"); G(70, 3);
            LtypeRec(hLtByBlock, hLtypeTab, "ByBlock", "");
            LtypeRec(hLtByLayer, hLtypeTab, "ByLayer", "");
            LtypeRec(hLtCont, hLtypeTab, "Continuous", "Solid line");
            G(0, "ENDTAB");
            // LAYER
            G(0, "TABLE"); G(2, "LAYER"); G(5, hLayerTab); G(330, "0"); G(100, "AcDbSymbolTable"); G(70, Layers.Length);
            for (int i = 0; i < Layers.Length; i++)
            {
                G(0, "LAYER"); G(5, layerHandles[i]); G(330, hLayerTab);
                G(100, "AcDbSymbolTableRecord"); G(100, "AcDbLayerTableRecord");
                G(2, Layers[i].name); G(70, 0); G(62, Layers[i].aci); G(6, "Continuous"); G(370, -3); G(390, "F");
            }
            G(0, "ENDTAB");
            // STYLE
            G(0, "TABLE"); G(2, "STYLE"); G(5, hStyleTab); G(330, "0"); G(100, "AcDbSymbolTable"); G(70, 1);
            G(0, "STYLE"); G(5, hStyleStd); G(330, hStyleTab); G(100, "AcDbSymbolTableRecord"); G(100, "AcDbTextStyleTableRecord");
            G(2, "Standard"); G(70, 0); G(40, 0.0); G(41, 1.0); G(50, 0.0); G(71, 0); G(42, 2.5); G(3, "txt"); G(4, "");
            G(0, "ENDTAB");
            // VIEW / UCS
            G(0, "TABLE"); G(2, "VIEW"); G(5, hViewTab); G(330, "0"); G(100, "AcDbSymbolTable"); G(70, 0); G(0, "ENDTAB");
            G(0, "TABLE"); G(2, "UCS"); G(5, hUcsTab); G(330, "0"); G(100, "AcDbSymbolTable"); G(70, 0); G(0, "ENDTAB");
            // APPID
            G(0, "TABLE"); G(2, "APPID"); G(5, hAppidTab); G(330, "0"); G(100, "AcDbSymbolTable"); G(70, 1);
            G(0, "APPID"); G(5, hAppidAcad); G(330, hAppidTab); G(100, "AcDbSymbolTableRecord"); G(100, "AcDbRegAppTableRecord"); G(2, "ACAD"); G(70, 0);
            G(0, "ENDTAB");
            // DIMSTYLE
            G(0, "TABLE"); G(2, "DIMSTYLE"); G(5, hDimstyTab); G(330, "0"); G(100, "AcDbSymbolTable"); G(70, 1); G(100, "AcDbDimStyleTable"); G(71, 0);
            G(0, "DIMSTYLE"); G(105, hDimstyStd); G(330, hDimstyTab); G(100, "AcDbSymbolTableRecord"); G(100, "AcDbDimStyleTableRecord"); G(2, "Standard"); G(70, 0);
            G(0, "ENDTAB");
            // BLOCK_RECORD
            G(0, "TABLE"); G(2, "BLOCK_RECORD"); G(5, hBlkrecTab); G(330, "0"); G(100, "AcDbSymbolTable"); G(70, 2);
            BlockRecord(hMsRec, hBlkrecTab, "*Model_Space");
            BlockRecord(hPsRec, hBlkrecTab, "*Paper_Space");
            G(0, "ENDTAB");
            G(0, "ENDSEC");
            // BLOCKS
            G(0, "SECTION"); G(2, "BLOCKS");
            BlockDef(hMsBlk, hMsEnd, hMsRec, "*Model_Space", false);
            BlockDef(hPsBlk, hPsEnd, hPsRec, "*Paper_Space", true);
            G(0, "ENDSEC");
            // ENTITIES
            G(0, "SECTION"); G(2, "ENTITIES");
            foreach (var e in entities) WriteEntity(e);
            G(0, "ENDSEC");
            // OBJECTS
            G(0, "SECTION"); G(2, "OBJECTS");
            G(0, "DICTIONARY"); G(5, hDictRoot); G(330, "0"); G(100, "AcDbDictionary"); G(281, 1); G(3, "ACAD_GROUP"); G(350, hDictGrp);
            G(0, "DICTIONARY"); G(5, hDictGrp); G(330, hDictRoot); G(100, "AcDbDictionary"); G(281, 1);
            G(0, "ENDSEC");
            G(0, "EOF");
            return _sb.ToString();
        }

        private void LtypeRec(string h, string owner, string name, string desc)
        {
            G(0, "LTYPE"); G(5, h); G(330, owner); G(100, "AcDbSymbolTableRecord"); G(100, "AcDbLinetypeTableRecord");
            G(2, name); G(70, 0); G(3, desc); G(72, 65); G(73, 0); G(40, 0.0);
        }
        private void BlockRecord(string h, string owner, string name)
        {
            G(0, "BLOCK_RECORD"); G(5, h); G(330, owner); G(100, "AcDbSymbolTableRecord"); G(100, "AcDbBlockTableRecord");
            G(2, name); G(70, 0); G(280, 1); G(281, 0);
        }
        private void BlockDef(string hBlk, string hEnd, string owner, string name, bool paper)
        {
            G(0, "BLOCK"); G(5, hBlk); G(330, owner); G(100, "AcDbEntity"); if (paper) G(67, 1); G(8, "0");
            G(100, "AcDbBlockBegin"); G(2, name); G(70, 0); G(10, 0.0); G(20, 0.0); G(30, 0.0); G(3, name); G(1, "");
            G(0, "ENDBLK"); G(5, hEnd); G(330, owner); G(100, "AcDbEntity"); if (paper) G(67, 1); G(8, "0"); G(100, "AcDbBlockEnd");
        }

        private void WriteEntity(DxfEntity e)
        {
            if (e is DxfPolyline pl)
            {
                G(0, "LWPOLYLINE"); G(5, NextHandle()); G(330, _msRec); G(100, "AcDbEntity"); G(8, pl.Layer); G(100, "AcDbPolyline");
                G(90, pl.Points.Count); G(70, pl.Closed ? 1 : 0);
                for (int i = 0; i < pl.Points.Count; i++)
                {
                    var pt = pl.Points[i];
                    G(10, pt.X); G(20, pt.Y);
                    // bulge (code 42) : arc entre ce sommet et le suivant ; omis si segment droit
                    if (pl.Bulges != null && i < pl.Bulges.Count && Math.Abs(pl.Bulges[i]) > 1e-9)
                        G(42, FB(pl.Bulges[i]));
                }
            }
            else if (e is DxfText tx)
            {
                G(0, "TEXT"); G(5, NextHandle()); G(330, _msRec); G(100, "AcDbEntity"); G(8, tx.Layer); G(100, "AcDbText");
                G(10, tx.X); G(20, tx.Y); G(30, 0.0); G(40, tx.Height); G(1, tx.Value); G(7, "Standard"); G(100, "AcDbText");
            }
            else if (e is DxfLine ln)
            {
                G(0, "LINE"); G(5, NextHandle()); G(330, _msRec); G(100, "AcDbEntity"); G(8, ln.Layer); G(100, "AcDbLine");
                G(10, ln.X1); G(20, ln.Y1); G(30, 0.0); G(11, ln.X2); G(21, ln.Y2); G(31, 0.0);
            }
        }
    }

    public abstract class DxfEntity { public string Layer = "DECOUPE"; }
    /// <summary>Bulges = null : polyligne droite. Sinon un bulge par sommet (0 = segment droit).</summary>
    public class DxfPolyline : DxfEntity { public List<Pt2> Points = new(); public List<double> Bulges; public bool Closed = true; }
    public class DxfText : DxfEntity { public double X, Y, Height = 4; public string Value = ""; }
    public class DxfLine : DxfEntity { public double X1, Y1, X2, Y2; }
}
