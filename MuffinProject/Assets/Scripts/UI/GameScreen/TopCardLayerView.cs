using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// HUD 위에 그리는 3D 카드 층 (10-ui.md 2-1 「3D 층 구현」 앞 층). HUD 는 Overlay 캔버스라 항상 3D 카드 위에 그려진다 —
    /// 올린 손패 · 끄는 카드 · 대상 고르는 카드처럼 HUD(내 패널 · 찹츄 버튼 · 가림판)를 덮어야 하는 카드만 TopCards 레이어로 옮겨
    /// 전용 카메라가 렌더 텍스처에 그리고, 이 오브젝트의 RawImage 가 HUD 위에 얹는다.
    /// 체인의 카드(낸 카드)도 HUD 를 덮지만 손패보다는 뒤다 (사용자 2026-11-17) — TopChainCards 레이어를 체인 카메라가 같은 텍스처에 먼저 그리고,
    /// 앞 층 카메라는 깊이만 지운 뒤 손패를 그린다. 그래서 겹쳐도 카메라와의 거리와 상관없이 손패가 앞이다.
    /// 카드가 하나도 없으면 카메라 · 그림을 끄고 텍스처를 푼다. 카드를 지우기 전에 Set · SetChain(card, false) 로 내려야 한다.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class TopCardLayerView : MonoBehaviour
    {
        public const string TopLayerName = "TopCards";
        public const string ChainLayerName = "TopChainCards";
        public const string HandLayerName = "HandCards";
        private const int DepthBits = 24;

        [Tooltip("TopCards 레이어(손패)만 그리는 카메라 — 메인 카메라와 같은 자리 · 같은 화각. 체인 카메라 다음에 깊이만 지우고 그린다. 평소에는 꺼 둔다.")]
        [SerializeField] private Camera topCamera;
        [Tooltip("TopChainCards 레이어(낸 카드)만 그리는 카메라 — 같은 텍스처에 앞 층 카메라보다 먼저(depth 낮게) 투명 바탕으로 그린다. 평소에는 꺼 둔다.")]
        [SerializeField] private Camera chainCamera;

        private readonly HashSet<CardView> _cards = new HashSet<CardView>();
        private RawImage _image;
        private RenderTexture _texture;
        private int _topLayer;
        private int _chainLayer;
        private int _handLayer;

        private void Awake()
        {
            _image = GetComponent<RawImage>();
            _topLayer = LayerMask.NameToLayer(TopLayerName);
            _chainLayer = LayerMask.NameToLayer(ChainLayerName);
            _handLayer = LayerMask.NameToLayer(HandLayerName);

            if (topCamera == null || chainCamera == null || _topLayer < 0 || _chainLayer < 0 || _handLayer < 0)
            {
                Debug.LogError($"[{nameof(TopCardLayerView)}] {nameof(topCamera)} · {nameof(chainCamera)} 참조 또는 {TopLayerName} · {ChainLayerName} · {HandLayerName} 레이어가 없습니다.", this);
                return;
            }

            _image.raycastTarget = false;
            Refresh();
        }

        // 화면 크기가 바뀌면 텍스처를 다시 만든다. 지워진 카드는 목록에서 뺀다.
        private void Update()
        {
            if (_cards.Count == 0) return;

            _cards.RemoveWhere(card => card == null);
            if (_cards.Count == 0 || _texture == null || _texture.width != Screen.width || _texture.height != Screen.height)
                Refresh();
        }

        private void OnDestroy()
        {
            ReleaseTexture();
        }

        /// <summary>손패의 카드를 HUD 위 층의 맨 앞에 올리거나(true) 원래 손패 층으로 내린다(false). 같은 값을 다시 넣어도 된다.</summary>
        public void Set(CardView card, bool onTop) => Put(card, onTop, _topLayer);

        /// <summary>체인의 카드(낸 카드)를 HUD 위 · 손패 뒤 층에 올리거나(true) 손패 층으로 내린다(false). 같은 값을 다시 넣어도 된다.</summary>
        public void SetChain(CardView card, bool onTop) => Put(card, onTop, _chainLayer);

        private void Put(CardView card, bool onTop, int layer)
        {
            if (card == null) return;

            // 내릴 때는 그 층에 있는 카드만 — 손패에서 체인으로 넘어간 카드에 늦게 온 Set(false) 가 체인 층에서 내리지 않게
            bool changed = onTop ? _cards.Add(card) : card.gameObject.layer == layer && _cards.Remove(card);
            if (!changed) return;

            card.SetLayer(onTop ? layer : _handLayer);
            Refresh();
        }

        private void Refresh()
        {
            bool on = _cards.Count > 0;
            if (on && (_texture == null || _texture.width != Screen.width || _texture.height != Screen.height))
            {
                ReleaseTexture();
                _texture = new RenderTexture(Screen.width, Screen.height, DepthBits, RenderTextureFormat.ARGB32)
                {
                    name = nameof(TopCardLayerView),
                    antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing),
                };
                chainCamera.targetTexture = _texture;
                topCamera.targetTexture = _texture;
                _image.texture = _texture;
            }
            else if (!on)
            {
                ReleaseTexture();
            }

            // 둘 다 켠다 — 바탕을 지우는 것은 체인 카메라라, 체인 카드가 없어도 앞 층 카메라만 켜면 지난 그림이 남는다
            chainCamera.enabled = on;
            topCamera.enabled = on;
            _image.enabled = on;
        }

        private void ReleaseTexture()
        {
            if (_texture == null) return;

            if (chainCamera != null)
                chainCamera.targetTexture = null;
            if (topCamera != null)
                topCamera.targetTexture = null;
            if (_image != null)
                _image.texture = null;
            _texture.Release();
            Destroy(_texture);
            _texture = null;
        }
    }
}
