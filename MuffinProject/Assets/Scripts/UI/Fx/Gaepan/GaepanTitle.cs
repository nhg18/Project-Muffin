using System;
using TMPro;
using UnityEngine;

namespace Chapchu.UI.Fx.Gaepan
{
    /// <summary>
    /// 개판 오분전 제목 「개판 / 오분전!」 (시안 .gp-title · .dp-title). 두 낱말을 위아래로 놓고 글자를 하나씩 튀어나오게 한다.
    /// 크기는 화면 짧은 변(vmin) 비율이고, 화면 크기가 바뀌면 다시 놓는다. 채움 두 톤(위 54% · 아래)은 실행할 때 만든 면 텍스처로 칠한다.
    /// 길이 단위는 이 판의 단위 (시안 1px = 1).
    /// </summary>
    public class GaepanTitle : MonoBehaviour
    {
        /// <summary>낱말 하나 (시안 .w1 · .w2).</summary>
        [Serializable]
        public struct Word
        {
            [Tooltip("글자들, 왼쪽부터.")]
            public GaepanLetter[] letters;
            [Tooltip("글자 크기 = clamp(x, y × vmin, z).")]
            public Vector3 size;
            [Tooltip("이 낱말이 나오기 시작하는 시각 (초, 연출 처음부터 — 시안 --d).")]
            public float delay;
            [Tooltip("위 낱말과 겹치는 양 (em, 시안 margin-top 의 반대).")]
            public float overlap;
            [Tooltip("왼쪽 여백 (em, 시안 margin-left).")]
            public float indent;
        }

        /// <summary>채움 두 톤 하나.</summary>
        [Serializable]
        public struct Tone
        {
            [Tooltip("이 색을 쓰는 글자들.")]
            public TMP_Text[] letters;
            public Color top;
            public Color bottom;
        }

        private const string FaceTexProperty = "_FaceTex";

        [Tooltip("크기 · 자리의 기준이 되는 판 (연출 전체).")]
        [SerializeField] private RectTransform frame;
        [SerializeField] private Word[] words;
        [Tooltip("채움 겹의 머티리얼 (면 텍스처를 쓰는 SDF). 톤마다 복사해 텍스처만 바꾼다.")]
        [SerializeField] private Material fillMaterial;
        [SerializeField] private Tone[] tones;
        [Tooltip("두 톤이 갈리는 자리 — 글자 줄 위에서부터 (시안 linear-gradient 54%).")]
        [SerializeField] private float toneSplit = 0.54f;
        [Tooltip("자간 (em, 시안 letter-spacing).")]
        [SerializeField] private float letterSpacing = -0.01f;
        [Tooltip("가로 화면에서 제목 가운데 높이 (화면 위에서부터 비율, 시안 top).")]
        [SerializeField] private float topLandscape = 0.22f;
        [Tooltip("세로 화면에서 제목 가운데 높이.")]
        [SerializeField] private float topPortrait = 0.16f;

        private RectTransform _rect;
        private Vector2 _laidOut;
        private Material[] _materials = new Material[0];
        private Texture2D[] _textures = new Texture2D[0];

        private void Awake()
        {
            if (frame == null || words == null || fillMaterial == null || tones == null)
                Debug.LogError($"[{nameof(GaepanTitle)}] 참조 중 비어 있는 것이 있습니다.", this);
            _rect = (RectTransform)transform;
            PaintTones();
        }

        private void OnDestroy()
        {
            foreach (Material material in _materials)
                Destroy(material);
            foreach (Texture2D texture in _textures)
                Destroy(texture);
        }

        /// <summary>연출 처음부터 time 초의 모습.</summary>
        public void Evaluate(float time)
        {
            Vector2 size = frame.rect.size;
            if (size != _laidOut)
                Layout(size);
            foreach (Word word in words)
            {
                foreach (GaepanLetter letter in word.letters)
                    letter.Evaluate(time - word.delay);
            }
        }

        /// <summary>판 크기에 맞춰 낱말 · 글자를 놓는다 (시안 flex 세로 줄 · 가운데 맞춤).</summary>
        public void Layout(Vector2 frameSize)
        {
            if (_rect == null)
                _rect = (RectTransform)transform;
            _laidOut = frameSize;
            float vmin = Mathf.Min(frameSize.x, frameSize.y) / 100f;
            int count = words.Length;
            var em = new float[count];
            var width = new float[count];
            float columnWidth = 0f, columnHeight = 0f;
            for (int w = 0; w < count; w++)
            {
                Word word = words[w];
                em[w] = Mathf.Clamp(word.size.y * vmin, word.size.x, word.size.z);
                foreach (GaepanLetter letter in word.letters)
                    width[w] += (letter.Advance() + letterSpacing) * em[w];
                columnWidth = Mathf.Max(columnWidth, width[w] + word.indent * em[w]);
                columnHeight += em[w] - (w > 0 ? word.overlap * em[w] : 0f);
            }

            // 낱말 칸은 가운데 맞춤 (왼쪽 여백 포함), 판 가운데가 제목 가운데
            float top = columnHeight / 2f;
            for (int w = 0; w < count; w++)
            {
                Word word = words[w];
                if (w > 0)
                    top += word.overlap * em[w];
                float x = -(width[w] + word.indent * em[w]) / 2f + word.indent * em[w];
                float y = top - em[w] / 2f;
                foreach (GaepanLetter letter in word.letters)
                {
                    float cell = (letter.Advance() + letterSpacing) * em[w];
                    letter.Place(new Vector2(x + cell / 2f, y), cell, em[w]);
                    x += cell;
                }
                top -= em[w];
            }

            float fromTop = frameSize.x >= frameSize.y ? topLandscape : topPortrait;
            _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 1f - fromTop);
            _rect.anchoredPosition = Vector2.zero;
            _rect.sizeDelta = new Vector2(columnWidth, columnHeight);
        }

        // 톤마다 1 × 2 텍스처(아래 · 위)와 머티리얼 — 텍스처 자리를 옮겨 갈리는 선을 toneSplit 에 맞춘다 (채움 겹은 줄 기준 세로 맵핑)
        private void PaintTones()
        {
            _materials = new Material[tones.Length];
            _textures = new Texture2D[tones.Length];
            for (int i = 0; i < tones.Length; i++)
            {
                var texture = new Texture2D(1, 2, TextureFormat.RGBA32, false)
                {
                    name = $"GaepanTitleTone{i}",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                texture.SetPixels(new[] { tones[i].bottom, tones[i].top });
                texture.Apply(false, true);
                var material = new Material(fillMaterial) { name = $"{fillMaterial.name} ({i})" };
                material.SetTexture(FaceTexProperty, texture);
                material.SetTextureOffset(FaceTexProperty, new Vector2(0f, toneSplit - 0.5f));
                _textures[i] = texture;
                _materials[i] = material;
                foreach (TMP_Text letter in tones[i].letters)
                    letter.fontSharedMaterial = material;
            }
        }
    }
}
