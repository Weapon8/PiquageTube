using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PiquageTube
{
    public class Profil
    {
        public string Nom { get; set; } = "Nouveau";
        public double DRun { get; set; } = 200;
        public double DBranche { get; set; } = 100;
        public bool Conique { get; set; } = false;
        public double DTop { get; set; } = 100;
        public double AngleDeg { get; set; } = 90;
        public double Deport { get; set; } = 0;
        public double Epaisseur { get; set; } = 0.8;
        public double Recouvr { get; set; } = 10;
        public double LDroite { get; set; } = 80;
        public int Segments { get; set; } = 360;
        public override string ToString() => Nom;
    }

    public class ProfilStore
    {
        public List<Profil> Profils { get; set; } = new();

        private static string PathFile()
        {
            string dir = null;
            try { dir = Path.GetDirectoryName(Environment.ProcessPath); } catch { }
            if (string.IsNullOrEmpty(dir)) dir = AppContext.BaseDirectory;
            return Path.Combine(dir, "PiquageTube.profils.json");
        }

        public static ProfilStore Load()
        {
            try
            {
                string f = PathFile();
                if (File.Exists(f))
                {
                    var s = JsonSerializer.Deserialize<ProfilStore>(File.ReadAllText(f));
                    if (s?.Profils != null && s.Profils.Count > 0) return s;
                }
            }
            catch { }
            var def = Defaults();
            def.Save();
            return def;
        }

        public void Save()
        {
            try
            {
                var opt = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(PathFile(), JsonSerializer.Serialize(this, opt));
            }
            catch { }
        }

        public static ProfilStore Defaults()
        {
            var s = new ProfilStore();
            s.Profils.Add(new Profil { Nom = "Descente 80 cylindrique", DBranche = 80, Conique = false, DTop = 80, AngleDeg = 90 });
            s.Profils.Add(new Profil { Nom = "Descente 100 cylindrique", DBranche = 100, Conique = false, DTop = 100, AngleDeg = 90 });
            s.Profils.Add(new Profil { Nom = "Descente conique 105 / 95", DBranche = 105, Conique = true, DTop = 95, LDroite = 2000, AngleDeg = 90 });
            s.Profils.Add(new Profil { Nom = "Jambon 100 a 67.5deg", DBranche = 100, Conique = false, DTop = 100, AngleDeg = 67.5, Deport = 20 });
            return s;
        }
    }
}
