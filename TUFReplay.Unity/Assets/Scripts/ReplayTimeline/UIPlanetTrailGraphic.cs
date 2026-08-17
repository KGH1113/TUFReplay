using UnityEngine;
using UnityEngine.UI;

namespace TUFReplay.Unity.ReplayTimeline
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/TUFReplay/Planet Trail Graphic")]
    public sealed class UIPlanetTrailGraphic : MaskableGraphic
    {
        private struct Particle
        {
            public bool Active;
            public Vector2 Position;
            public float Age;
            public float Lifetime;
            public float StartSize;
            public float Brightness;
            public float AlphaScale;
        }

        [SerializeField] private Texture2D particleTexture;
        [SerializeField, Range(1, 512)] private int maxParticles = 256;
        [SerializeField, Min(0f)] private float emissionRate = 501.2f;
        [SerializeField, Min(0.01f)] private float lifetime = 0.474f;
        [SerializeField, Min(0.1f)] private float startSize = 19.2f;
        [SerializeField, Min(0.1f)] private float endSize = 0.15f;
        [SerializeField] private float positionJitter = 2.4f;
        [SerializeField] private float sizeJitter;
        [SerializeField] private Color startColor = new Color(1f, 0.2f, 0.25f, 0.53f);
        [SerializeField] private Color endColor = new Color(0.6f, 0.08f, 0.1f, 0f);
        [SerializeField] private Vector2 previewOrbitRadius = new Vector2(18f, 18f);
        [SerializeField] private float previewPhase = 0.65f;

        private Particle[] particles;
        private Vector2 emitterPosition;
        private Vector2 previousEmitterPosition;
        private bool hasEmitter;
        private bool emissionEnabled = true;
        private float emissionAccumulator;
        private uint randomState = 0xA341316Cu;

        public override Texture mainTexture => particleTexture != null ? particleTexture : s_WhiteTexture;

        public void Configure(Texture2D texture, Color from, Color to, Vector2 orbitRadius, float phase)
        {
            particleTexture = texture;
            startColor = from;
            endColor = to;
            previewOrbitRadius = orbitRadius;
            previewPhase = phase;
            EnsureBuffer();
            SetMaterialDirty();
            SetVerticesDirty();
        }

        public void SetPreviewPhase(float phase)
        {
            previewPhase = phase;
            if (!Application.isPlaying)
                SetVerticesDirty();
        }

        public void SetEmitterPosition(Vector2 position)
        {
            emitterPosition = position;
            if (!hasEmitter)
            {
                previousEmitterPosition = position;
                hasEmitter = true;
            }
        }

        public void SetEmissionEnabled(bool value) => emissionEnabled = value;

        public void ClearTrail()
        {
            EnsureBuffer();
            for (int i = 0; i < particles.Length; i++)
                particles[i].Active = false;
            emissionAccumulator = 0f;
            hasEmitter = false;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            EnsureBuffer();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            maxParticles = Mathf.Max(1, maxParticles);
            particles = null;
            EnsureBuffer();
        }
#endif

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            EnsureBuffer();
            float deltaTime = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            bool changed = false;

            for (int i = 0; i < particles.Length; i++)
            {
                if (!particles[i].Active)
                    continue;

                particles[i].Age += deltaTime;
                if (particles[i].Age >= particles[i].Lifetime)
                    particles[i].Active = false;
                changed = true;
            }

            if (hasEmitter && emissionEnabled && emissionRate > 0f)
            {
                emissionAccumulator += deltaTime * emissionRate;
                int emitCount = Mathf.Min(Mathf.FloorToInt(emissionAccumulator), maxParticles);
                emissionAccumulator -= emitCount;
                for (int i = 0; i < emitCount; i++)
                {
                    float interpolation = emitCount > 1 ? i / (float)(emitCount - 1) : 1f;
                    Emit(Vector2.Lerp(previousEmitterPosition, emitterPosition, interpolation));
                }
                changed |= emitCount > 0;
            }

            previousEmitterPosition = emitterPosition;
            if (changed)
                SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            if (!Application.isPlaying)
            {
                PopulateStaticPreview(vertexHelper);
                return;
            }

            EnsureBuffer();
            for (int i = 0; i < particles.Length; i++)
            {
                Particle particle = particles[i];
                if (!particle.Active)
                    continue;

                float normalizedAge = Mathf.Clamp01(particle.Age / particle.Lifetime);
                float size = Mathf.Lerp(particle.StartSize, endSize, normalizedAge);
                AddParticle(vertexHelper, particle.Position, size, EvaluateParticleColor(particle, normalizedAge));
            }
        }

        private void PopulateStaticPreview(VertexHelper vertexHelper)
        {
            const int previewParticles = 22;
            for (int i = previewParticles - 1; i >= 0; i--)
            {
                float normalizedAge = (i + 1f) / (previewParticles + 1f);
                float phase = previewPhase - normalizedAge * Mathf.PI * 0.95f;
                Vector2 position = new Vector2(
                    Mathf.Cos(phase) * previewOrbitRadius.x,
                    Mathf.Sin(phase) * previewOrbitRadius.y);
                float wobble = Mathf.Sin(i * 2.17f) * positionJitter * 0.35f;
                position += position.normalized * wobble;
                float size = Mathf.Lerp(startSize, endSize, normalizedAge);
                AddParticle(vertexHelper, position, size, Color.Lerp(startColor, endColor, normalizedAge));
            }
        }

        private void Emit(Vector2 position)
        {
            int index = FindAvailableParticle();
            float angle = Next01() * Mathf.PI * 2f;
            float radius = Next01() * positionJitter;
            float variedSize = startSize * (1f + (Next01() * 2f - 1f) * sizeJitter);
            particles[index] = new Particle
            {
                Active = true,
                Position = position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                Age = 0f,
                Lifetime = lifetime,
                StartSize = variedSize,
                Brightness = Mathf.Lerp(0.82f, 1f, Next01()),
                AlphaScale = Mathf.Lerp(0.8f, 1f, Next01())
            };
        }

        private Color EvaluateParticleColor(Particle particle, float normalizedAge)
        {
            Color particleColor = Color.Lerp(startColor, endColor, normalizedAge);
            particleColor.r *= particle.Brightness;
            particleColor.g *= particle.Brightness;
            particleColor.b *= particle.Brightness;
            particleColor.a *= particle.AlphaScale;
            return particleColor;
        }

        private int FindAvailableParticle()
        {
            int oldestIndex = 0;
            float oldestAge = -1f;
            for (int i = 0; i < particles.Length; i++)
            {
                if (!particles[i].Active)
                    return i;
                if (particles[i].Age > oldestAge)
                {
                    oldestAge = particles[i].Age;
                    oldestIndex = i;
                }
            }
            return oldestIndex;
        }

        private float Next01()
        {
            randomState = randomState * 1664525u + 1013904223u;
            return (randomState & 0x00FFFFFFu) / 16777216f;
        }

        private void EnsureBuffer()
        {
            if (particles == null || particles.Length != maxParticles)
                particles = new Particle[maxParticles];
        }

        private static void AddParticle(VertexHelper vh, Vector2 center, float size, Color particleColor)
        {
            float half = size * 0.5f;
            Color32 color32 = particleColor;
            int index = vh.currentVertCount;
            vh.AddVert(center + new Vector2(-half, -half), color32, Vector2.zero);
            vh.AddVert(center + new Vector2(-half, half), color32, Vector2.up);
            vh.AddVert(center + new Vector2(half, half), color32, Vector2.one);
            vh.AddVert(center + new Vector2(half, -half), color32, Vector2.right);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
