# PiquageTube

Développé de **piquage cylindre / cylindre** pour tôlerie fine (suite TolTem).
Branche piquée sur tube droit — Ø quelconque, angle libre, déport latéral, **branche cylindrique ou conique**. Génère les flans à plat et le DXF prêt pour Metalix/cncKad. Interface sombre, **profils standard sauvegardés en JSON**.

> Cas type : Y / jambon à côté droit fabriqué en tube droit + bout de tôle roulée, pointé puis étanché à l'étain (galva/zinc 0.73–0.8).

---

## Ce que l'outil sort

- **Développé branche (sabot)** — flan déroulé sur **fibre neutre** `π(Ø − ép)`, courbe de sabot (intersection des deux cylindres) dissymétrique quand il y a déport, top carré + repère `HAUT`. Recouvrement longitudinal intégré directement dans le contour blanc extérieur (Metalix ne lit que l'extérieur).
- **Trou sur le tube droit** — empreinte du piquage déroulée, contour fermé + croix de centre.
- **Virole tube droit** (optionnelle, case à cocher) — rectangle déroulé + recouvrement.
- **Aperçu** 2D (branche / trou) et **vue 3D fil de fer** avec courbe d'intersection en rouge.

## Paramètres

| Champ | Rôle |
|-------|------|
| Ø tube droit (run) | diamètre du tube principal |
| Ø branche piquée | diamètre de la branche (≤ run) |
| Angle / axe run | 90° = perpendiculaire, sinon piquage oblique (plage 2°–178°) |
| Déport latéral | excentration de la branche (Y), 0 = centré |
| Épaisseur | tôle — sert au calcul fibre neutre |
| Recouvrement | lap longitudinal de mouillage brasure (défaut 10 mm) |
| Long. droite branche | partie cylindrique droite au-dessus du sabot |
| Segments | résolution circonférentielle (défaut 360) |

Garde-fou : si `déport + rayon branche > rayon run`, l'outil signale que la branche déborderait le flanc (sabot tronqué).

## DXF

Format **R2000 / AC1015**, validé en audit **ezdxf strict** (0 erreur / 0 fix). Couches TolTem :

| Couche | ACI | Contenu |
|--------|-----|---------|
| DECOUPE | 7 | contours de découpe (blanc, extérieur) |
| TRACAGE | 6 | sabot, seam de recouvrement, croix de centre |
| REPERE | 5 | repère `HAUT` |
| INFOS | 8 | nomenclature pièce, matière, paramètres |
| COTATION | 4 | cotes |

## Branche conique (descentes télescopiques)

Cocher **Branche conique** pour développer la branche en **tronc de cône** au lieu d'un cylindre : on saisit le Ø base (côté piqué) et le Ø haut. Typique des descentes EP à emboîtement (ex. **105 → 95**). Le développé devient un **secteur** ; au roulage, la conicité mâle/femelle est préservée (les deux bouts gardent leurs Ø). Si Ø haut = Ø base, on retombe sur le cylindre.

## Profils standard (JSON)

Les configurations courantes sont mémorisées dans `PiquageTube.profils.json` (à côté de l'exe) :

- **Charger** : applique le profil sélectionné.
- **Enregistrer…** : sauvegarde les paramètres courants sous un nom (écrase si même nom).
- **Suppr.** : retire le profil.

Le fichier est créé automatiquement au premier lancement avec quelques profils par défaut (descente 80, descente 100, descente conique 105/95, jambon 100 à 67.5°). Édite-le à la main si besoin, c'est du JSON lisible.

## Build / publish

Cible : **.NET 8 / WinForms / win-x64**, exécutable self-contained single-file.

```bash
dotnet publish PiquageTube.csproj -c Release -r win-x64 -p:PublishSingleFile=true -o exe_final
```

`<SelfContained>true</SelfContained>` et `IncludeAllContentForSelfExtract=true` sont dans le csproj.
Mettre `toltem.ico` à la racine du projet avant de publier (icône embarquée via `Assembly.GetManifestResourceStream`).

> Intégration future au **TolTemSuite shell** : retirer la ligne `<SelfContained>true</SelfContained>` puis publier avec `--self-contained true` en ligne de commande.

## Structure

```
PiquageTube/
├── PiquageTube.csproj   # cible net8 win-x64, self-contained, icône embarquée
├── Program.cs           # entrée STAThread
├── MainForm.cs          # UI, aperçu 2D/3D, export DXF
├── Geometry.cs          # math piquage cylindre ET cône/cylindre (fibre neutre)
├── Profils.cs           # persistance JSON des profils standard
├── DxfWriter.cs         # writer AC1015 (audit ezdxf strict OK)
└── toltem.ico           # icône (à fournir, non versionnée si tu préfères)
```

## Notes atelier

- Méthode roulé / pointé / étain en ≤ 0.8 → recouvrement par défaut **10 mm** partout pour le mouillage, calcul fibre neutre systématique.
- Évolution possible : languettes/griffes au pied du sabot pour faciliter la brasure branche↔run.

---

*Made with love by weapon666 pour TolTem.*
