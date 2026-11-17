using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// HUD 위에 그리는 3D 카드 층 (10-ui.md 2-1 「3D 층 구현」 앞 층). HUD 는 Overlay 캔버스라 항상 3D 카드 위에 그려진다 —
    /// HUD 를 덮어야 하는 카드만 전용 레이어로 옮겨 전용 카메라가 렌더 텍스처에 그리고, HUD 안의 RawImage 가 그것을 얹는다.
    /// 층은 둘이다 (사용자 2026-11-17) — HUD 안에서 RawImage 의 형제 순서가 그대로 그리는 순서다:
    ///   체인 층 (TopChainCards — 낸 카드): chainCamera → chainImage. 상대 좌석 위 · 카드 이름 + 시간 알약(반응 타이머) · 주인 태그 아래.
    ///   손패 층 (TopCards — 올린 손패 · 끄는 카드 · 대상 고르는 카드): topCamera → 이 오브젝트의 RawImage. 그 알약 · 내 패널 · 찹츄 버튼 위, 서랍 아래.
    /// 층마다 카드가 없으면 카메라 · 그림을 끄고 텍스처를 푼다. 카드를 지우기 전에 Set · SetChain(card, false) 로 내려야 한다.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class TopCardLayerView : MonoBehaviour
    {
        public const string TopLayerName = "TopCards";
        public const string ChainLayerName = "TopChainCards";
        public const string HandLayerName = "HandCards";
        private const int DepthBits = 24;

        [Tooltip("TopCards 레이어(손패)만 그리는 카메라 — 메인 카메라와 같은 자리 · 같은 화각. 그림은 이 오브젝트의 RawImage. 평소에는 꺼 둔다.")]
        [SerializeField] private Camera topCamera;
        [Tooltip("TopChainCards 레이어(낸 카드)만 그리는 카메라 — 메인 카메라와 같은 자리 · 같은 화각. 그림은 chainImage. 평소에는 꺼 둔다.")]
        [SerializeField] private Camera chainCamera;
        [Tooltip("체인 층 그림을 얹는 RawImage — HUD 에서 상대 좌석보다 뒤(위), 반응 타이머 · 주인 태그보다 앞(아래) 형제. 앞 층 합성 재질.")]
        [SerializeField] private RawImage chainImage;

        // 한 층 — 카메라가 텍스처에 그리고 RawImage 가 얹는다
        private sealed class Pass
        {
            public Camera Camera;
            public RawImage Image;
            public RenderTexture Texture;
            public int Layer;
        }

        private readonly Dictionary<CardView, int> _cards = new Dictionary<CardView, int>(); // 올라간 카드 → 그 층의 레이어
        private Pass _top;
        private Pass _chain;
        private int _handLayer;

        private void Awake()
        {
            int topLayer = LayerMask.NameToLayer(TopLayerName);
            int chainLayer = LayerMask.NameToLayer(ChainLayerName);
            _handLayer = LayerMask.NameToLayer(HandLayerName);

            if (topCamera == null || chainCamera == null || chainImage == null || topLayer < 0 || chainLayer < 0 || _handLayer < 0)
            {
                Debug.LogError($"[{nameof(TopCardLayerView)}] {nameof(topCamera)} · {nameof(chainCamera)} · {nameof(chainImage)} 참조 또는 {TopLayerName} · {ChainLayerName} · {HandLayerName} 레이어가 없습니다.", this);
                return;
            }

            _top = new Pass { Camera = topCamera, Image = GetComponent<RawImage>(), Layer = topLayer };
            _chain = new Pass { Camera = chainCamera, Image = chainImage, Layer = chainLayer };
            _top.Image.raycastTarget = false;
            _chain.Image.raycastTarget = false;
            Refresh();
        }

        // 화면 크기가 바뀌면 텍스처를 다시 만든다. 지워진 카드는 목록에서 뺀다.
        private void Update()
        {
            if (_cards.Count == 0) return;

            if (_cards.Keys.Any(card => card == null))
            {
                foreach (CardView card in _cards.Keys.Where(card => card == null).ToList())
                    _cards.Remove(card);
                Refresh();
            }
            else if (NeedsTexture(_top) || NeedsTexture(_chain))
            {
                Refresh();
            }
        }

        private void OnDestroy()
        {
            Release(_top);
            Release(_chain);
        }

        /// <summary>손패의 카드를 손패 층(HUD 위 맨 앞)에 올리거나(true) 원래 손패 레이어로 내린다(false). 같은 값을 다시 넣어도 된다.</summary>
        public void Set(CardView card, bool onTop) => Put(card, onTop, _top.Layer);

        /// <summary>체인의 카드(낸 카드)를 체인 층(HUD 위 · 알약 · 손패 아래)에 올리거나(true) 손패 레이어로 내린다(false). 같은 값을 다시 넣어도 된다.</summary>
        public void SetChain(CardView card, bool onTop) => Put(card, onTop, _chain.Layer);

        private void Put(CardView card, bool onTop, int layer)
        {
            if (card == null) return;

            bool placed = _cards.TryGetValue(card, out int current);
            if (onTop)
            {
                if (placed && current == layer) return;
                _cards[card] = layer;
                card.SetLayer(layer);
            }
            else
            {
                // 그 층에 있는 카드만 내린다 — 손패에서 체인으로 넘어간 카드에 늦게 온 Set(false) 가 체인 층에서 내리지 않게
                if (!placed || current != layer) return;
                _cards.Remove(card);
                card.SetLayer(_handLayer);
            }

            Refresh();
        }

        private void Refresh()
        {
            Refresh(_top);
            Refresh(_chain);
        }

        private void Refresh(Pass pass)
        {
            bool on = _cards.ContainsValue(pass.Layer);
            if (on && IsStale(pass))
            {
                Release(pass);
                pass.Texture = new RenderTexture(Screen.width, Screen.height, DepthBits, RenderTextureFormat.ARGB32)
                {
                    name = $"{nameof(TopCardLayerView)}_{LayerMask.LayerToName(pass.Layer)}",
                    antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing),
                };
                pass.Camera.targetTexture = pass.Texture;
                pass.Image.texture = pass.Texture;
            }
            else if (!on)
            {
                Release(pass);
            }

            pass.Camera.enabled = on;
            pass.Image.enabled = on;
        }

        // 카드가 있는 층인데 텍스처가 없거나 화면 크기와 다르다
        private bool NeedsTexture(Pass pass) => _cards.ContainsValue(pass.Layer) && IsStale(pass);

        private static bool IsStale(Pass pass) => pass.Texture == null || pass.Texture.width != Screen.width || pass.Texture.height != Screen.height;

        private void Release(Pass pass)
        {
            if (pass == null || pass.Texture == null) return;

            // 씬이 내려갈 때는 카메라 · 이미지가 먼저 파괴돼 있을 수 있다 (게임이 끝나 결과 씬으로 넘어갈 때)
            if (pass.Camera != null) pass.Camera.targetTexture = null;
            if (pass.Image != null) pass.Image.texture = null;
            pass.Texture.Release();
            Destroy(pass.Texture);
            pass.Texture = null;
        }
    }
}
