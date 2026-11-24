# Miniaturgolf (Eternit, WMF/DMV Abt. 2) - 3D reference notes, lanes 1-9

Collected 2026-11-23. Scope: heights, profiles and 3D shape only (plan dimensions are in the norm and are not repeated).

Conventions used below:

- **STATED** = a number or fact written by the source, with URL and quote.
- **ESTIMATE** = my own measurement from a photo, with the scale reference. Treat estimates as +-15 % at best unless noted.
- **OBSERVED** = qualitative shape fact visible in the photos (no number).

Overall result: apart from the norm itself, **no source states obstacle heights or radii**. Manufacturer pages give materials, plate thickness, frame profile, pipe and ring diameters only. Everything else below is photo estimation.

Sources that were checked and gave nothing usable: de.wikipedia "Miniaturgolf" (now only a redirect to Bahnengolf, no per-lane gallery; same for sv/en/cs/pl/it articles), Wikimedia Commons (few usable photos, none categorised per obstacle), minigolf-schondorf.de lane pages (rule text and norm drawings only), miniaturgolf-am-stadtwald.de (plan diagrams only), minigolfplatz.com Hamburg (lane image service broken), minigolfshop.de / Game N Fun (no dimensions beyond 0.90 x 6.25 m and 1.40 m), old Austrian norm OEBGV-Re-2.2 via web.archive.org (plan views only, same as current norm), Bamberg 2019 course description (ball/line advice only). NIFO, City Golf Europe and Bergmann catalogues were not reached.

Photo sources used:

- 1. BGC Wolfsburg / Minigolf Vorsfelde, https://minigolf-vorsfelde.de/about/ - one short 1080p video per lane on Google Drive; frames extracted with ffmpeg (1 fps). Their lane numbering differs from the norm numbering.
- "A Couple of Putts" blog + Flickr account `coupleputts`: Solna BGK https://acoupleofputts.com/2024/12/12/solna-bangolfklubb-eternit-minigolf-solna-sweden/ and Uppsala BGK https://acoupleofputts.com/2023/08/21/uppsala-bangolfklubb-eternit-miniaturegolf-uppsala-sweden/ - Flickr originals (about 4032x3024).
- Minigolf Harrislee, https://www.minigolf-harrislee.de/Unsere-Anlage.htm - one photo per lane, only 400x300.
- Minigolf Springe, https://www.minigolf-springe.de/die-minigolfanlage - gallery originals (overviews).
- Czech Minigolf s.r.o. (minigolfbau.eu), https://www.minigolfbau.eu/miniatur-golf-hindernisse/ and https://www.minigolfbau.eu/minigolf-miniaturgolf/ - product photos. Caution: this manufacturer also sells hobby variants; shapes agree with club photos but are not guaranteed norm-exact. Some of these photos already exist in `refs\minigolfbau_eu\`.
- Wikimedia Commons (two files).

Rejected after viewing: Commons "Rodalben-Minigolfanlage-02" (short hump, not the Bruecke), Vorsfelde "Fenster" video (ramp with a wall and window in mid-lane = Auflaufkeil/Toeter type, not lane 8), Solna red/white cone lane (Vulkan), all Hamburg, Stadtwald, Schondorf and Gebhardshagen downloads.

## Lane 1 - Pyramiden

### Images (folder `lane01\`)

- `harrislee_pyramiden.jpg` (400x300) - Whole lane, oblique: three low triangular 2D pyramids (dark frames), staggered left/right/left. Small (400x300).
  - Source: https://img.webme.com/pic/c/cobras-harrislee/bahn07.jpg (page https://www.minigolf-harrislee.de/Unsere-Anlage.htm)
- `minigolfbau_pyramiden_2d.jpg` (900x540) - Manufacturer photo, 2D type: three triangles of upright galvanised flat steel (about rail height), gravel-filled, standing on the plate.
  - Source: https://16af6c9f59.clvaw-cdnwnd.com/e29ac4878528e2f7e55fddb9691c9b4a/200002418-0717a0717e/Minigolf%20Pyramidem.JPG (minigolfbau.eu product photo; pages https://www.minigolfbau.eu/miniatur-golf-hindernisse/ and /minigolf-miniaturgolf/)
- `minigolfbau_pyramiden_3d.jpg` (900x675) - Manufacturer photo, 3D type: three solid steep-sided pyramids (blue, red, yellow), the last one at the target-circle entrance.
  - Source: https://16af6c9f59.clvaw-cdnwnd.com/e29ac4878528e2f7e55fddb9691c9b4a/200002420-80c5580c59/Minigolf%20Pyramiden-3.jpg (minigolfbau.eu product photo; pages https://www.minigolfbau.eu/miniatur-golf-hindernisse/ and /minigolf-miniaturgolf/)
- `vorsfelde_pyramiden_frame05.jpg` (1080x1920) - Video frame, view from the target circle down the lane with player at the tee: three open steel-band triangles; hole and circle rail well visible.
  - Source: page https://minigolf-vorsfelde.de/about/ -> video https://drive.google.com/file/d/1tqV8WXD0C1fo7ZKpgfKvNAHHVbykynX4/view (frame at t=5 s)
- `vorsfelde_pyramiden_frame11.jpg` (1080x1920) - Same view, ball lying in the hole (scale for hole diameter).
  - Source: page https://minigolf-vorsfelde.de/about/ -> video https://drive.google.com/file/d/1tqV8WXD0C1fo7ZKpgfKvNAHHVbykynX4/view (frame at t=11 s)

### Stated dimensions

- None found for heights. minigolfbau.eu only names the two variants and materials: "Miniatur Golf - Pyramiden - 2D ... Hindernise ( set - 3 Stücke ) Material verzinkt Stahl" and "Miniatur Golf Pyramiden - 3D ... Material Schichtpressstoff (GFK) ... verzinkt Stahl pulverbeschichte" (https://www.minigolfbau.eu/miniatur-golf-hindernisse/).

### Shape (observed) and estimates

- OBSERVED: two accepted constructions. (a) "2D": a triangle of upright flat steel band standing on the plate, open or gravel-filled (Vorsfelde, Harrislee, minigolfbau 2D). (b) "3D": a solid pyramid with the same triangular footprint and steep faces (minigolfbau 3D).
- OBSERVED: the band triangles have sharp welded corners, vertical walls, no flange.
- ESTIMATE (2D band height): about equal to the side rail, i.e. roughly 4-5 cm. Scale: band compared with the adjacent rail in `vorsfelde_pyramiden_frame05.jpg` and `minigolfbau_pyramiden_2d.jpg`.
- ESTIMATE (3D pyramid height): roughly 0.8-1.0 x its base edge; the yellow one has a base edge of about 0.4 x lane width (about 35-40 cm), so about 30-35 cm high. Scale: 90 cm lane width in `minigolfbau_pyramiden_3d.jpg`. Low confidence (oblique view).

## Lane 2 - Salto (Looping)

### Images (folder `lane02\`)

- `commons_melnik_q.jpg` (3296x1854) - UNCERTAIN: red loop obstacle seen end-on on a small (probably non-norm) lane at Autocamp Melnik; only useful as a front view of a looping band.
  - Source: https://commons.wikimedia.org/wiki/File:Autocamp_Mělník4.jpg (original file)
- `harrislee_salto.jpg` (400x300) - Whole lane oblique: galvanised looping about 2/3 down the lane. Small.
  - Source: https://img.webme.com/pic/c/cobras-harrislee/bahn16.jpg (page https://www.minigolf-harrislee.de/Unsere-Anlage.htm)
- `minigolfbau_salto_a.jpg` (1024x576) - Manufacturer photo: blue looping seen almost exactly from the side, lane edge-on; good for loop diameter vs. rail and for the entry/exit tongues.
  - Source: https://16af6c9f59.clvaw-cdnwnd.com/e29ac4878528e2f7e55fddb9691c9b4a/200002394-2667d26680/MINIATUR%20GOLF%20HINDERNISSE.jpg (minigolfbau.eu product photo; pages https://www.minigolfbau.eu/miniatur-golf-hindernisse/ and /minigolf-miniaturgolf/)
- `minigolfbau_salto_b.jpg` (900x675) - Manufacturer photo, 3/4 view: loop band with side flanges, helical offset between entry and exit, V-shaped funnel bars from both rails, flat entry tongue.
  - Source: https://16af6c9f59.clvaw-cdnwnd.com/e29ac4878528e2f7e55fddb9691c9b4a/200002402-6029760299/minigolf%20salto-9.JPG (minigolfbau.eu product photo; pages https://www.minigolfbau.eu/miniatur-golf-hindernisse/ and /minigolf-miniaturgolf/)
- `solna_salto_endon.jpg` (4032x3024) - Flickr original (shows upside-down unless EXIF rotation is applied): red looping end-on from the tee; band width vs. lane width; broken plate in the foreground shows plate edge/thickness.
  - Source: https://www.flickr.com/photos/coupleputts/54191327486/ (original size; via https://acoupleofputts.com/2024/12/12/solna-bangolfklubb-eternit-minigolf-solna-sweden/)
- `solna_salto_player_oblique.jpg` (4032x3024) - Flickr original: red looping with a player at the tee for scale; funnel bars and exit towards the target circle visible.
  - Source: https://www.flickr.com/photos/coupleputts/54190431772/ (original size; via https://acoupleofputts.com/2024/12/12/solna-bangolfklubb-eternit-minigolf-solna-sweden/)
- `vorsfelde_salto_frame01.jpg` (1920x1080) - Video frame: galvanised looping from the tee side, player's legs and putter for scale, funnel bars.
  - Source: page https://minigolf-vorsfelde.de/about/ -> video https://drive.google.com/file/d/1NHgS5MIJ36MwVIHeDqMljfRfBhyhM3ID/view (frame at t=1 s)
- `vorsfelde_salto_frame08.jpg` (1920x1080) - Video frame: looping in 3/4 view with target circle behind; entry tongue and funnel clearly visible.
  - Source: page https://minigolf-vorsfelde.de/about/ -> video https://drive.google.com/file/d/1NHgS5MIJ36MwVIHeDqMljfRfBhyhM3ID/view (frame at t=8 s)

### Stated dimensions

- None found for loop diameter or height. The norm only covers the entry (plan).

### Shape (observed) and estimates

- OBSERVED: the loop is a single sheet-metal (or GFK) band with low side flanges, wound once as a helix: the exit lies beside the entry, offset sideways by about one band width. Entry and exit are flat tongues lying on the plate.
- OBSERVED: two straight bars run from the side rails to the loop entry forming a V funnel (Vorsfelde, Solna, minigolfbau), so a ball cannot pass beside the loop.
- OBSERVED: the loop stands noticeably off-centre towards one rail in some builds (Vorsfelde frame01) and centred in others (minigolfbau).
- ESTIMATE (outer diameter): 55-65 cm. Scale references: lane width at the loop in `vorsfelde_salto_frame01.jpg` (loop height about 0.65-0.7 x lane width) and the standing player in `solna_salto_player_oblique.jpg` (loop top at about lower-thigh height). Low to medium confidence.
- ESTIMATE (band width): about 1/3 of the loop diameter, i.e. about 18-22 cm, consistent with the norm's entrance width. Flange height about 2-3 cm.
- ESTIMATE (total footprint of loop plus tongues along the lane): about 1.0-1.2 m, from `minigolfbau_salto_a.jpg` (loop diameter as scale).

## Lane 3 - Schräger Kreis mit Niere

### Images (folder `lane03\`)

- `harrislee_niere_kaese.jpg` (400x300) - Whole lane (called "Der Kaese" there): two-part 2D kidney outline in mid-lane, round target circle. Small; circle tilt not discernible.
  - Source: https://img.webme.com/pic/c/cobras-harrislee/bahn10.jpg (page https://www.minigolf-harrislee.de/Unsere-Anlage.htm)
- `minigolfbau_niere_2d.jpg` (900x675) - Manufacturer photo, 2D kidney: two halves of bent flat steel band (about rail height) with a straight tunnel between them, bolted to the plate with small lugs.
  - Source: https://16af6c9f59.clvaw-cdnwnd.com/e29ac4878528e2f7e55fddb9691c9b4a/200002442-b67a9b67b3/Minigolf%20Hindernis%20Niere%202D.JPG (minigolfbau.eu product photo; pages https://www.minigolfbau.eu/miniatur-golf-hindernisse/ and /minigolf-miniaturgolf/)
- `minigolfbau_niere_3d.jpg` (900x507) - Manufacturer photo, 3D kidney: closed yellow box with flat top and a straight tunnel slot through it.
  - Source: https://16af6c9f59.clvaw-cdnwnd.com/e29ac4878528e2f7e55fddb9691c9b4a/200002990-67c5e67c61/Miniatur%20Golf%20Niere.jpeg (minigolfbau.eu product photo; pages https://www.minigolfbau.eu/miniatur-golf-hindernisse/ and /minigolf-miniaturgolf/)
- `vorsfelde_niere_frame01.jpg` (1920x1080) - Video frame from the target circle: two-part steel kidney near the tee end, player for scale.
  - Source: page https://minigolf-vorsfelde.de/about/ -> video https://drive.google.com/file/d/1efoZlqMOdbj7k9-Cj7brWgMP3YKU0bhp/view (frame at t=1 s)
- `vorsfelde_niere_frame10.jpg` (1920x1080) - Same, slightly closer.
  - Source: page https://minigolf-vorsfelde.de/about/ -> video https://drive.google.com/file/d/1efoZlqMOdbj7k9-Cj7brWgMP3YKU0bhp/view (frame at t=10 s)
- `vorsfelde_niere_frame13.jpg` (1920x1080) - Video frame: target circle of that lane from a low angle, standing on its own supports (rail, plate edge, hole visible). Tilt not clearly measurable.
  - Source: page https://minigolf-vorsfelde.de/about/ -> video https://drive.google.com/file/d/1efoZlqMOdbj7k9-Cj7brWgMP3YKU0bhp/view (frame at t=13 s)

### Stated dimensions

- None found (no stated tilt angle or height difference of the circle, no stated kidney height).
- minigolfbau.eu lists the kidney as "Niere 3D" (steel or GFK) and "Niere 2D" (steel) without sizes (https://www.minigolfbau.eu/miniatur-golf-hindernisse/).

### Shape (observed) and estimates

- OBSERVED: the kidney comes as (a) two bent flat-steel band halves with a straight tunnel between them, bolted down with small lugs, or (b) a closed flat-topped box with a tunnel slot. Walls are vertical.
- ESTIMATE (kidney wall height, 2D): about rail height, 4-5 cm. Scale: rail in `minigolfbau_niere_2d.jpg`.
- ESTIMATE (kidney height, 3D box): about 10-12 cm (roughly 2.5 x rail height in `minigolfbau_niere_3d.jpg`). Low confidence.
- NOT FOUND: a photo that shows the slope of the "schräger Kreis" clearly enough to measure. In `vorsfelde_niere_frame13.jpg` the target circle sits on separate supports, which would allow a tilt, but the tilt itself cannot be read. The Vorsfelde and Harrislee lanes are locally called "Niere"/"Käse"; I could not confirm from the photos that their circles are tilted. The Bamberg course sheet only says for this lane "highest point of lane on the right side and at half past eight" (https://www.minigolfsport.de/pdf/Sportbetrieb/Meisterschaften/2019/Deaf%20WC%202019/course%20description%20Bamberg%20miniature%20golf%20April%202019.pdf) - text extraction of that table is scrambled, so the row assignment is uncertain.

## Lane 4 - Doppelwellen

### Images (folder `lane04\`)

- `commons_herdecke_doppelwelle_side.jpg` (1493x1000) - Wikimedia Commons: double-wave lane (white surface, brown sides) in near side view with target circle; both crests visible.
  - Source: https://commons.wikimedia.org/wiki/File:Minigolf_Herdecke.jpg (original file)
- `minigolfbau_doppelwelle.jpg` (900x675) - Manufacturer photo: blue GFK double wave on a galvanised frame, open underneath, oblique side view.
  - Source: https://16af6c9f59.clvaw-cdnwnd.com/e29ac4878528e2f7e55fddb9691c9b4a/200002424-edc2fedc32/Minigolf%20Welle.JPG (minigolfbau.eu product photo; pages https://www.minigolfbau.eu/miniatur-golf-hindernisse/ and /minigolf-miniaturgolf/)
- `solna_doppelwelle_side.jpg` (4032x3024) - Flickr original, BEST: true side view of an Eternit double wave with players and a ball on the lane; steel side arches, open underside, adjustable feet.
  - Source: https://www.flickr.com/photos/coupleputts/54191600819/ (original size; via https://acoupleofputts.com/2024/12/12/solna-bangolfklubb-eternit-minigolf-solna-sweden/)
- `springe_doppelwelle.jpg` (4032x3024) - High-res overview: orange/blue double wave in the background (side view, distant), another raised lane in front.
  - Source: https://image.jimcdn.com/app/cms/image/transf/none/path/s2495adba1da9d704/image/ie941959c49331988/version/1537197650/image.jpg (gallery on https://www.minigolf-springe.de/die-minigolfanlage)
- `uppsala_doppelwelle_endon.jpg` (4000x3000) - Flickr original (rotated 90 degrees): double wave seen from above the target circle looking back; bent plates, rail following the waves, circle with repositioning line and hole.
  - Source: https://www.flickr.com/photos/coupleputts/53133111946/ (original size; via https://acoupleofputts.com/2023/08/21/uppsala-bangolfklubb-eternit-miniaturegolf-uppsala-sweden/)
- `uppsala_doppelwelle_oblique.jpg` (4032x3024) - Flickr original: green double wave in the middle distance, oblique.
  - Source: https://www.flickr.com/photos/coupleputts/53124368853/ (original size; via https://acoupleofputts.com/2023/08/21/uppsala-bangolfklubb-eternit-miniaturegolf-uppsala-sweden/)
- `vorsfelde_welle_frame03.jpg` (1920x1080) - Video frame, close-up from the tee: first wave rising, rail following the curve, open dark gap under the plate.
  - Source: page https://minigolf-vorsfelde.de/about/ -> video https://drive.google.com/file/d/1FnZYAIiopwMG0y8YmCtCXke0DLtSMMDR/view (frame at t=3 s; lane called Bodenwelle there)
- `vorsfelde_welle_frame12.jpg` (1920x1080) - Video frame: second wave running out into the target circle; plate joint on the crest visible.
  - Source: page https://minigolf-vorsfelde.de/about/ -> video https://drive.google.com/file/d/1FnZYAIiopwMG0y8YmCtCXke0DLtSMMDR/view (frame at t=12 s)

### Stated dimensions

- None found. minigolfbau.eu sells only a GFK "Bodenwellen" pair ("Welle 1 ( kleine )", "Welle 2 ( große )") without sizes.

### Shape (observed) and estimates

- OBSERVED: the plates themselves are bent into two smooth humps; the side rail follows the curve. Under the humps the lane is open, carried by arched steel side members on a straight bottom frame (`solna_doppelwelle_side.jpg`).
- OBSERVED: profile is sine-like with concave lead-in and run-out; the trough between the crests comes back almost to lane level.
- ESTIMATE from `solna_doppelwelle_side.jpg`, scale = lane length 6.25 m including circle (cross-checked with the ball on the lane):
  - flat tee section about 1.4-1.6 m;
  - crest heights about 30 cm (+-4 cm) above lane level, both crests about equal;
  - crest-to-crest spacing about 1.4-1.5 m;
  - trough about 4-5 cm above lane level;
  - first wave looks longer (about 1.7 m base) than the second (about 1.2-1.3 m), but this may partly be wide-angle perspective;
  - short flat run (about 0.3-0.6 m) between the second wave and the target circle.
- The Herdecke and minigolfbau lanes show the same proportions (crest height about 1/3 of lane width).

## Lane 5 - Liegende Schleife

### Images (folder `lane05\`)

- `harrislee_schleife.jpg` (400x300) - Whole lane oblique: black moulded lying loop just before the target circle. Small.
  - Source: https://img.webme.com/pic/c/cobras-harrislee/bahn04.jpg (page https://www.minigolf-harrislee.de/Unsere-Anlage.htm)
- `minigolfbau_liegende_schleife.jpg` (900x675) - Manufacturer photo from above: red GFK lying loop - full-width entry ramp, banked spiral channel, raised centre, exit.
  - Source: https://16af6c9f59.clvaw-cdnwnd.com/e29ac4878528e2f7e55fddb9691c9b4a/200002398-b0655b0658/mnigolf%20Liegende%20Schleife.JPG (minigolfbau.eu product photo; pages https://www.minigolfbau.eu/miniatur-golf-hindernisse/ and /minigolf-miniaturgolf/)
- `springe_schleife_overview.jpg` (4032x3024) - High-res overview: red lying loop in the background before the target circle (distant); foreground lane shows tee plate and rail detail.
  - Source: https://image.jimcdn.com/app/cms/image/transf/none/path/s2495adba1da9d704/image/i486365a82aecb924/version/1537197650/image.jpg (gallery on https://www.minigolf-springe.de/die-minigolfanlage)
- `vorsfelde_schleife_frame08.jpg` (1920x1080) - Video frame from the tee side: ball rolling towards the red moulded loop; outer bank height vs. rail visible.
  - Source: page https://minigolf-vorsfelde.de/about/ -> video https://drive.google.com/file/d/1VaonE70RvYMe9YoX_nWcx4gs_bzrKfeP/view (frame at t=8 s)
- `vorsfelde_schleife_frame10.jpg` (1920x1080) - Video frame, BEST: loop from the side - exit channel dropping back onto the lane, ball just leaving, outer wall height.
  - Source: page https://minigolf-vorsfelde.de/about/ -> video https://drive.google.com/file/d/1VaonE70RvYMe9YoX_nWcx4gs_bzrKfeP/view (frame at t=10 s)

### Stated dimensions

- None found. minigolfbau.eu: GFK moulding, no sizes.

### Shape (observed) and estimates

- OBSERVED: one-piece moulding spanning the full lane width. The ball enters over a short full-width ramp, is caught by a banked channel that spirals about 360 degrees around a raised centre, climbing slightly, and leaves through a straight exit channel that drops back onto the lane on the target side (`vorsfelde_schleife_frame10.jpg`, `minigolfbau_liegende_schleife.jpg`).
- OBSERVED: the outer wall is highest on the outside of the turn; the centre is a raised island; the channel floor is banked.
- ESTIMATE (outer wall height): about 20-25 cm above the plate. Scale: rail height and the ball in `vorsfelde_schleife_frame10.jpg`. Low confidence.
- ESTIMATE (channel width): about 12-15 cm (about 3 ball diameters, same frame).
- ESTIMATE (footprint): full lane width x about 1.0-1.1 m along the lane, from `minigolfbau_liegende_schleife.jpg` (lane width as scale).

## Lane 6 - Brücke

### Images (folder `lane06\`)

- `harrislee_bruecke.jpg` (400x300) - Whole lane, side/oblique: bridge arch on posts ending at the target circle. Small.
  - Source: https://img.webme.com/pic/c/cobras-harrislee/bahn06.jpg (page https://www.minigolf-harrislee.de/Unsere-Anlage.htm)
- `minigolfbau_bruecke.jpg` (900x608) - Manufacturer photo: red GFK bridge on a galvanised tube frame seen from one end; open underneath, cross braces.
  - Source: https://16af6c9f59.clvaw-cdnwnd.com/e29ac4878528e2f7e55fddb9691c9b4a/200002428-6c9336c937/Minigolf%20Br%C3%BCcke-1.JPG (minigolfbau.eu product photo; pages https://www.minigolfbau.eu/miniatur-golf-hindernisse/ and /minigolf-miniaturgolf/)
- `solna_bruecke_background.jpg` (4032x3024) - Flickr original: bridge lane in the background in side view (foreground: a different ramp lane with a player).
  - Source: https://www.flickr.com/photos/coupleputts/54191771800/ (original size; via https://acoupleofputts.com/2024/12/12/solna-bangolfklubb-eternit-minigolf-solna-sweden/)
- `solna_bruecke_side.jpg` (4032x3024) - Flickr original, BEST: true side view of the whole bridge lane from tee to target circle, two support posts under the apex, people for scale.
  - Source: https://www.flickr.com/photos/coupleputts/54190431557/ (original size; via https://acoupleofputts.com/2024/12/12/solna-bangolfklubb-eternit-minigolf-solna-sweden/)
- `springe_bruecke_side.jpg` (4032x3024) - High-res overview: red bridge in side view (distant) ending in the target circle.
  - Source: https://image.jimcdn.com/app/cms/image/transf/none/path/s2495adba1da9d704/image/ie4cc787f8898abc7/version/1537197650/image.jpg (gallery on https://www.minigolf-springe.de/die-minigolfanlage)
- `uppsala_bruecke_oblique.jpg` (4032x3024) - Flickr original: green-railed bridge, oblique from the tee end; rail follows the arch, frame below.
  - Source: https://www.flickr.com/photos/coupleputts/53123884966/ (original size; via https://acoupleofputts.com/2023/08/21/uppsala-bangolfklubb-eternit-miniaturegolf-uppsala-sweden/)

### Stated dimensions

- None found. minigolfbau.eu sells the bridge as two GFK halves ("Brücke Hindernis - Hälfte"), no sizes.

### Shape (observed) and estimates

- OBSERVED: the whole lane between the tee plate and the target circle is one continuous arch; plates are bent, the rail follows the arch, the underside is open and carried by a tube frame with posts under the apex.
- OBSERVED: profile is not a plain circular arc: convex over the top with concave transitions at both ends (S-curves), and it runs straight into the target circle.
- ESTIMATE from `solna_bruecke_side.jpg` (true side view; scale = total lane length 6.25 m, cross-checked with the 1.40 m circle, both give about 275 px/m in my working crop):
  - flat tee section about 0.7 m;
  - arch span about 4.1 m, ending at the circle entrance;
  - apex about 0.60 m (+-0.07 m) above lane level, located about 2.6-2.8 m from the tee end;
  - two vertical support posts close to the apex.
- Other builds (Uppsala, Harrislee, Springe, minigolfbau) look proportionally similar; none could be measured as well.

## Lane 7 - Sprungschanze mit Netz

### Images (folder `lane07\`)

- `harrislee_netz.jpg` (400x300) - Whole lane: dark ramp (Sprungschanze) and a large green backboard with a circular ring opening instead of a free-standing net ring. Small.
  - Source: https://img.webme.com/pic/c/cobras-harrislee/bahn14.jpg (page https://www.minigolf-harrislee.de/Unsere-Anlage.htm)
- `minigolfbau_netz_a.jpg` (900x675) - Manufacturer photo: net ring (stated diameter 50 cm) on a single post with net bag, flat safety-net barrier behind (stated 200/200 cm).
  - Source: https://16af6c9f59.clvaw-cdnwnd.com/e29ac4878528e2f7e55fddb9691c9b4a/200002414-bb628bb62b/minigolf%20Netz-8.JPG (minigolfbau.eu product photo; pages https://www.minigolfbau.eu/miniatur-golf-hindernisse/ and /minigolf-miniaturgolf/)
- `minigolfbau_netz_b.jpg` (900x675) - Manufacturer photo: ring on a post in front of a curved safety net (stated 200/314 cm).
  - Source: https://16af6c9f59.clvaw-cdnwnd.com/e29ac4878528e2f7e55fddb9691c9b4a/200002416-4a6ee4a6f1/Netz%20Minigolf.JPG (minigolfbau.eu product photo; pages https://www.minigolfbau.eu/miniatur-golf-hindernisse/ and /minigolf-miniaturgolf/)
- `minigolfbau_sprungschanze.jpg` (900x590) - Manufacturer photo: red GFK jump ramp on a short lane section - inclined face, sharp ridge, side facets.
  - Source: https://16af6c9f59.clvaw-cdnwnd.com/e29ac4878528e2f7e55fddb9691c9b4a/200002412-0068d00690/minigolf%20Netz%20-%20Sprungschanze.JPG (minigolfbau.eu product photo; pages https://www.minigolfbau.eu/miniatur-golf-hindernisse/ and /minigolf-miniaturgolf/)
- `springe_schanze_netz.jpg` (600x800) - Red ramp at the end of a short lane piece, safety-net fence, two people for scale. 600x800.
  - Source: https://image.jimcdn.com/app/cms/image/transf/none/path/s2495adba1da9d704/image/i4c6b2003903eafee/version/1537197650/image.jpg (gallery on https://www.minigolf-springe.de/die-minigolfanlage)
- `vorsfelde_netz_frame04.jpg` (1920x1080) - Video frame through the safety fence: ring with net bag on a post in the foreground, target circle below it, red ramp and player at the tee behind.
  - Source: page https://minigolf-vorsfelde.de/about/ -> video https://drive.google.com/file/d/13gxWZKvGRgGqs14kyR87dA1GoGahemMN/view (frame at t=4 s)

### Stated dimensions

- STATED (norm, vertical dimension, repeated here because it is the only stated height): "Netzring 50 cm im Durchmesser" and, in the English WMF/Norwegian copy, "distance between teeing-off level and lower edge of ring: 70 - 80 cm" (file `refs\norway_godeidrettsanlegg\Banemaal_minigolf.pdf`).
- STATED (minigolfbau.eu, https://www.minigolfbau.eu/miniatur-golf-hindernisse/): "Zielnetz ( Durchmesser 50cm. )", "Ring mit Ständer (Durchmesser 50cm.)", "Barriere ( 200/200cm. )", "Sicherheitsnetz (200/200cm.) ... Material: PE 1,3mm., die Masche 2,5cm.", and for the curved variant "Sicherheitsnetz (200/314cm.)".
- Ramp height: not stated anywhere.

### Shape (observed) and estimates

- OBSERVED: the lane is short; it ends with a wedge ramp whose ridge is the end of the lane. The ring stands free on a single post some distance behind, with a net bag hanging behind the ring; in Vorsfelde the target circle lies under/behind the ring. A safety net or fence stands behind everything. Harrislee uses a solid backboard with a ring opening instead.
- OBSERVED: the ramp is an inclined plane up to a sharp transverse ridge; the GFK version has inclined side facets and a steep back.
- ESTIMATE (ramp ridge height): 30-40 cm above the plate. Scale: lane width in `minigolfbau_sprungschanze.jpg` and `harrislee_netz.jpg` (ridge height about 0.35-0.45 x lane width). Low confidence.
- ESTIMATE (ramp length): about 1.0-1.2 m, i.e. slope about 17-20 degrees. Same scale, low confidence.

## Lane 8 - Gerade Bahn mit Zielkreisfenster

### Images (folder `lane08\`)

- `harrislee_fenster.jpg` (400x300) - Whole lane ("Das Fenster"): low wall with window across the lane at the target-circle entrance. Small; the lane also seems to have a level change mid-lane, so it may not be a pure straight lane.
  - Source: https://img.webme.com/pic/c/cobras-harrislee/bahn12.jpg (page https://www.minigolf-harrislee.de/Unsere-Anlage.htm)
- `minigolfbau_kreisfenster.jpg` (900x675) - Manufacturer photo ("Kreisfenster"), BEST for this lane: dark green upright plate across the lane exactly at the circle entrance with one central rectangular window; hole, repositioning line, rail and adjustable feet visible.
  - Source: https://16af6c9f59.clvaw-cdnwnd.com/e29ac4878528e2f7e55fddb9691c9b4a/200002410-6d7866d78a/minigolf%20Kreisfenster.jpg (minigolfbau.eu product photo; pages https://www.minigolfbau.eu/miniatur-golf-hindernisse/ and /minigolf-miniaturgolf/)

### Stated dimensions

- None found for the height of the barrier or window. (Window width is in the norm.)

### Shape (observed) and estimates

- OBSERVED: a thin upright plate across the full lane width exactly at the target-circle entrance, fixed to the rails at both ends, with one central rectangular window open at the bottom (`minigolfbau_kreisfenster.jpg`).
- ESTIMATE (barrier height): about 10-13 cm, i.e. roughly 2.5-3 x rail height. Scale: 90 cm lane width along the plate, corrected for the oblique view. Low confidence.
- ESTIMATE (window): about 12 cm wide (agrees with the norm range) and about 7-8 cm high. Same scale.
- Only two photos found for this lane, and the Harrislee one is small and may be a variant. This lane is the weakest covered.

## Lane 9 - Rohr

### Images (folder `lane09\`)

- `harrislee_rohrhuegel.jpg` (400x300) - Whole lane oblique: pale moulded pipe hill directly before the target circle. Small.
  - Source: https://img.webme.com/pic/c/cobras-harrislee/bahn02.jpg (page https://www.minigolf-harrislee.de/Unsere-Anlage.htm)
- `minigolfbau_rohrhuegel.jpg` (900x625) - Manufacturer photo: orange GFK pipe hill with a grey pipe sticking out horizontally towards the target circle; central valley leading to the pipe mouth.
  - Source: https://16af6c9f59.clvaw-cdnwnd.com/e29ac4878528e2f7e55fddb9691c9b4a/200002422-c3741c3745/Minigolf%20Rohrh%C3%BCgel.JPG (minigolfbau.eu product photo; pages https://www.minigolfbau.eu/miniatur-golf-hindernisse/ and /minigolf-miniaturgolf/)
- `solna_rohrhuegel_q.jpg` (4032x3024) - UNCERTAIN (probably the pipe hill seen from the tee, but could be another red moulded obstacle): red obstacle before the target circle with a player in front.
  - Source: https://www.flickr.com/photos/coupleputts/54191327291/ (original size; via https://acoupleofputts.com/2024/12/12/solna-bangolfklubb-eternit-minigolf-solna-sweden/)
- `uppsala_rohrhuegel.jpg` (4032x3024) - Flickr original: orange pipe hill at the circle entrance in oblique side view; hill length/height relative to the lane visible.
  - Source: https://www.flickr.com/photos/coupleputts/53124368093/ (original size; via https://acoupleofputts.com/2023/08/21/uppsala-bangolfklubb-eternit-miniaturegolf-uppsala-sweden/)
- `vorsfelde_rohr_frame10.jpg` (1920x1080) - Video frame, BEST: ball rolling up the red central valley towards the pipe mouth; worn side ridges, pipe exit over the circle; ball gives scale.
  - Source: page https://minigolf-vorsfelde.de/about/ -> video https://drive.google.com/file/d/1D91AuhOgvHnUYqzuwiiBn2U0tQ9iRJjG/view (frame at t=10 s)
- `vorsfelde_rohr_frame12.jpg` (1920x1080) - Video frame: whole hill plus target circle from the side/front.
  - Source: page https://minigolf-vorsfelde.de/about/ -> video https://drive.google.com/file/d/1D91AuhOgvHnUYqzuwiiBn2U0tQ9iRJjG/view (frame at t=12 s)

### Stated dimensions

- STATED (minigolfbau.eu, https://www.minigolfbau.eu/miniatur-golf-hindernisse/): "Miniatur Golf - Rohrhügel Hindernis ( ohne Rohr ) ... Rohr 60mm. rostfrei Stahl ... Rohr grauer Kunststoff ... Material Schichtpressstoff (GFK)". (Norm range for the pipe diameter is 4,5 - 6,5 cm.)
- Hill height and pipe length: not stated anywhere.

### Shape (observed) and estimates

- OBSERVED: a moulded hill across the full lane width directly before the target circle. A central V-shaped valley rises from lane level to the pipe mouth; the shoulders left and right of the valley are higher than the pipe mouth, so a ball that misses rolls back. The pipe is short, roughly horizontal, and sticks out of the back of the hill over the start of the target circle.
- ESTIMATE (pipe mouth height above the plate): about 15-20 cm; shoulders about 20-25 cm. Scale: ball (about 4 cm) and lane width in `vorsfelde_rohr_frame10.jpg`. Low confidence, no true side view found.
- ESTIMATE (hill length along the lane): about 1.0-1.3 m. Scale: lane width at the foot of the hill in the same frame.
- ESTIMATE (pipe): visible length behind the crest about 20-30 cm; inner diameter about 1.5 ball diameters, consistent with the stated 60 mm.

## General: Bande, plates, hole, lane height

### Images (folder `general\`)

- `commons_minigolf101_bande_ball.jpg` (1280x960) - Wikimedia Commons "Minigolf 101.jpg": ball and putter head on a lane seen exactly at surface level with the side rail behind. NOTE: lane system not stated on the file page; rail looks like a fibre-cement/concrete strip, so it may not be a steel-rail Eternit lane.
  - Source: https://commons.wikimedia.org/wiki/File:Minigolf_101.jpg (original file)

### Stated

- Plate thickness 15 mm: DMV Homologationsbestimmungen S 17 (01/2026) lists the approved product "Eterplan 15 mm" (https://www.minigolfsport.de/pdf/Download/Regelwerk/s17_homologationsbestimmungen%20f%C3%BCr%20bahnsysteme_2026-01_rot.pdf). minigolfbau.eu: "Wir benutzen Faserzementplatten der belgischen Firma EQUITONE - ETERBOARD - HD 15MM. ... 18 mm. Dicke auch auf Bestellung erhältlich" (https://www.minigolfbau.eu/minigolf-renovierung/).
- Plate sizes (same page): "Minigolf Platten 2a. 2500/895mm.", "Zielkreisplatte mit Loch 1390/1230mm.", also "1500/895mm.", "1000/895mm.", "700/895mm.", "625/895mm.".
- Frame / rail: "Die Rahmen der Minigolf Bahnen sind aus massivem L- Profilstahl (L60/30/5) hergestellt und mit T Querstreben (T30/30/3, T25/25/3) aus Stahl versteift. Die Konstruktion des Rahmens ist feuerverzinkt." (https://www.minigolfbau.eu/minigolf-miniaturgolf/). This is one manufacturer's construction, not a norm value.
- Norm S 17 names the permitted rail materials for Miniaturgolf as "Flacheisen/Faserzement" (flat steel or fibre cement), no height given.
- Tee plate (same manufacturer): stainless steel "500/400/2mm." or "200/400/2mm.".
- Rail height, hole diameter and lane height above ground: **not stated in any source I found**, including the norm texts available in `scratchpad\txt\`.

### Derived / estimated

- DERIVED (rail height): with an L60/30/5 frame, if the 15 mm plate rests on the inward 30 mm leg (5 mm thick), the rail stands 60 - 5 - 15 = about 40 mm above the playing surface and is 5 mm thick, square top edge. The seating of the plate is my assumption from the manufacturer's cross-section drawing (`refs\minigolfbau_eu\lane_crosssection.jpg`), not a stated fact. Photos agree: the rail is about one ball diameter high (`vorsfelde_rohr_frame10.jpg`, `vorsfelde_pyramiden_frame11.jpg`).
- ESTIMATE (hole diameter): about 12-13 cm from two photos (hole width compared with the 1.40 m circle: 0.09 x 1.40 m in `solna_bruecke_side.jpg`, 0.097 x 1.40 m in `minigolfbau_kreisfenster.jpg`). A ball lying in the hole in `vorsfelde_pyramiden_frame11.jpg` fills about one third of it. My unverified recollection is that the nominal value for Miniaturgolf is 10 cm; I found no source to confirm either figure, so treat it as 10-13 cm.
- OBSERVED (hole): plain round hole in the plate with a cup below, no raised lip; a scribed repositioning line runs 20 cm inside the circle rail.
- ESTIMATE (lane height above ground): the frame stands on threaded adjustable feet; underside of the frame about 3-8 cm above the paving, playing surface about 10-15 cm above the paving (`minigolfbau_kreisfenster.jpg`, `solna_doppelwelle_side.jpg`). Varies per site; some target circles sit on separate stands about 20 cm high (`vorsfelde_niere_frame13.jpg`).
- OBSERVED: the target-circle rail is the same profile bent to the 1.40 m circle; plate joints are butt joints, visible as fine lines; the tee area is marked by a scribed rectangle or a stainless tee plate.
