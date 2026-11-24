using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalkaboutDesktop;

// Lane coordinates: origin = centre of the tee end of the lane, on the (unraised) playing surface.
// x = lateral (right is +), y = up, z = along the lane towards the hole. Metres, degrees.
public class LaneDef
{
    public string name { get; set; } = "Lane";
    public string system { get; set; } = "";
    public string note { get; set; } = "";
    public float length { get; set; } = 6.25f;
    public float width { get; set; } = 0.9f;
    /// Diameter of the round target area at the end of the lane (Eternit 1.40, Beton 2.50). 0 = plain rectangle.
    public float targetCircle { get; set; } = 0f;
    /// Optional free outline polygon [[x, z], ...] instead of the straight rectangle (angled lanes like Winkel/Blitz).
    public float[][] outline { get; set; }
    /// Optional target circle position/size [x, z, diameter] (default: centred at the end of a straight lane).
    public float[] circle { get; set; }
    /// Cup centre height in lane coordinates; null = on the surface. Used for targets like the net or a bowl.
    public float? cupHeight { get; set; }
    /// Surface grid spacing in metres (must divide 0.12); finer for lanes with moulded obstacles.
    public float grid { get; set; } = 0.04f;
    /// false = no hole in the surface and no cup collider (the target is e.g. a net or bowl built as a part).
    public bool cupHole { get; set; } = true;
    /// For lanes without a hole: the ball counts as holed when it rests within this distance of `cup` (and near cupHeight).
    public float targetRadius { get; set; } = 0.25f;
    public float surfaceThickness { get; set; } = 0.05f;
    public string surfaceTag { get; set; } = "Green";
    public float bandHeight { get; set; } = 0.04f;   // ~35-40 mm above the plate (builder cross-section, estimate)
    public float bandThickness { get; set; } = 0.012f;  // steel strip; only its inner face matters for play
    public bool teeField { get; set; } = true;        // paint the Abschlagfeld (40 cm deep, 50 cm wide) at the lane start
    public bool teeSign { get; set; } = true;         // paint "BAHNEN BITTE NICHT BETRETEN" into the Abschlagfeld
    public bool circleLine { get; set; } = true;      // paint the repositioning line 20 cm inside the target circle
    public string bandTag { get; set; } = "Plastic";
    public bool bandAtStart { get; set; } = true;
    public bool bandAtEnd { get; set; } = true;
    public float[] surfaceColor { get; set; } = { 0.74f, 0.74f, 0.72f };   // light grey fibre cement / concrete
    public float[] bandColor { get; set; } = { 0.36f, 0.37f, 0.39f };      // steel
    public float[] obstacleColor { get; set; } = { 0.70f, 0.70f, 0.66f };
    // Ball/lane physics (see BallPhysics). Null = keep the current (global / live-tuned) value.
    public float? rollDecel { get; set; }      // m/s^2 constant rolling resistance
    public float? rollDrag { get; set; }       // 1/s speed-proportional braking
    public float bandBounce { get; set; } = 1.0f;     // physics-material bounciness of the borders (1.0 gives ~0.55 effective rebound for a rolling ball)
    public float obstacleBounce { get; set; } = 0.9f; // same for obstacles (~0.5 effective)
    public float[] tee { get; set; } = { 0f, 0.3f };   // x, z
    public float[] cup { get; set; } = { 0f, 5.5f };   // x, z
    /// Height profile of the playing surface (features are summed).
    public List<Feature> surface { get; set; } = new();
    public List<Part> parts { get; set; } = new();

    [JsonIgnore] public bool HasCircle => circle is { Length: >= 3 } ? circle[2] > 0 : targetCircle > 0;
    [JsonIgnore] public float CircleRadius => circle is { Length: >= 3 } ? circle[2] / 2f : targetCircle / 2f;
    [JsonIgnore] public float CircleX => circle is { Length: >= 3 } ? circle[0] : 0f;
    [JsonIgnore] public float CircleZ => circle is { Length: >= 3 } ? circle[1] : length - targetCircle / 2f;

    public static LaneDef Load(string path)
    {
        var opts = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
        };
        return JsonSerializer.Deserialize<LaneDef>(File.ReadAllText(path), opts);
    }
}

/// A height feature of the playing surface.
///   slope : rises linearly by `height` from z0 to z1 and stays there afterwards (inclines, raised ends)
///   wedge : rises linearly by `height` from z0 to z1, then drops back to 0 (Keil)
///   hump  : cosine bump of `height` between z0 and z1, 0 outside (waves, bridge)
///   dome  : round cosine hill of `height` and `radius` around (x, z) (Mittelhügel, Vulkan, Zielhügel)
///   ramp  : like wedge but `curve` > 1 makes it concave (flat start, steep end); combined by maximum with plateaus
///   plateau: area inside `points` raised to `height` (sharp edges); combined by maximum with ramps
/// Optional x0/x1 limit a feature laterally (e.g. a ramp only 30 cm wide).
public class Feature
{
    public float[][] points { get; set; }
    public float curve { get; set; } = 1f;
    public string type { get; set; } = "slope";
    public float z0 { get; set; }
    public float z1 { get; set; }
    public float height { get; set; }
    public float x { get; set; }
    public float z { get; set; }
    public float radius { get; set; }
    public float? x0 { get; set; }
    public float? x1 { get; set; }
    public float? peak { get; set; }     // hump: z of the crest if it is not in the middle
    public float[] a { get; set; }       // halfcone: end at the Bande [x, half chord, height]
    public float[] b { get; set; }       // halfcone: free end [x, half chord, height]
    public float r1 { get; set; }        // vulkan: outer radius of the plateau rim
    public float width { get; set; }     // vulkan: inner width of the chute
    public float wall { get; set; } = 0.035f; // vulkan: chute wall height above the chute floor
    public float rim { get; set; } = 0.035f;  // vulkan: plateau rim height above the plateau floor
    public float[] color { get; set; }   // surface colour of this feature (GRP obstacles); null = lane plate colour
}

public class Part
{
    public string name { get; set; } = "part";
    /// box | cylinder | prism (vertical extrusion of convex `points`) | pyramid (base `points` + `apex` at `height`)
    /// | frustum (base `points`, smaller `top` outline at `height`) | tube (open pipe from `a` to `b`)
    /// | loop (looping channel starting at `pos`, `radius`, `width`, wall `height`, `shift`, `entry`, `exit`)
    /// | lathe (`profile` revolved around a vertical or lengthwise axis through `pos`; y of pos above the surface)
    /// | wall (along `points`) | arc (wall along a circle arc) | mesh (OBJ `file`, placed at `pos`/`rot`, y absolute)
    public string shape { get; set; } = "box";
    public float[] pos { get; set; } = { 0, 0, 0 };   // box/cylinder centre; y is measured from the surface at (x, z)
    public float[] size { get; set; } = { 0.1f, 0.1f, 0.1f }; // box: x,y,z extents; cylinder: diameter, height
    public float[] rot { get; set; } = { 0, 0, 0 };   // euler degrees (x = pitch, y = yaw, z = roll)
    public float[][] points { get; set; }              // prism outline / wall polyline, [x, z] pairs
    public float height { get; set; } = 0.06f;         // prism / wall / arc height above the surface
    public float thickness { get; set; } = 0.03f;      // wall / arc thickness
    public float[] apex { get; set; }                  // pyramid apex [x, z] (default: centroid of points)
    public float[][] top { get; set; }                 // frustum: top outline [[x, z], ...] (same vertex count as points)
    public float[] a { get; set; }                     // tube: start [x, y, z] (y above the surface at that point)
    public float[] b { get; set; }                     // tube: end [x, y, z]
    public float diameter { get; set; }                // tube: inner diameter
    public float width { get; set; } = 0.18f;          // loop: channel width
    public float shift { get; set; } = 0.25f;          // loop: lateral offset between entry and exit
    public float entry { get; set; } = 0.4f;           // loop: straight entry length before the loop
    public float exit { get; set; } = 0.5f;            // loop: straight exit length after the loop
    public bool cover { get; set; } = false;           // loop: close the channel over the upper half (the ball collides with it - off)
    public float[][] profile { get; set; }             // lathe: [[radius, y], ...] revolved around the axis
    public string axis { get; set; } = "y";            // lathe: "y" (vertical) or "z" (along the lane)
    public float gapFrom { get; set; }                 // lathe: leave out angles gapFrom..gapTo (deg) e.g. a bowl entrance
    public float gapTo { get; set; }
    public float? bounce { get; set; }                 // per-part bounciness override (e.g. a net: 0.1)
    public string file { get; set; }                   // mesh: OBJ file (relative to the lanes folder), metres, Y up, Z along the lane
    public bool concave { get; set; }                  // prism: outline is not convex - use an exact (non-convex) collider
    public float? baseY { get; set; }                  // prism: slab starting this far above the surface (a roof)
    public float alpha { get; set; } = 1f;             // < 1: see-through material (nets)
    public bool surface { get; set; }                  // mesh: a moulding the ball rolls on - same friction as the lane plate
    public bool slick { get; set; }                    // mesh: smooth moulded channel (low friction against the ball)
    public bool convex { get; set; }                   // mesh: use a convex collider
    public float[] center { get; set; }                // arc centre [x, z]
    public float radius { get; set; }                  // arc radius (to the wall centre line)
    public float from { get; set; }                    // arc start angle (deg, 0 = +x/right, 90 = +z/towards the hole)
    public float to { get; set; }                      // arc end angle
    public string tag { get; set; } = "Plastic";
    public string material { get; set; } = "";       // "green" to reuse the surface look, else obstacle look
    public float[] color { get; set; }                 // optional r,g,b 0..1 tint
    [JsonIgnore] public bool IsCylinder => shape == "cylinder";
}
