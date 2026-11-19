using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FrostFlame
{
    /// <summary>
    /// Keyboard-driven platformer hero. Two of these share one keyboard (local co-op).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Identity")]
        public Element element = Element.Fire;

        [Header("Keys")]
        public Key leftKey = Key.A;
        public Key rightKey = Key.D;
        public Key jumpKey = Key.W;
        public Key interactKey = Key.S;

        [Header("Movement")]
        public float moveSpeed = 6f;
        public float groundAcceleration = 70f;
        public float airAcceleration = 45f;
        public float jumpVelocity = 13f;
        public float baseGravity = 3.2f;
        public float fallGravity = 3.9f;
        public float jumpCutGravity = 6f;
        public float maxFallSpeed = 18f;
        public float coyoteTime = 0.1f;
        public float jumpBufferTime = 0.12f;

        public PlayerVisual visual;

        public bool Grounded { get; private set; }
        public bool IsDead { get; private set; }
        public bool Frozen { get; set; }
        public float MoveInput { get; private set; }
        public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;

        Rigidbody2D rb;
        BoxCollider2D box;
        readonly List<Collider2D> hits = new List<Collider2D>();
        ContactFilter2D solidFilter;
        float coyoteTimer;
        float bufferTimer;
        bool jumpHeld;
        bool wasGrounded;
        MovingPlatform groundPlatform;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            solidFilter = new ContactFilter2D { useTriggers = false };
            solidFilter.NoFilter();
            solidFilter.useTriggers = false;
            rb.gravityScale = baseGravity;
            if (visual == null) visual = GetComponentInChildren<PlayerVisual>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (IsDead || Frozen || kb == null)
            {
                MoveInput = 0f;
                jumpHeld = false;
                return;
            }

            float x = 0f;
            if (kb[leftKey].isPressed) x -= 1f;
            if (kb[rightKey].isPressed) x += 1f;
            MoveInput = x;

            jumpHeld = kb[jumpKey].isPressed;
            if (kb[jumpKey].wasPressedThisFrame) bufferTimer = jumpBufferTime;

            if (kb[interactKey].wasPressedThisFrame) TryInteract();
        }

        void FixedUpdate()
        {
            if (IsDead) return;
            float dt = Time.fixedDeltaTime;

            CheckGround();
            if (Grounded) coyoteTimer = coyoteTime; else coyoteTimer -= dt;
            bufferTimer -= dt;

            Vector2 v = rb.linearVelocity;
            Vector2 platformVel = groundPlatform != null ? groundPlatform.Velocity : Vector2.zero;

            float target = (Frozen ? 0f : MoveInput * moveSpeed) + platformVel.x;
            float accel = Grounded ? groundAcceleration : airAcceleration;
            v.x = Mathf.MoveTowards(v.x, target, accel * dt);

            if (!Frozen && bufferTimer > 0f && coyoteTimer > 0f)
            {
                v.y = jumpVelocity + Mathf.Max(0f, platformVel.y);
                bufferTimer = 0f;
                coyoteTimer = 0f;
                Grounded = false;
                if (visual) visual.OnJump();
            }

            if (v.y > 0.01f && !jumpHeld) rb.gravityScale = jumpCutGravity;
            else if (v.y < -0.01f) rb.gravityScale = fallGravity;
            else rb.gravityScale = baseGravity;

            if (v.y < -maxFallSpeed) v.y = -maxFallSpeed;
            rb.linearVelocity = v;

            if (Grounded && !wasGrounded && visual) visual.OnLand();
            wasGrounded = Grounded;
        }

        void CheckGround()
        {
            Bounds b = box.bounds;
            Vector2 center = new Vector2(b.center.x, b.min.y - 0.04f);
            Vector2 size = new Vector2(b.size.x * 0.9f, 0.08f);
            hits.Clear();
            Physics2D.OverlapBox(center, size, 0f, solidFilter, hits);

            Grounded = false;
            groundPlatform = null;
            foreach (var h in hits)
            {
                if (h == box || h.isTrigger) continue;
                var platform = h.GetComponentInParent<MovingPlatform>();
                float groundVy = platform != null ? platform.Velocity.y : 0f;
                var otherRb = h.attachedRigidbody;
                if (otherRb != null && otherRb.bodyType == RigidbodyType2D.Dynamic) groundVy = otherRb.linearVelocity.y;
                if (rb.linearVelocity.y <= groundVy + 0.2f)
                {
                    Grounded = true;
                    if (platform != null) groundPlatform = platform;
                }
            }
        }

        void TryInteract()
        {
            var cols = Physics2D.OverlapBoxAll(box.bounds.center, box.bounds.size + new Vector3(0.4f, 0.2f), 0f);
            foreach (var c in cols)
            {
                var lever = c.GetComponentInParent<Lever>();
                if (lever != null)
                {
                    lever.Toggle();
                    return;
                }
            }
        }

        public void Collect()
        {
            if (visual) visual.OnCollect();
        }

        public void Die()
        {
            if (IsDead) return;
            IsDead = true;
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
            if (visual) visual.PlayDeath();
            if (GameManager.Instance) GameManager.Instance.OnPlayerDied(this);
        }
    }
}
