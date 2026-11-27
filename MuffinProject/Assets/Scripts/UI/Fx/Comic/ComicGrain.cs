using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 종이결 — 화면 전체에 옅은 잡티를 덮는다 (시안 grain: 256×256 잡티 세 장을 1/24 초마다 바꾸며 어긋나게 깔고 overlay 0.08).
    /// 같은 오브젝트의 RawImage 에 실행 때 만든 잡티를 넣는다. 머티리얼은 ComicGrain (UIFxBlend 의 「두 배 곱하기」 — 회색 0.5 = 그대로).
    /// overlay 를 「두 배 곱하기」로 근사한다: 잡티 값 0.46 + 0.08·v (v = 0 ~ 1) 를 화면에 두 번 곱한다 — 어두운 쪽은 시안과 같고, 밝은 쪽은 조금 다르다.
    /// 잡티 한 칸 = 화면 1px (시안 캔버스 1px). 컷인이 매 프레임 <see cref="SetTime"/> 을 부른다.
    /// 프리팹에서는 RawImage 를 꺼 둔다 — 잡티가 없으면 흰 그림을 두 배로 곱해 화면이 밝아진다. 처음 SetTime 때 켠다.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class ComicGrain : MonoBehaviour
    {
        // 시안 grain(): 잡티 그림 한 변 256, 세 장 (씨앗 = 1 · 2 · 3 × 91), 1초에 24 번, 칸마다 (97, 57) 씩 밀린다
        private const int TextureSize = 256;
        private const int TextureCount = 3;
        private const int SeedStride = 91;
        private const float FramesPerSecond = 24f;
        private const int ShiftX = 97;
        private const int ShiftY = 57;

        [Tooltip("잡티 세기 (시안 overlay 의 globalAlpha 0.08).")]
        [SerializeField] private float strength = 0.08f;

        private readonly Texture2D[] _textures = new Texture2D[TextureCount];
        private RawImage _image;
        private Canvas _canvas;
        private int _frame = -1;

        /// <summary>장면 시각 (초) — 1/24 초 칸이 바뀌면 잡티를 바꾼다.</summary>
        public void SetTime(float t)
        {
            int frame = Mathf.FloorToInt(t * FramesPerSecond);
            if (frame == _frame)
                return;
            _frame = frame;
            Prepare();
            _image.texture = _textures[frame % TextureCount];
            _image.enabled = true;
            Rect rect = _image.rectTransform.rect;
            float pixels = _canvas != null ? _canvas.rootCanvas.scaleFactor : 1f;
            float w = rect.width * pixels / TextureSize, h = rect.height * pixels / TextureSize;
            // 시안: 잡티를 (−(f·97 mod 256), −(f·57 mod 256)) 로 옮겨 깐다
            float u = (float)(frame * ShiftX % TextureSize) / TextureSize, v = (float)(frame * ShiftY % TextureSize) / TextureSize;
            _image.uvRect = new Rect(u, v, w, h);
        }

        private void Prepare()
        {
            if (_image != null && _textures[0] != null)
                return;
            _image = GetComponent<RawImage>();
            _canvas = GetComponentInParent<Canvas>();
            for (int i = 0; i < TextureCount; i++)
            {
                if (_textures[i] == null)
                    _textures[i] = Make((i + 1) * SeedStride);
            }
        }

        private Texture2D Make(int seed)
        {
            var random = new ComicRandom(seed);
            var pixels = new Color32[TextureSize * TextureSize];
            float low = 0.5f - strength / 2f;
            for (int i = 0; i < pixels.Length; i++)
            {
                // 시안 v = R()·255 — 두 배 곱하기에서 0.5 가 그대로이므로 0.5 둘레로 세기만큼
                byte value = (byte)Mathf.RoundToInt((low + random.Next() * strength) * 255f);
                pixels[i] = new Color32(value, value, value, 255);
            }
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "만화 컷인 종이결",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < TextureCount; i++)
            {
                if (_textures[i] != null)
                    Destroy(_textures[i]);
                _textures[i] = null;
            }
        }
    }
}
