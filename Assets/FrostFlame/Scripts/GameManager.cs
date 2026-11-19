using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FrostFlame
{
    /// <summary>Level state, gem tally, timer, win/lose, restart and the HUD (IMGUI).</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public PlayerController ember;
        public PlayerController frost;
        public ExitDoor emberExit;
        public ExitDoor frostExit;
        public string levelTitle = "Level 1 - The Frozen Furnace";

        enum State { Playing, Lost, Won }
        State state = State.Playing;

        int emberGems, frostGems, emberTotal, frostTotal;
        float timer;
        float stateTime;
        float bothAtExitTime;
        string lostMessage = "";
        Lever[] levers;

        GUIStyle big, mid, small;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            foreach (var g in FindObjectsByType<Gem>(FindObjectsSortMode.None))
            {
                if (g.element == Element.Fire) emberTotal++; else frostTotal++;
            }
            levers = FindObjectsByType<Lever>(FindObjectsSortMode.None);
        }

        public void AddGem(Element e)
        {
            if (e == Element.Fire) emberGems++; else frostGems++;
        }

        public void OnPlayerDied(PlayerController p)
        {
            if (state != State.Playing) return;
            state = State.Lost;
            stateTime = 0f;
            lostMessage = ElementColors.Name(p.element) + (p.element == Element.Fire ? " was doused!" : " melted away!");
        }

        void Update()
        {
            stateTime += Time.deltaTime;
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) Restart();
            if (state != State.Playing && stateTime > 0.6f && kb != null &&
                (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) Restart();

            if (state != State.Playing) return;
            timer += Time.deltaTime;

            bool both = emberExit && frostExit && emberExit.Occupied && frostExit.Occupied;
            bothAtExitTime = both ? bothAtExitTime + Time.deltaTime : 0f;
            if (bothAtExitTime > 0.5f)
            {
                state = State.Won;
                stateTime = 0f;
                if (ember) ember.Frozen = true;
                if (frost) frost.Frozen = true;
            }
        }

        void Restart()
        {
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }

        // ------------------------------------------------------------------ HUD

        void EnsureStyles()
        {
            if (big != null) return;
            big = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            mid = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            small = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        }

        static void Label(Rect r, string text, GUIStyle style, Color color)
        {
            var old = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.6f * color.a);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }

        static void Panel(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void OnGUI()
        {
            EnsureStyles();
            // design for 1920x1080, scale to the actual screen
            float scale = Screen.height / 1080f;
            float w = Screen.width / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            // top bar
            Panel(new Rect(w / 2 - 430, 8, 860, 52), new Color(0.05f, 0.07f, 0.12f, 0.55f));
            Label(new Rect(w / 2 - 430, 8, 280, 52), $"Ember  {emberGems}/{emberTotal}", mid, ElementColors.Fire);
            int sec = Mathf.FloorToInt(timer);
            Label(new Rect(w / 2 - 150, 8, 300, 52), $"{sec / 60:00}:{sec % 60:00}", mid, Color.white);
            Label(new Rect(w / 2 + 150, 8, 280, 52), $"Frost  {frostGems}/{frostTotal}", mid, ElementColors.Ice);

            // intro / controls card
            if (state == State.Playing && timer < 7f)
            {
                float a = Mathf.Clamp01(7f - timer);
                Panel(new Rect(w / 2 - 520, 80, 1040, 200), new Color(0.05f, 0.07f, 0.12f, 0.7f * a));
                Label(new Rect(w / 2 - 520, 88, 1040, 60), levelTitle, mid, new Color(1, 1, 1, a));
                Label(new Rect(w / 2 - 520, 145, 1040, 34), "EMBER:  A / D move,  W jump,  S use lever      -     FROST:  ← / → move,  ↑ jump,  ↓ use lever", small, new Color(1, 1, 1, a));
                Label(new Rect(w / 2 - 520, 180, 1040, 34), "Ember wades through lava, Frost through water. Green poison is deadly to both.", small, new Color(1, 0.9f, 0.7f, a));
                Label(new Rect(w / 2 - 520, 215, 1040, 34), "Get both heroes to their chests at the top-left. R = restart", small, new Color(0.8f, 0.9f, 1, a));
            }

            // lever hints
            if (state == State.Playing && levers != null && Camera.main)
            {
                foreach (var lv in levers)
                {
                    if (lv == null) continue;
                    bool near = Near(ember, lv.transform.position) || Near(frost, lv.transform.position);
                    if (!near) continue;
                    Vector3 sp = Camera.main.WorldToScreenPoint(lv.transform.position + Vector3.up * 1.6f);
                    Vector2 gp = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
                    Label(new Rect(gp.x - 150, gp.y - 18, 300, 36), "S / ↓  to pull", small, Color.white);
                }
            }

            if (state == State.Lost)
            {
                Panel(new Rect(0, 0, w, 1080), new Color(0.15f, 0f, 0f, Mathf.Clamp01(stateTime * 2f) * 0.45f));
                Label(new Rect(0, 400, w, 100), lostMessage, big, new Color(1f, 0.85f, 0.8f));
                Label(new Rect(0, 500, w, 60), "Press R or Enter to try again", mid, Color.white);
            }
            else if (state == State.Won)
            {
                Panel(new Rect(0, 0, w, 1080), new Color(0f, 0.05f, 0.1f, Mathf.Clamp01(stateTime * 2f) * 0.5f));
                Label(new Rect(0, 330, w, 100), "Level Complete!", big, new Color(1f, 0.95f, 0.6f));
                int total = emberGems + frostGems, max = emberTotal + frostTotal;
                string rank = total == max && timer < 120f ? "A" : total >= max - 1 ? "B" : "C";
                Label(new Rect(0, 440, w, 50), $"Gems {total}/{max}     Time {sec / 60:00}:{sec % 60:00}     Rank {rank}", mid, Color.white);
                Label(new Rect(0, 500, w, 50), "Press R or Enter to play again", small, new Color(0.85f, 0.9f, 1f));
            }
        }

        static bool Near(PlayerController p, Vector3 pos)
        {
            return p != null && !p.IsDead && Vector2.Distance(p.transform.position, pos) < 1.2f;
        }
    }
}
