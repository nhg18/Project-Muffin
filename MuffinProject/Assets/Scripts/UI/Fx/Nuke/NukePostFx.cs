using UnityEngine;

namespace Chapchu.UI.Fx.Nuke
{
    /// <summary>
    /// 「대소동」 3D 장면 카메라의 후처리 — 시안 NUKE3D GX.finish 와 같은 순서 (주변광 차폐 → 블룸 → 심도 → 빛줄기 → 마무리).
    /// 값은 <see cref="NukeSceneFx"/> 가 매 프레임 <see cref="Apply"/> 로 넣는다. 셰이더는 Sprites/CardFx/Nuke/Shaders/NukePost.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class NukePostFx : MonoBehaviour
    {
        /// <summary>한 프레임의 후처리 값 (시안 env 의 E).</summary>
        public struct Settings
        {
            public float Expo;
            public float Bloom;
            public float Saturation;
            public float Contrast;
            public Vector3 Tint;
            public float Grain;
            public float Vignette;
            public Vector3 VignetteColor;
            public float RadialBlur;
            public Vector2 RadialCenter;
            public float Chroma;
            public float Fade;
            public Vector3 FadeColor;
            /// <summary>충격파 물결 (화면 x, y, 반지름, 세기).</summary>
            public Vector4 Ripple;
            public float AoK;
            public float Focus;
            public float DofK;
            public float RayK;
            public Vector2 SunUV;
            /// <summary>tan(반 화각) 가로 · 세로.</summary>
            public Vector2 TanHalf;
            /// <summary>알갱이가 움직이는 시각 (초).</summary>
            public float Time;
        }

        private const int AoPass = 0;
        private const int BrightPass = 1;
        private const int DownPass = 2;
        private const int UpPass = 3;
        private const int RayPass = 4;
        private const int FinishPass = 5;
        private const int BloomLevels = 5;

        private static readonly int PxId = Shader.PropertyToID("_Px");
        private static readonly int KId = Shader.PropertyToID("_K");
        private static readonly int ThrId = Shader.PropertyToID("_Thr");
        private static readonly int KkId = Shader.PropertyToID("_KK");
        private static readonly int SunId = Shader.PropertyToID("_Sun");
        private static readonly int AoTexId = Shader.PropertyToID("_AoTex");
        private static readonly int AoPxId = Shader.PropertyToID("_AoPx");
        private static readonly int AoKId = Shader.PropertyToID("_AoK");
        private static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");
        private static readonly int RayTexId = Shader.PropertyToID("_RayTex");
        private static readonly int HalfTexId = Shader.PropertyToID("_HalfTex");
        private static readonly int QuarterTexId = Shader.PropertyToID("_QuarterTex");
        private static readonly int ExpoId = Shader.PropertyToID("_Expo");
        private static readonly int BloomId = Shader.PropertyToID("_Bloom");
        private static readonly int SatId = Shader.PropertyToID("_Sat");
        private static readonly int ConId = Shader.PropertyToID("_Con");
        private static readonly int VigId = Shader.PropertyToID("_Vig");
        private static readonly int GrainId = Shader.PropertyToID("_Grain");
        private static readonly int CaId = Shader.PropertyToID("_Ca");
        private static readonly int RbId = Shader.PropertyToID("_Rb");
        private static readonly int RbcId = Shader.PropertyToID("_Rbc");
        private static readonly int TimeId = Shader.PropertyToID("_Time2");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int AspId = Shader.PropertyToID("_Asp");
        private static readonly int FocId = Shader.PropertyToID("_Foc");
        private static readonly int DofKId = Shader.PropertyToID("_DofK");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int VigCId = Shader.PropertyToID("_VigC");
        private static readonly int FadeCId = Shader.PropertyToID("_FadeC");
        private static readonly int RipId = Shader.PropertyToID("_Rip");

        [Tooltip("후처리 셰이더 (Hidden/Chapchu/Nuke/Post).")]
        [SerializeField] private Shader shader;
        [Tooltip("이보다 밝은 곳만 번진다 (시안 HDR 1.08).")]
        [SerializeField] private float bloomThreshold = 1.08f;
        [Tooltip("번짐을 한 단계 키울 때마다 곱하는 세기.")]
        [SerializeField] private float bloomStep = 0.85f;

        private Material _material;
        private Settings _settings;
        private readonly RenderTexture[] _bloom = new RenderTexture[BloomLevels];

        private void Awake()
        {
            if (shader == null)
            {
                Debug.LogError($"[{nameof(NukePostFx)}] 후처리 셰이더가 비어 있습니다.", this);
                return;
            }

            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (_material == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            int width = source.width;
            int height = source.height;
            RenderTextureFormat format = source.format;

            // 주변광 차폐 (반 해상도) — 마무리에서 고르게 펴서 곱한다
            RenderTexture ao = null;
            _material.SetFloat(AoKId, _settings.AoK);
            if (_settings.AoK > 0.01f)
            {
                ao = RenderTexture.GetTemporary(Mathf.Max(2, width >> 1), Mathf.Max(2, height >> 1), 0, RenderTextureFormat.ARGB32);
                _material.SetVector(KkId, _settings.TanHalf);
                Graphics.Blit(source, ao, _material, AoPass);
                _material.SetTexture(AoTexId, ao);
                _material.SetVector(AoPxId, new Vector2(1f / ao.width, 1f / ao.height));
            }

            // 블룸: 밝은 곳 → 다섯 번 줄이기 → 키우며 더하기
            int w = width;
            int h = height;
            for (int i = 0; i < BloomLevels; i++)
            {
                w = Mathf.Max(2, w >> 1);
                h = Mathf.Max(2, h >> 1);
                _bloom[i] = RenderTexture.GetTemporary(w, h, 0, format);
            }

            _material.SetVector(PxId, new Vector2(1f / width, 1f / height));
            _material.SetFloat(ThrId, bloomThreshold);
            Graphics.Blit(source, _bloom[0], _material, BrightPass);
            for (int i = 1; i < BloomLevels; i++)
            {
                _material.SetVector(PxId, new Vector2(1f / _bloom[i - 1].width, 1f / _bloom[i - 1].height));
                Graphics.Blit(_bloom[i - 1], _bloom[i], _material, DownPass);
            }

            _material.SetFloat(KId, bloomStep);
            for (int i = BloomLevels - 1; i > 0; i--)
            {
                _material.SetVector(PxId, new Vector2(1f / _bloom[i].width, 1f / _bloom[i].height));
                Graphics.Blit(_bloom[i], _bloom[i - 1], _material, UpPass);
            }

            // 심도: 반 · 4분의 1 해상도로 흐린 장면
            RenderTexture half = null;
            RenderTexture quarter = null;
            if (_settings.DofK > 0.001f)
            {
                half = RenderTexture.GetTemporary(Mathf.Max(2, width >> 1), Mathf.Max(2, height >> 1), 0, format);
                quarter = RenderTexture.GetTemporary(Mathf.Max(2, width >> 2), Mathf.Max(2, height >> 2), 0, format);
                _material.SetVector(PxId, new Vector2(1f / width, 1f / height));
                Graphics.Blit(source, half, _material, DownPass);
                _material.SetVector(PxId, new Vector2(1f / half.width, 1f / half.height));
                Graphics.Blit(half, quarter, _material, DownPass);
            }

            // 빛줄기 (4분의 1 해상도)
            RenderTexture rays = null;
            if (_settings.RayK > 0.005f)
            {
                rays = RenderTexture.GetTemporary(Mathf.Max(2, width >> 2), Mathf.Max(2, height >> 2), 0, format);
                _material.SetVector(SunId, _settings.SunUV);
                _material.SetFloat(KId, _settings.RayK);
                Graphics.Blit(source, rays, _material, RayPass);
            }

            _material.SetTexture(BloomTexId, _bloom[0]);
            _material.SetTexture(RayTexId, rays != null ? rays : (Texture)Texture2D.blackTexture);
            _material.SetTexture(HalfTexId, half != null ? half : (Texture)Texture2D.blackTexture);
            _material.SetTexture(QuarterTexId, quarter != null ? quarter : (Texture)Texture2D.blackTexture);
            _material.SetFloat(ExpoId, _settings.Expo);
            _material.SetFloat(BloomId, _settings.Bloom);
            _material.SetFloat(SatId, _settings.Saturation);
            _material.SetFloat(ConId, _settings.Contrast);
            _material.SetFloat(VigId, _settings.Vignette);
            _material.SetFloat(GrainId, _settings.Grain);
            _material.SetFloat(CaId, _settings.Chroma);
            _material.SetFloat(RbId, _settings.RadialBlur);
            _material.SetVector(RbcId, _settings.RadialCenter);
            _material.SetFloat(TimeId, _settings.Time);
            _material.SetFloat(FadeId, _settings.Fade);
            _material.SetFloat(AspId, (float)width / height);
            _material.SetFloat(FocId, _settings.Focus);
            _material.SetFloat(DofKId, _settings.DofK);
            _material.SetVector(TintId, _settings.Tint);
            _material.SetVector(VigCId, _settings.VignetteColor);
            _material.SetVector(FadeCId, _settings.FadeColor);
            _material.SetVector(RipId, _settings.Ripple);
            Graphics.Blit(source, destination, _material, FinishPass);

            for (int i = 0; i < BloomLevels; i++)
            {
                RenderTexture.ReleaseTemporary(_bloom[i]);
                _bloom[i] = null;
            }

            if (rays != null)
                RenderTexture.ReleaseTemporary(rays);
            if (ao != null)
                RenderTexture.ReleaseTemporary(ao);
            if (half != null)
                RenderTexture.ReleaseTemporary(half);
            if (quarter != null)
                RenderTexture.ReleaseTemporary(quarter);
        }

        /// <summary>이번 프레임의 후처리 값을 넣는다.</summary>
        public void Apply(in Settings settings)
        {
            _settings = settings;
        }
    }
}
