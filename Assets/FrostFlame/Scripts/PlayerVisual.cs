using UnityEngine;

namespace FrostFlame
{
    /// <summary>
    /// Procedural animation for the blob hero assembled from the BayatGames character parts:
    /// run cycle (feet + hands), body bob, squash/stretch, blinking, glow pulse, death pop.
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        public PlayerController controller;
        public Transform rig;          // flipped for facing, squashed for jumps
        public Transform body;
        public Transform eyeA, eyeB;
        public Transform footL, footR;
        public Transform handL, handR;
        public SpriteRenderer glow;
        public ParticleSystem aura;
        public ParticleSystem deathBurst;

        Vector3 footLBase, footRBase, handLBase, handRBase, bodyBase, eyeABase, eyeBBase;
        float phase;
        float facing = 1f;
        Vector2 squash = Vector2.one;      // current
        Vector2 squashVel;
        float blinkTimer = 2f;
        float blinkT;
        float deathT = -1f;
        SpriteRenderer[] renderers;

        void Start()
        {
            footLBase = footL.localPosition; footRBase = footR.localPosition;
            handLBase = handL.localPosition; handRBase = handR.localPosition;
            bodyBase = body.localPosition;
            eyeABase = eyeA.localPosition; eyeBBase = eyeB.localPosition;
            renderers = GetComponentsInChildren<SpriteRenderer>();
        }

        public void OnJump() { squash = new Vector2(0.75f, 1.3f); }
        public void OnLand() { squash = new Vector2(1.3f, 0.72f); }
        public void OnCollect() { squash = new Vector2(1.2f, 1.2f); }

        public void PlayDeath()
        {
            deathT = 0f;
            if (aura) aura.Stop();
            if (deathBurst) deathBurst.Play();
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (deathT >= 0f)
            {
                deathT += dt;
                float k = Mathf.Clamp01(deathT / 0.35f);
                rig.localScale = Vector3.one * Mathf.Lerp(1.15f, 0f, k * k);
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    var c = r.color; c.a = 1f - k; r.color = c;
                }
                return;
            }

            float move = controller ? controller.MoveInput : 0f;
            bool grounded = controller && controller.Grounded;
            float vy = controller ? controller.Velocity.y : 0f;
            if (Mathf.Abs(move) > 0.01f) facing = Mathf.Sign(move);

            // spring the squash back to 1
            squash = Vector2.SmoothDamp(squash, Vector2.one, ref squashVel, 0.08f);
            rig.localScale = new Vector3(facing * squash.x, squash.y, 1f);

            float speed01 = Mathf.Abs(move);
            if (grounded && speed01 > 0.01f)
            {
                phase += dt * 16f;
                float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
                footL.localPosition = footLBase + new Vector3(s * 0.16f, Mathf.Max(0f, c) * 0.12f, 0f);
                footR.localPosition = footRBase + new Vector3(-s * 0.16f, Mathf.Max(0f, -c) * 0.12f, 0f);
                handL.localPosition = handLBase + new Vector3(-s * 0.1f, 0f, 0f);
                handR.localPosition = handRBase + new Vector3(s * 0.1f, 0f, 0f);
                body.localPosition = bodyBase + new Vector3(0f, Mathf.Abs(s) * 0.07f, 0f);
                body.localRotation = Quaternion.Euler(0f, 0f, -6f);
            }
            else if (!grounded)
            {
                // tuck feet and raise arms in the air
                float t = Mathf.Clamp(vy / 10f, -1f, 1f);
                footL.localPosition = Vector3.Lerp(footL.localPosition, footLBase + new Vector3(-0.08f, 0.1f, 0f), dt * 12f);
                footR.localPosition = Vector3.Lerp(footR.localPosition, footRBase + new Vector3(0.12f, 0.16f, 0f), dt * 12f);
                handL.localPosition = Vector3.Lerp(handL.localPosition, handLBase + new Vector3(0f, 0.2f + t * 0.1f, 0f), dt * 12f);
                handR.localPosition = Vector3.Lerp(handR.localPosition, handRBase + new Vector3(0f, 0.2f + t * 0.1f, 0f), dt * 12f);
                body.localPosition = Vector3.Lerp(body.localPosition, bodyBase, dt * 12f);
                body.localRotation = Quaternion.Euler(0f, 0f, -t * 6f);
            }
            else
            {
                // idle breathing
                float b = Mathf.Sin(Time.time * 3f) * 0.025f;
                footL.localPosition = Vector3.Lerp(footL.localPosition, footLBase, dt * 14f);
                footR.localPosition = Vector3.Lerp(footR.localPosition, footRBase, dt * 14f);
                handL.localPosition = Vector3.Lerp(handL.localPosition, handLBase + new Vector3(0f, b, 0f), dt * 14f);
                handR.localPosition = Vector3.Lerp(handR.localPosition, handRBase + new Vector3(0f, b, 0f), dt * 14f);
                body.localPosition = bodyBase + new Vector3(0f, b, 0f);
                body.localRotation = Quaternion.Lerp(body.localRotation, Quaternion.identity, dt * 10f);
            }

            // eyes follow body + blink
            Vector3 bodyOffset = body.localPosition - bodyBase;
            blinkTimer -= dt;
            if (blinkTimer <= 0f) { blinkT = 0.12f; blinkTimer = Random.Range(2f, 4.5f); }
            float eyeY = 1f;
            if (blinkT > 0f) { blinkT -= dt; eyeY = 0.15f; }
            eyeA.localPosition = eyeABase + bodyOffset;
            eyeB.localPosition = eyeBBase + bodyOffset;
            eyeA.localScale = new Vector3(1f, eyeY, 1f);
            eyeB.localScale = new Vector3(1f, eyeY, 1f);

            if (glow)
            {
                float p = 0.85f + Mathf.Sin(Time.time * 4f) * 0.15f;
                glow.transform.localScale = Vector3.one * p * 2.2f;
            }
        }
    }
}
