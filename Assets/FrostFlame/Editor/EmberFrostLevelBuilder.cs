using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FrostFlame.EditorTools
{
    /// <summary>
    /// One-click builder for "Ember &amp; Frost - Level 1".
    /// 1) Generates recoloured sprites from the BayatGames Free Platform Game Assets
    ///    (fire/ice heroes, lava/poison pools, gems, chests, tiles) into Assets/FrostFlame/Art/Generated.
    /// 2) Builds and saves the level scene and adds it to Build Settings.
    /// Menu: Tools > Ember &amp; Frost > Build Level 1
    /// </summary>
    public static class EmberFrostLevelBuilder
    {
        const string Src = "Assets/BayatGames/Free Platform Game Assets/";
        const string Root = "Assets/FrostFlame";
        const string GenDir = Root + "/Art/Generated";
        const string ScenePath = Root + "/Scenes/EmberAndFrost_Level1.unity";

        const int W = 32, H = 17;

        // sorting orders
        const int ORDER_BG = -100, ORDER_DECOR = -20, ORDER_PROPS = 5, ORDER_TILES = 0,
                  ORDER_GEM = 8, ORDER_PLAYER = 10, ORDER_LIQUID = 20;

        struct Pending { public string path; public float ppu; public Vector2 pivot; }
        static readonly List<Pending> pending = new List<Pending>();

        [MenuItem("Tools/Ember & Frost/Build Level 1")]
        public static void BuildAll()
        {
            GenerateArt();
            BuildScene();
        }

        [MenuItem("Tools/Ember & Frost/Regenerate Art Only")]
        public static void GenerateArt()
        {
            Directory.CreateDirectory(GenDir);
            Directory.CreateDirectory(Root + "/Scenes");
            pending.Clear();

            // --- tiles (Winter set) -> 1 unit per tile
            string tiles = Src + "Tiles/2D Tiles ( Update 1.9 )/Winter/128x128/";
            var top = Load(tiles + "GrassMid.png");
            Save(top, "TileTop", 128, Center);
            Save(Load(tiles + "GrassLeft.png"), "TileTopL", 128, Center);
            Save(Load(tiles + "GrassRight.png"), "TileTopR", 128, Center);
            Save(Load(tiles + "Grass.png"), "TileBlock", 128, Center);
            Save(MakeDirt(top), "TileDirt", 128, Center);

            // --- heroes (2x character source parts)
            string parts = Src + "Character/Character Source/png/2x/";
            string[] partFiles = { "Body", "Eye", "Foot", "LeftHand&Arm", "RightHand&Arm" };
            string[] partNames = { "Body", "Eye", "Foot", "HandL", "HandR" };
            for (int i = 0; i < partFiles.Length; i++)
            {
                var t = Load(parts + partFiles[i] + ".png");
                Save(HueShift(t, 0.035f, 1.05f, 1.05f), "Ember_" + partNames[i], 100, Center);
                Save(HueShift(t, 0.56f, 0.95f, i == 1 ? 0.8f : 1.08f), "Frost_" + partNames[i], 100, Center);
            }

            // --- pools (16 frame water animation)
            string water = Src + "Water Animation/2D view ( Update 1.9 )/128x128/";
            for (int i = 1; i <= 16; i++)
            {
                var t = Load(water + i + "-1x.png");
                Save(t, "Water_" + i, 128, Center);
                Save(HueShift(t, -0.49f, 1.25f, 1.05f, 0.05f), "Lava_" + i, 128, Center);
                Save(HueShift(t, -0.25f, 1.1f, 0.95f, 0.05f), "Poison_" + i, 128, Center);
            }

            // --- gems from the first coin frame
            var coinSheet = Load(Src + "Coin Animation/png/2048x256.png");
            var coin = Crop(coinSheet, 62, 60, 140, 140);
            Save(HueShift(coin, -0.115f, 1.1f, 1f), "GemFire", 128, Center);
            Save(HueShift(coin, 0.43f, 1.1f, 1f), "GemIce", 128, Center);

            // --- exit chests (6 opening frames)
            for (int i = 1; i <= 6; i++)
            {
                var t = Load(Src + "Chests/Chest/png/Separate/1x/" + i + "-1x.png");
                Save(HueShift(t, -0.045f, 1.15f, 1f), "ChestFire_" + i, 128, new Vector2(0.5f, 0f));
                Save(HueShift(t, 0.5f, 0.9f, 1.05f), "ChestIce_" + i, 128, new Vector2(0.5f, 0f));
            }

            // --- simple procedural shapes
            Save(MakeGlow(64), "Glow", 64, Center);
            Save(MakeDot(32), "Dot", 32, Center);
            Save(MakePanel(64, 32, 8), "Panel", 64, Center);

            AssetDatabase.Refresh();
            foreach (var p in pending) ConfigureSprite(p.path, p.ppu, p.pivot);
            pending.Clear();

            // physics materials
            if (AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(Root + "/Art/Slippery.physicsMaterial2D") == null)
                AssetDatabase.CreateAsset(new PhysicsMaterial2D("Slippery") { friction = 0f, bounciness = 0f }, Root + "/Art/Slippery.physicsMaterial2D");
            if (AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(Root + "/Art/Block.physicsMaterial2D") == null)
                AssetDatabase.CreateAsset(new PhysicsMaterial2D("Block") { friction = 0.1f, bounciness = 0f }, Root + "/Art/Block.physicsMaterial2D");
            AssetDatabase.SaveAssets();
        }

        // =====================================================================================
        //  Scene
        // =====================================================================================

        static char[,] grid;

        static void Fill(int x0, int x1, int y0, int y1, char c)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    grid[x, y] = c;
        }

        static bool Solid(int x, int y)
        {
            if (x < 0 || x >= W || y < 0 || y >= H) return true;
            return grid[x, y] != '.';
        }

        static void LayoutLevel()
        {
            grid = new char[W, H];
            Fill(0, W - 1, 0, H - 1, '.');

            // outer frame
            Fill(0, 0, 0, 16, '#'); Fill(31, 31, 0, 16, '#');
            Fill(0, 31, 16, 16, '#'); Fill(0, 31, 0, 0, '#');

            // floor 1 (stand y=1): lava, water, poison
            Fill(7, 8, 0, 0, 'L'); Fill(11, 12, 0, 0, 'W'); Fill(16, 17, 0, 0, 'P');

            // floor 2 slab (y=4) - elevator shaft at x=27..29
            Fill(1, 26, 4, 4, '#'); Fill(30, 30, 4, 4, '#');
            Fill(11, 12, 4, 4, 'W'); Fill(17, 18, 4, 4, 'L');
            // tall block on floor 2 (needs the push block to climb)
            Fill(1, 2, 5, 7, '#');

            // floor 3 slab (y=8) - opening at x=1..4 above the tall block and the push-block spot
            Fill(5, 30, 8, 8, '#');
            Fill(13, 18, 8, 8, 'P'); Fill(22, 23, 8, 8, 'W'); Fill(25, 26, 8, 8, 'L');
            // step up to floor 4
            Fill(29, 30, 9, 10, '#');

            // floor 4 slab (y=12) - opening at x=29..30
            Fill(1, 28, 12, 12, '#');
            Fill(15, 16, 12, 12, 'W'); Fill(20, 21, 12, 12, 'L');
        }

        public static void BuildScene()
        {
            LayoutLevel();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------------- camera + background
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.20f, 0.19f, 0.33f);
            camGo.transform.position = new Vector3(16f, 8.8f, -10f);
            camGo.AddComponent<AudioListener>();

            var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Src + "Backgrounds/New Background ( Update 1.9 )/png/1920x1080/background/NewBackgraound 1920x1080 (2).png");
            if (bgSprite != null)
            {
                var bg = MakeSprite("Background", null, bgSprite, new Vector2(16f, 8.8f), ORDER_BG, new Color(0.55f, 0.52f, 0.78f));
                Vector2 size = bgSprite.bounds.size;
                bg.transform.localScale = new Vector3(33.5f / size.x, 19f / size.y, 1f);
            }

            // ---------------- tiles + colliders
            var level = new GameObject("Level").transform;
            BuildTiles(level);
            BuildColliders(level);
            BuildLiquids(level);

            // ---------------- decor
            var decor = new GameObject("Decor").transform;
            decor.SetParent(level);
            string details = Src + "Environments/New Details ( Update 1.9 )/8x/";
            MakeDecor(decor, details + "Winter Tree.png", new Vector2(11.5f, 13f), 2.9f);
            MakeDecor(decor, details + "Bush.png", new Vector2(4.5f, 1f), 0.7f);
            MakeDecor(decor, details + "Bush.png", new Vector2(21f, 5f), 0.6f);
            MakeDecor(decor, details + "Bush.png", new Vector2(9f, 9f), 0.6f);
            MakeDecor(decor, details + "Bush.png", new Vector2(25.5f, 13f), 0.7f);
            MakeDecor(decor, details + "Flower-1.png", new Vector2(14.5f, 1f), 0.5f);
            MakeDecor(decor, details + "Flower-2.png", new Vector2(7.5f, 5f), 0.5f);
            MakeDecor(decor, details + "Flower-3.png", new Vector2(27.5f, 9f), 0.5f);

            // ---------------- mechanisms
            var mech = new GameObject("Mechanisms").transform;
            mech.SetParent(level);
            Color yellow = new Color(1f, 0.85f, 0.25f);
            Color purple = new Color(0.8f, 0.45f, 1f);

            var plate1 = MakePlate(mech, "Plate_Floor1", new Vector2(23.5f, 1f), yellow);
            var plate2 = MakePlate(mech, "Plate_Floor2", new Vector2(24.5f, 5f), yellow);
            MakePlatform(mech, "Elevator", new Vector2(28.5f, 1.15f), 2.9f, new Vector2(0f, 3.7f), 3f,
                MovingPlatform.Mode.Hold, new Activator[] { plate1, plate2 }, yellow);

            var lever = MakeLever(mech, "Lever_Floor3", new Vector2(6.5f, 9f), purple);
            MakePlatform(mech, "Ferry", new Vector2(11f, 9.35f), 2f, new Vector2(9f, 0f), 2.6f,
                MovingPlatform.Mode.PingPong, new Activator[] { lever }, purple);

            MakePushBlock(mech, new Vector2(22.5f, 5.5f));

            // ---------------- gems
            var gems = new GameObject("Gems").transform;
            gems.SetParent(level);
            MakeGem(gems, Element.Ice, new Vector2(8f, 2.7f));
            MakeGem(gems, Element.Fire, new Vector2(12f, 2.7f));
            MakeGem(gems, Element.Ice, new Vector2(12f, 5.7f));
            MakeGem(gems, Element.Fire, new Vector2(18f, 5.7f));
            MakeGem(gems, Element.Fire, new Vector2(14.5f, 10.5f));
            MakeGem(gems, Element.Ice, new Vector2(17.5f, 10.5f));
            MakeGem(gems, Element.Ice, new Vector2(16f, 13.7f));
            MakeGem(gems, Element.Fire, new Vector2(21f, 13.7f));

            // ---------------- exits
            var exits = new GameObject("Exits").transform;
            exits.SetParent(level);
            var fireExit = MakeExit(exits, Element.Fire, new Vector2(4.5f, 13f));
            var iceExit = MakeExit(exits, Element.Ice, new Vector2(7.5f, 13f));

            // ---------------- heroes
            var ember = MakePlayer(Element.Fire, new Vector2(2.5f, 1.6f), Key.A, Key.D, Key.W, Key.S);
            var frost = MakePlayer(Element.Ice, new Vector2(4f, 1.6f), Key.LeftArrow, Key.RightArrow, Key.UpArrow, Key.DownArrow);

            // ---------------- game manager
            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            gm.ember = ember; gm.frost = frost;
            gm.emberExit = fireExit; gm.frostExit = iceExit;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log("[Ember & Frost] Level built: " + ScenePath);
        }

        // ------------------------------------------------------------------ tiles

        static void BuildTiles(Transform level)
        {
            var parent = new GameObject("Tiles").transform;
            parent.SetParent(level);
            var sTop = GenSprite("TileTop");
            var sTopL = GenSprite("TileTopL");
            var sTopR = GenSprite("TileTopR");
            var sDirt = GenSprite("TileDirt");

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (!Solid(x, y)) continue;
                    char c = grid[x, y];
                    bool exposedTop = !Solid(x, y + 1) && c == '#';
                    Sprite s = sDirt;
                    if (exposedTop)
                    {
                        bool openL = !Solid(x - 1, y), openR = !Solid(x + 1, y);
                        s = openL && !openR ? sTopL : openR && !openL ? sTopR : sTop;
                    }
                    var sr = MakeSprite("T" + x + "_" + y, parent, s, new Vector2(x + 0.5f, y + 0.5f), ORDER_TILES, Color.white);
                    sr.transform.localScale = new Vector3(1.01f, 1.01f, 1f);
                }
        }

        static void BuildColliders(Transform level)
        {
            var go = new GameObject("Solids");
            go.transform.SetParent(level);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var comp = go.AddComponent<CompositeCollider2D>();
            comp.geometryType = CompositeCollider2D.GeometryType.Polygons;

            // horizontal runs per row, merged vertically where identical
            var used = new bool[W, H];
            for (int y = 0; y < H; y++)
            {
                int x = 0;
                while (x < W)
                {
                    if (!Solid(x, y) || used[x, y]) { x++; continue; }
                    int x1 = x;
                    while (x1 + 1 < W && Solid(x1 + 1, y) && !used[x1 + 1, y]) x1++;
                    int y1 = y;
                    while (y1 + 1 < H && RunMatches(x, x1, y1 + 1, used)) y1++;
                    for (int yy = y; yy <= y1; yy++) for (int xx = x; xx <= x1; xx++) used[xx, yy] = true;

                    var box = go.AddComponent<BoxCollider2D>();
                    box.size = new Vector2(x1 - x + 1, y1 - y + 1);
                    box.offset = new Vector2((x + x1 + 1) * 0.5f, (y + y1 + 1) * 0.5f);
                    box.compositeOperation = Collider2D.CompositeOperation.Merge;
                    x = x1 + 1;
                }
            }
        }

        static bool RunMatches(int x0, int x1, int y, bool[,] used)
        {
            for (int x = x0; x <= x1; x++) if (!Solid(x, y) || used[x, y]) return false;
            // the run must not continue beyond its ends on this row
            if (x0 - 1 >= 0 && Solid(x0 - 1, y) && !used[x0 - 1, y]) return false;
            if (x1 + 1 < W && Solid(x1 + 1, y) && !used[x1 + 1, y]) return false;
            return true;
        }

        static void BuildLiquids(Transform level)
        {
            var parent = new GameObject("Pools").transform;
            parent.SetParent(level);
            var water = Frames("Water_"); var lava = Frames("Lava_"); var poison = Frames("Poison_");

            for (int y = 0; y < H; y++)
            {
                int x = 0;
                while (x < W)
                {
                    char c = grid[x, y];
                    if (c != 'L' && c != 'W' && c != 'P') { x++; continue; }
                    int x1 = x;
                    while (x1 + 1 < W && grid[x1 + 1, y] == c) x1++;
                    int len = x1 - x + 1;

                    string kind = c == 'L' ? "Lava" : c == 'W' ? "Water" : "Poison";
                    var pool = new GameObject(kind + "Pool_" + x + "_" + y);
                    pool.transform.SetParent(parent);
                    pool.transform.position = new Vector3(x + len * 0.5f, y + 1f, 0f);
                    var trig = pool.AddComponent<BoxCollider2D>();
                    trig.isTrigger = true;
                    trig.size = new Vector2(len - 0.3f, 0.35f);
                    trig.offset = new Vector2(0f, 0.175f);
                    var liquid = pool.AddComponent<Liquid>();
                    liquid.safeFor = c == 'L' ? Element.Fire : c == 'W' ? Element.Ice : Element.Both;

                    var frames = c == 'L' ? lava : c == 'W' ? water : poison;
                    for (int i = 0; i < len; i++)
                    {
                        var sr = MakeSprite("Surface" + i, pool.transform, frames[0], new Vector2(x + i + 0.5f, y + 0.84f), ORDER_LIQUID, new Color(1f, 1f, 1f, 0.93f));
                        sr.transform.localScale = new Vector3(1.03f, 0.6f, 1f);
                        var anim = sr.gameObject.AddComponent<SpriteFrameAnimator>();
                        anim.frames = frames;
                        anim.fps = 14f;
                        anim.randomStart = false;
                    }
                    // soft glow over the pool so it reads at a glance
                    Color glowCol = c == 'L' ? ElementColors.Fire : c == 'W' ? ElementColors.Ice : ElementColors.Poison;
                    glowCol.a = 0.35f;
                    var g = MakeSprite("Glow", pool.transform, GenSprite("Glow"), new Vector2(x + len * 0.5f, y + 1.1f), ORDER_DECOR + 1, glowCol);
                    g.transform.localScale = new Vector3(len * 1.1f, 1.1f, 1f);
                    x = x1 + 1;
                }
            }
        }

        // ------------------------------------------------------------------ props

        static void MakeDecor(Transform parent, string path, Vector2 groundPos, float height)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) return;
            var sr = MakeSprite(Path.GetFileNameWithoutExtension(path), parent, s, groundPos, ORDER_DECOR, new Color(0.9f, 0.9f, 1f));
            float k = height / s.bounds.size.y;
            sr.transform.localScale = new Vector3(k, k, 1f);
            // align bottom of sprite with the ground
            float bottom = s.bounds.min.y * k;
            sr.transform.position = new Vector3(groundPos.x, groundPos.y - bottom - 0.05f, 0f);
        }

        static PressurePlate MakePlate(Transform parent, string name, Vector2 pos, Color lampColor)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = pos;
            var basePanel = MakeSprite("Base", root.transform, GenSprite("Panel"), pos + new Vector2(0f, 0.06f), ORDER_PROPS + 1, new Color(0.3f, 0.3f, 0.36f));
            basePanel.transform.localScale = new Vector3(1.3f, 0.24f, 1f);
            var button = MakeSprite("Button", root.transform, GenSprite("Panel"), pos + new Vector2(0f, 0.17f), ORDER_PROPS, new Color(0.35f, 0.35f, 0.4f));
            button.transform.localScale = new Vector3(0.9f, 0.3f, 1f);
            var plate = root.AddComponent<PressurePlate>();
            plate.button = button.transform;
            plate.lamp = button;
            plate.onColor = lampColor;
            return plate;
        }

        static Lever MakeLever(Transform parent, string name, Vector2 pos, Color lampColor)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = pos;
            var trig = root.AddComponent<BoxCollider2D>();
            trig.isTrigger = true;
            trig.size = new Vector2(1.2f, 1.2f);
            trig.offset = new Vector2(0f, 0.6f);

            var handle = new GameObject("Handle").transform;
            handle.SetParent(root.transform);
            handle.localPosition = new Vector3(0f, 0.18f, 0f);
            handle.localRotation = Quaternion.Euler(0f, 0f, 35f);
            var stick = MakeSprite("Stick", handle, GenSprite("Panel"), Vector2.zero, ORDER_PROPS, new Color(0.75f, 0.7f, 0.65f));
            stick.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            stick.transform.localScale = new Vector3(0.12f, 1.3f, 1f);
            var knob = MakeSprite("Knob", handle, GenSprite("Dot"), Vector2.zero, ORDER_PROPS + 1, new Color(0.35f, 0.35f, 0.4f));
            knob.transform.localPosition = new Vector3(0f, 0.66f, 0f);
            knob.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
            var basePanel = MakeSprite("Base", root.transform, GenSprite("Panel"), pos + new Vector2(0f, 0.12f), ORDER_PROPS + 2, new Color(0.3f, 0.3f, 0.36f));
            basePanel.transform.localScale = new Vector3(0.8f, 0.5f, 1f);

            var lever = root.AddComponent<Lever>();
            lever.handle = handle;
            lever.lamp = knob;
            lever.onColor = lampColor;
            return lever;
        }

        static MovingPlatform MakePlatform(Transform parent, string name, Vector2 pos, float width, Vector2 offset, float speed,
            MovingPlatform.Mode mode, Activator[] activators, Color lampColor)
        {
            // rail showing the path
            Vector2 mid = pos + offset * 0.5f;
            var rail = MakeSprite(name + "_Rail", parent, GenSprite("Panel"), mid, ORDER_DECOR + 2, new Color(lampColor.r, lampColor.g, lampColor.b, 0.22f));
            rail.transform.localScale = offset.y != 0f
                ? new Vector3(0.18f, (Mathf.Abs(offset.y) + 0.3f) * 2f, 1f)
                : new Vector3(Mathf.Abs(offset.x) + width, 0.18f, 1f);

            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = pos;
            var rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var col = root.AddComponent<BoxCollider2D>();
            col.size = new Vector2(width, 0.3f);

            var body = MakeSprite("Body", root.transform, GenSprite("Panel"), pos, ORDER_PROPS, new Color(0.72f, 0.75f, 0.85f));
            body.transform.localScale = new Vector3(width, 0.6f, 1f);
            var lamp = MakeSprite("Lamp", root.transform, GenSprite("Dot"), pos, ORDER_PROPS + 1, new Color(0.35f, 0.35f, 0.4f));
            lamp.transform.localScale = new Vector3(0.2f, 0.2f, 1f);

            var mp = root.AddComponent<MovingPlatform>();
            mp.mode = mode;
            mp.activators = activators;
            mp.offsetToB = offset;
            mp.speed = speed;
            mp.lamp = lamp;
            mp.onColor = lampColor;
            return mp;
        }

        static void MakePushBlock(Transform parent, Vector2 pos)
        {
            var go = new GameObject("PushBlock");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.mass = 2.5f;
            rb.freezeRotation = true;
            rb.gravityScale = 3f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.96f, 0.96f);
            col.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(Root + "/Art/Block.physicsMaterial2D");
            go.AddComponent<PushBlock>();
            var sr = MakeSprite("Art", go.transform, GenSprite("TileBlock"), pos, ORDER_PROPS, new Color(0.85f, 0.9f, 1f));
            sr.transform.localScale = new Vector3(0.98f, 0.98f, 1f);
        }

        static void MakeGem(Transform parent, Element e, Vector2 pos)
        {
            var go = new GameObject((e == Element.Fire ? "EmberGem" : "FrostGem"));
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.3f;
            var glowCol = ElementColors.Of(e); glowCol.a = 0.45f;
            var glow = MakeSprite("Glow", go.transform, GenSprite("Glow"), pos, ORDER_GEM - 1, glowCol);
            glow.transform.localScale = Vector3.one * 1.1f;
            var art = MakeSprite("Art", go.transform, GenSprite(e == Element.Fire ? "GemFire" : "GemIce"), pos, ORDER_GEM, Color.white);
            art.transform.localScale = Vector3.one * 0.55f;
            var gem = go.AddComponent<Gem>();
            gem.element = e;
            gem.art = art.transform;
        }

        static ExitDoor MakeExit(Transform parent, Element e, Vector2 pos)
        {
            var go = new GameObject(e == Element.Fire ? "EmberChest" : "FrostChest");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            string prefix = e == Element.Fire ? "ChestFire_" : "ChestIce_";
            var frames = new Sprite[6];
            for (int i = 0; i < 6; i++) frames[i] = GenSprite(prefix + (i + 1));

            var glowCol = ElementColors.Of(e); glowCol.a = 0.5f;
            var glow = MakeSprite("Glow", go.transform, GenSprite("Glow"), pos + new Vector2(0f, 0.6f), ORDER_PROPS - 1, glowCol);
            glow.transform.localScale = Vector3.one * 2.4f;
            var chest = MakeSprite("Chest", go.transform, frames[0], pos, ORDER_PROPS, Color.white);
            chest.transform.localScale = new Vector3(0.95f, 0.95f, 1f);
            var icon = MakeSprite("Icon", go.transform, GenSprite(e == Element.Fire ? "GemFire" : "GemIce"), pos + new Vector2(0f, 1.75f), ORDER_PROPS, Color.white);
            icon.transform.localScale = Vector3.one * 0.4f;

            var door = go.AddComponent<ExitDoor>();
            door.element = e;
            door.chest = chest;
            door.openFrames = frames;
            return door;
        }

        static PlayerController MakePlayer(Element e, Vector2 pos, Key left, Key right, Key jump, Key use)
        {
            string p = e == Element.Fire ? "Ember_" : "Frost_";
            var go = new GameObject(e == Element.Fire ? "Ember" : "Frost");
            go.transform.position = pos;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.mass = 1f;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.62f, 0.84f);
            col.edgeRadius = 0.04f;
            col.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(Root + "/Art/Slippery.physicsMaterial2D");

            var pc = go.AddComponent<PlayerController>();
            pc.element = e;
            pc.leftKey = left; pc.rightKey = right; pc.jumpKey = jump; pc.interactKey = use;

            // visual hierarchy
            var visualRoot = new GameObject("Visual").transform;
            visualRoot.SetParent(go.transform, false);
            visualRoot.localPosition = new Vector3(0f, 0.02f, 0f);
            visualRoot.localScale = Vector3.one * 0.6f;

            var glowCol = ElementColors.Of(e); glowCol.a = 0.4f;
            var glow = LocalSprite("Glow", visualRoot, GenSprite("Glow"), Vector2.zero, ORDER_PLAYER - 2, glowCol);

            var rig = new GameObject("Rig").transform;
            rig.SetParent(visualRoot, false);
            var handL = LocalSprite("HandL", rig, GenSprite(p + "HandL"), new Vector2(-0.6f, -0.08f), ORDER_PLAYER, Color.white);
            var handR = LocalSprite("HandR", rig, GenSprite(p + "HandR"), new Vector2(0.6f, -0.08f), ORDER_PLAYER, Color.white);
            var footL = LocalSprite("FootL", rig, GenSprite(p + "Foot"), new Vector2(-0.28f, -0.62f), ORDER_PLAYER, Color.white);
            var footR = LocalSprite("FootR", rig, GenSprite(p + "Foot"), new Vector2(0.3f, -0.62f), ORDER_PLAYER, Color.white);
            var body = LocalSprite("Body", rig, GenSprite(p + "Body"), Vector2.zero, ORDER_PLAYER + 1, Color.white);
            var eyeA = LocalSprite("EyeA", rig, GenSprite(p + "Eye"), new Vector2(0.1f, 0.16f), ORDER_PLAYER + 2, Color.white);
            var eyeB = LocalSprite("EyeB", rig, GenSprite(p + "Eye"), new Vector2(0.42f, 0.13f), ORDER_PLAYER + 2, Color.white);

            var vis = go.AddComponent<PlayerVisual>();
            vis.controller = pc;
            vis.rig = rig;
            vis.body = body.transform;
            vis.eyeA = eyeA.transform; vis.eyeB = eyeB.transform;
            vis.footL = footL.transform; vis.footR = footR.transform;
            vis.handL = handL.transform; vis.handR = handR.transform;
            vis.glow = glow;
            vis.aura = Aura(visualRoot, e);
            vis.deathBurst = Burst(go.transform, e);
            pc.visual = vis;
            return pc;
        }

        // ------------------------------------------------------------------ particles

        static Material ParticleMat()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
            if (m != null) return m;
            string path = Root + "/Art/Particle.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Sprites/Default"));
                m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GenDir + "/Dot.png");
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        static ParticleSystem Aura(Transform parent, Element e)
        {
            var go = new GameObject("Aura");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, e == Element.Fire ? 0.5f : 0.3f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            Color c = ElementColors.Of(e);
            main.startColor = new ParticleSystem.MinMaxGradient(c, Color.Lerp(c, Color.white, 0.6f));
            main.gravityModifier = e == Element.Fire ? -0.25f : 0.08f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            main.playOnAwake = true;
            var em = ps.emission; em.rateOverTime = 16f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.35f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMat();
            r.sortingOrder = ORDER_PLAYER + 3;
            return ps;
        }

        static ParticleSystem Burst(Transform parent, Element e)
        {
            var go = new GameObject("DeathBurst");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.28f);
            main.startColor = ElementColors.Of(e);
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.2f;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMat();
            r.sortingOrder = ORDER_LIQUID + 2;
            return ps;
        }

        // ------------------------------------------------------------------ helpers (scene)

        static SpriteRenderer MakeSprite(string name, Transform parent, Sprite s, Vector2 worldPos, int order, Color color)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, true);
            go.transform.position = worldPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            sr.color = color;
            return sr;
        }

        static SpriteRenderer LocalSprite(string name, Transform parent, Sprite s, Vector2 localPos, int order, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            sr.color = color;
            return sr;
        }

        static Sprite GenSprite(string name)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(GenDir + "/" + name + ".png");
            if (s == null) Debug.LogWarning("[Ember & Frost] Missing generated sprite " + name + " - run Regenerate Art.");
            return s;
        }

        static Sprite[] Frames(string prefix)
        {
            var list = new Sprite[16];
            for (int i = 0; i < 16; i++) list[i] = GenSprite(prefix + (i + 1));
            return list;
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(path, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != path) scenes.Add(s);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Renders the main camera (no IMGUI HUD) to a PNG. Handy for previews.</summary>
        public static void CaptureCamera(string path, int width = 1920, int height = 1080)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var rt = new RenderTexture(width, height, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            cam.targetTexture = prev;
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        // ------------------------------------------------------------------ helpers (textures)

        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        static Texture2D Load(string path)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(path));
            return t;
        }

        static void Save(Texture2D t, string name, float ppu, Vector2 pivot)
        {
            string path = GenDir + "/" + name + ".png";
            File.WriteAllBytes(path, t.EncodeToPNG());
            pending.Add(new Pending { path = path, ppu = ppu, pivot = pivot });
        }

        static void ConfigureSprite(string path, float ppu, Vector2 pivot)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = ppu;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = pivot;
            s.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();
        }

        static Texture2D HueShift(Texture2D src, float shift, float satMul, float valMul, float minSat = 0.12f)
        {
            var px = src.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a <= 0f) continue;
                Color.RGBToHSV(c, out float h, out float s, out float v);
                if (s < minSat) continue;
                h = Mathf.Repeat(h + shift, 1f);
                s = Mathf.Clamp01(s * satMul);
                v = Mathf.Clamp01(v * valMul);
                var n = Color.HSVToRGB(h, s, v);
                n.a = c.a;
                px[i] = n;
            }
            var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static Texture2D Crop(Texture2D src, int x, int y, int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels(src.GetPixels(x, y, w, h));
            t.Apply();
            return t;
        }

        static Texture2D MakeDirt(Texture2D top)
        {
            // the lower half of the snowy tile is plain earth - tile it to fill the block
            int w = top.width, h = top.height, band = h / 2;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    t.SetPixel(x, y, top.GetPixel(x, y % band));
            t.Apply();
            return t;
        }

        static Texture2D MakeGlow(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01(1f - d);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            t.Apply();
            return t;
        }

        static Texture2D MakeDot(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
                }
            t.Apply();
            return t;
        }

        static Texture2D MakePanel(int w, int h, int radius)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // rounded-rect coverage
                    float cx = Mathf.Clamp(x + 0.5f, radius, w - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, h - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    float shade = y < 5 ? 0.62f : y > h - 4 ? 1f : 0.86f;
                    t.SetPixel(x, y, new Color(shade, shade, shade, a));
                }
            t.Apply();
            return t;
        }
    }
}
