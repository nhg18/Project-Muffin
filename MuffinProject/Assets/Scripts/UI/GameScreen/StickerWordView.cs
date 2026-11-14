﻿using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>스티커 글자 색 (프로토타입 v2 .gp-title · .gp-stk) — 털색 · 분홍 · 노랑(「!」 「?」).</summary>
    public enum StickerTone
    {
        Fur,
        Pink,
        Bang
    }

    /// <summary>
    /// 스티커 글자 한 줄 (프로토타입 v2 .gp-title · .gp-stk — 121 개판 오분전 시작 제목 · 판정 띠). 글자마다 따로 기울고, 하나씩 튀어나온 뒤 위아래로 살랑인다.
    /// 흰 바깥 테 · 잉크 선은 스티커 글꼴의 머티리얼, 위아래 두 톤 채움은 글자의 세로 그라데이션 (시안은 54% 에서 딱 나뉜다 — 비슷하게).
    /// 글자 템플릿(비활성)을 복제한다. 줄 맞춤은 부모의 HorizontalLayoutGroup.
    /// </summary>
    public class StickerWordView : MonoBehaviour
    {
        private const float PopSeconds = 0.5f;
        private const float PopStagger = 0.075f;
        private const float BobDelay = 0.7f;
        private const float BobStagger = 0.11f;
        private const float BobSeconds = 0.9f;
        private const float SpaceWidthEm = 0.28f;

        [Tooltip("글자 칸 템플릿 (비활성) — 레이아웃 칸. 첫 자식이 스티커 글꼴 · 머티리얼의 글자(움직이는 쪽).")]
        [SerializeField] private RectTransform charTemplate;
        [Tooltip("글자 기울기 순서 (도, 시안 RT) — 글자마다 돌아가며 쓴다.")]
        [SerializeField] private float[] tilts = { -7f, 5f, -4f, 6f, -5f, 8f };
        [Tooltip("털색 — 위 · 아래.")]
        [SerializeField] private Color furTop = new Color32(0xFF, 0xFC, 0xF6, 0xFF);
        [SerializeField] private Color furBottom = new Color32(0xF5, 0xD6, 0xAA, 0xFF);
        [Tooltip("분홍 — 위 · 아래.")]
        [SerializeField] private Color pinkTop = new Color32(0xFF, 0x9D, 0xBB, 0xFF);
        [SerializeField] private Color pinkBottom = new Color32(0xFF, 0x5C, 0x8A, 0xFF);
        [Tooltip("노랑 — 위 · 아래.")]
        [SerializeField] private Color bangTop = new Color32(0xFF, 0xE2, 0x7A, 0xFF);
        [SerializeField] private Color bangBottom = new Color32(0xFF, 0xC2, 0x3F, 0xFF);

        private readonly List<RectTransform> _chars = new List<RectTransform>();
        private readonly List<float> _charTilts = new List<float>();
        private int _tiltOffset;

        private void Awake()
        {
            if (charTemplate == null)
                Debug.LogError($"[{nameof(StickerWordView)}] charTemplate 이 비어 있습니다.", this);
        }

        private void OnDisable() => Clear();

        /// <summary>
        /// 글자를 새로 깐다. 「!」 「?」 는 노랑, 빈칸은 틈. tiltOffset = 기울기 순서의 시작. delay 뒤부터 하나씩 튀어나온다 (음수면 바로 보인다).
        /// </summary>
        public void Set(IReadOnlyList<(string text, StickerTone tone)> parts, int tiltOffset, float delay)
        {
            Clear();
            _tiltOffset = tiltOffset;
            float size = charTemplate.GetComponentInChildren<TMP_Text>(true).fontSize;
            int index = 0;
            foreach ((string text, StickerTone tone) in parts)
            {
                foreach (char ch in text)
                {
                    if (ch == ' ')
                    {
                        AddSpace(size * SpaceWidthEm);
                        continue;
                    }
                    StickerTone charTone = ch == '!' || ch == '?' ? StickerTone.Bang : tone;
                    AddChar(ch, charTone, index++);
                }
            }

            for (int i = 0; i < _chars.Count; i++)
                Animate(i, delay, size);
        }

        private void AddChar(char ch, StickerTone tone, int index)
        {
            RectTransform cell = Instantiate(charTemplate, charTemplate.parent);
            cell.gameObject.name = "Char" + index;
            cell.gameObject.SetActive(true);
            TMP_Text text = cell.GetComponentInChildren<TMP_Text>(true);
            text.text = ch.ToString();
            Vector2 preferred = text.GetPreferredValues(text.text);
            var layout = cell.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.preferredWidth = preferred.x;
            layout.preferredHeight = preferred.y;
            text.enableVertexGradient = true;
            (Color top, Color bottom) = ColorsOf(tone);
            text.color = Color.white;
            text.colorGradient = new VertexGradient(top, top, bottom, bottom);

            // 기울기 · 튀어나옴은 안쪽 글자에 — 레이아웃은 바깥 칸만 본다
            var rect = (RectTransform)text.transform;
            _cells.Add(cell.gameObject);
            float tilt = tilts.Length > 0 ? tilts[(index + _tiltOffset) % tilts.Length] : 0f;
            rect.localRotation = Quaternion.Euler(0f, 0f, -tilt); // 시안 rotate(+) = 시계 방향
            _chars.Add(rect);
            _charTilts.Add(tilt);
        }

        private void AddSpace(float width)
        {
            var go = new GameObject("Space", typeof(RectTransform));
            go.transform.SetParent(charTemplate.parent, false);
            go.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = width;
            _spaces.Add(go);
        }

        private readonly List<GameObject> _spaces = new List<GameObject>();
        private readonly List<GameObject> _cells = new List<GameObject>();

        // 시안 gpPop (0.5초): 아래 .45em · 0.2배 · −25° 에서 → 65% 에 위 .06em · 1.16배 · +6° → 제자리. 그 뒤 gpBob (0.9초 왕복): 위 .045em · 기울기 × −0.6
        private void Animate(int i, float delay, float size)
        {
            RectTransform rect = _chars[i];
            float tilt = _charTilts[i];
            var group = rect.GetComponent<CanvasGroup>();
            if (group == null)
                group = rect.gameObject.AddComponent<CanvasGroup>();

            if (delay < 0f)
                return;

            float start = delay + i * PopStagger;
            Vector2 home = Vector2.zero;
            group.alpha = 0f;
            rect.localScale = Vector3.one * 0.2f;
            rect.localRotation = Quaternion.Euler(0f, 0f, -(tilt - 25f));
            rect.anchoredPosition = home + new Vector2(0f, -0.45f * size);

            Sequence pop = DOTween.Sequence().SetTarget(rect).SetDelay(start);
            pop.Append(group.DOFade(1f, PopSeconds * 0.65f));
            pop.Join(rect.DOScale(1.16f, PopSeconds * 0.65f).SetEase(Ease.OutQuad));
            pop.Join(rect.DOLocalRotate(new Vector3(0f, 0f, -(tilt + 6f)), PopSeconds * 0.65f).SetEase(Ease.OutQuad));
            pop.Join(rect.DOAnchorPos(home + new Vector2(0f, 0.06f * size), PopSeconds * 0.65f).SetEase(Ease.OutQuad));
            pop.Append(rect.DOScale(1f, PopSeconds * 0.35f).SetEase(Ease.InOutQuad));
            pop.Join(rect.DOLocalRotate(new Vector3(0f, 0f, -tilt), PopSeconds * 0.35f).SetEase(Ease.InOutQuad));
            pop.Join(rect.DOAnchorPos(home, PopSeconds * 0.35f).SetEase(Ease.InOutQuad));

            float bobStart = delay + BobDelay + i * BobStagger - (start + PopSeconds);
            Sequence bob = DOTween.Sequence().SetTarget(rect);
            bob.Append(rect.DOAnchorPos(home + new Vector2(0f, 0.045f * size), BobSeconds).SetEase(Ease.InOutSine));
            bob.Join(rect.DOLocalRotate(new Vector3(0f, 0f, tilt * 0.6f), BobSeconds).SetEase(Ease.InOutSine));
            bob.SetLoops(-1, LoopType.Yoyo);
            pop.AppendInterval(Mathf.Max(0f, bobStart));
            pop.OnComplete(() => bob.Play());
            bob.Pause();
        }

        private (Color, Color) ColorsOf(StickerTone tone) => tone switch
        {
            StickerTone.Pink => (pinkTop, pinkBottom),
            StickerTone.Bang => (bangTop, bangBottom),
            _ => (furTop, furBottom)
        };

        private void Clear()
        {
            foreach (RectTransform rect in _chars)
            {
                if (rect == null)
                    continue;
                DOTween.Kill(rect);
            }
            foreach (GameObject cell in _cells)
                if (cell != null)
                    Destroy(cell);
            foreach (GameObject space in _spaces)
                if (space != null)
                    Destroy(space);
            _chars.Clear();
            _charTilts.Clear();
            _spaces.Clear();
            _cells.Clear();
        }
    }
}
