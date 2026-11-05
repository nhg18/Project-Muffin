using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Chapchu.UI.Fx;
using TMPro;
using UnityEngine;

namespace Chapchu.DebugTools
{
    /// <summary>
    /// 이펙트 시험장 — 어두운 바닥 위 대상 자리에서 카드 연출에 쓰인 공통 이펙트를 하나씩, 또는 카드 연출 전체를 재생한다.
    /// 목록은 카드 → 이펙트 종류(같은 공통 이펙트끼리 묶음) → 그 종류의 쓰임. 쓰임이 하나뿐인 종류는 누르면 바로 재생한다.
    /// 이펙트 하나만 볼 때는 한 점 이펙트가 대상 자리 가운데에서 나온다. 장면(그림 애니메이션)은 목록에 없고 전체 재생에서만 나온다.
    /// 재생할 때마다 프리팹을 새로 만들어서, 플레이 중에 프리팹(Prefab Mode)에서 고친 값이 다음 재생에 바로 들어가고 플레이가 끝나도 남는다.
    /// 키: ↑ ↓ = 고르기, Space · Enter = 열기 / 재생, A = 카드 전체, B = 이펙트 뒤에 장면 깔기, Esc · Backspace = 돌아가기.
    /// </summary>
    public class CardFxLab : MonoBehaviour
    {
        // 목록 글자 크기는 화면 높이 1080 기준
        private const float GuiReferenceHeight = 1080f;
        private const float ListWidth = 500f;
        private const float RowHeight = 32f;
        private const int None = -1;

        /// <summary>시험할 카드 연출 하나.</summary>
        [Serializable]
        public class Card
        {
            [Tooltip("목록에 보일 카드 이름.")]
            public string cardName;
            public CardFx fx;
        }

        // 같은 공통 이펙트(컴포넌트 종류)의 쓰임들
        private class Kind
        {
            public string Name;
            public readonly List<int> Effects = new List<int>();
        }

        [Tooltip("시험할 카드 연출들. 목록 첫 단계에 버튼으로 나온다.")]
        [SerializeField] private List<Card> cards = new List<Card>();

        [Header("화면")]
        [Tooltip("연출 레이어. 화면 전체를 덮고, 카드 연출 프리팹도 여기에 만든다.")]
        [SerializeField] private FxLayer fxLayer;
        [Tooltip("대상 자리. 이펙트 하나만 볼 때 여기 가운데에서 나오고, 마무리 이펙트(대상 칼질 · 터짐)도 여기서 재생된다.")]
        [SerializeField] private RectTransform target;
        [SerializeField] private TMP_Text captionText;

        [Header("재생")]
        [Tooltip("배속 (Time.timeScale). 1 = 원래 속도. 플레이 중에 바꿔도 바로 적용된다.")]
        [Range(0.05f, 2f)]
        [SerializeField] private float speed = 1f;
        [Tooltip("이펙트 하나만 볼 때 그 시각의 장면(첫 모습)을 뒤에 깔고, 이펙트를 장면 속 제자리에서 낸다.")]
        [SerializeField] private bool showScenery;
        [Tooltip("끝나면 같은 것을 다시 재생한다.")]
        [SerializeField] private bool loop;

        private readonly List<string> _effectLabels = new List<string>();
        private readonly List<Kind> _kinds = new List<Kind>();
        private int _card = None;
        private int _kind = None;
        private int _selected;
        private int _effect;
        private bool _playAll;
        private Coroutine _playing;
        private GameObject _current;
        private Vector2 _scroll;

        private int RowCount
        {
            get
            {
                if (_card == None)
                    return cards.Count;
                return _kind == None ? _kinds.Count : _kinds[_kind].Effects.Count;
            }
        }

        private void Awake()
        {
            if (cards == null || cards.Count == 0 || cards.Any(card => card.fx == null) || fxLayer == null || target == null || captionText == null)
                Debug.LogError($"[{nameof(CardFxLab)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        // 인스펙터에서 배속을 바꾸면 플레이 중에도 바로 적용
        private void OnValidate()
        {
            if (Application.isPlaying)
                Time.timeScale = speed;
        }

        // timeScale 은 씬을 넘어 남는다 — 시험장을 벗어나면 원래 속도로
        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        private void Start()
        {
            Time.timeScale = speed;
            ShowCards();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
                GoBack();
            if (RowCount == 0)
                return;

            if (Input.GetKeyDown(KeyCode.UpArrow))
                _selected = (_selected + RowCount - 1) % RowCount;
            if (Input.GetKeyDown(KeyCode.DownArrow))
                _selected = (_selected + 1) % RowCount;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                Choose(_selected);
            if (_card == None)
                return;
            if (Input.GetKeyDown(KeyCode.A))
                PlayAll();
            if (Input.GetKeyDown(KeyCode.B))
                showScenery = !showScenery;
        }

        private void OnGUI()
        {
            float scale = Screen.height / GuiReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.skin.button.fontSize = 17;
            GUI.skin.button.alignment = TextAnchor.MiddleLeft;
            GUI.skin.toggle.fontSize = 17;
            GUI.skin.label.fontSize = 17;

            GUILayout.BeginArea(new Rect(16f, 16f, ListWidth, GuiReferenceHeight - 32f));
            if (_card == None)
            {
                GUILayout.Label("카드 연출");
            }
            else
            {
                string back = _kind == None ? "← 돌아가기 (Esc)" : $"← {cards[_card].cardName} (Esc)";
                if (GUILayout.Button(back, GUILayout.Height(RowHeight)))
                    GoBack();
                if (GUILayout.Button($"▶ {cards[_card].cardName} 전체 (A)", GUILayout.Height(RowHeight)))
                    PlayAll();
                showScenery = GUILayout.Toggle(showScenery, " 이펙트 뒤에 장면 (B)");
                loop = GUILayout.Toggle(loop, " 반복");
                if (_kind != None)
                    GUILayout.Label($"{_kinds[_kind].Name}  ×{_kinds[_kind].Effects.Count}");
            }

            _scroll = GUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < RowCount; i++)
            {
                string mark = i == _selected ? "▶ " : "    ";
                if (GUILayout.Button(mark + RowLabel(i), GUILayout.Height(RowHeight)))
                    Choose(i);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // 카드: 「지옥참마도」 · 종류: 「FlareBurst  ×3  ▸」 또는 쓰임 하나면 「Flash  · 절 번쩍」 · 쓰임: 「이펙트  0130  칼집 빛 — 살짝」
        private string RowLabel(int row)
        {
            if (_card == None)
                return cards[row].cardName;
            if (_kind != None)
                return _effectLabels[_kinds[_kind].Effects[row]];

            Kind kind = _kinds[row];
            return kind.Effects.Count > 1 ? $"{kind.Name}  ×{kind.Effects.Count}  ▸" : $"{kind.Name}  · {_effectLabels[kind.Effects[0]]}";
        }

        private void Choose(int row)
        {
            _selected = row;
            if (_card == None)
            {
                OpenCard(row);
                return;
            }

            if (_kind != None)
            {
                Play(_kinds[_kind].Effects[row]);
                return;
            }

            Kind kind = _kinds[row];
            if (kind.Effects.Count == 1)
            {
                Play(kind.Effects[0]);
                return;
            }

            _kind = row;
            _selected = 0;
            _scroll = Vector2.zero;
            captionText.text = $"{cards[_card].cardName} · {kind.Name} — {kind.Effects.Count}군데에 쓰였다.";
        }

        private void OpenCard(int card)
        {
            _card = card;
            _kind = None;
            _selected = 0;
            _scroll = Vector2.zero;
            CardFx probe = CreateFx();
            ReadEffects(probe);
            Destroy(probe.gameObject);
            captionText.text = $"{cards[_card].cardName} — 이펙트를 고르고 Space, 전체는 A.";
        }

        // 종류 안 → 카드 안 → 카드 목록
        private void GoBack()
        {
            if (_kind != None)
            {
                _selected = _kind;
                _kind = None;
                _scroll = Vector2.zero;
                captionText.text = $"{cards[_card].cardName} — 이펙트를 고르고 Space, 전체는 A.";
                return;
            }

            if (_card != None)
                ShowCards();
        }

        private void ShowCards()
        {
            Stop();
            _selected = _card == None ? 0 : _card;
            _card = None;
            _kind = None;
            _effectLabels.Clear();
            _kinds.Clear();
            captionText.text = "카드를 고르세요.";
        }

        private void Play(int effect)
        {
            _effect = effect;
            _playAll = false;
            Restart();
        }

        private void PlayAll()
        {
            _playAll = true;
            Restart();
        }

        private void Restart()
        {
            Stop();
            _playing = StartCoroutine(PlayRoutine());
        }

        private void Stop()
        {
            if (_playing != null)
                StopCoroutine(_playing);
            _playing = null;
            Clear();
        }

        private IEnumerator PlayRoutine()
        {
            do
            {
                Clear();
                // 매번 프리팹에서 새로 — 고친 값을 바로 쓴다
                CardFx fx = CreateFx();
                _current = fx.gameObject;
                var context = new CardFxContext { Layer = fxLayer, Targets = new[] { target } };
                List<CardFxStep> effects = EffectsOf(fx);

                if (_playAll)
                {
                    captionText.text = $"{cards[_card].cardName} — 전체";
                    yield return fx.PlayRoutine(context);
                }
                else if (_effect < effects.Count)
                {
                    CardFxStep step = effects[_effect];
                    captionText.text = $"{fx.GroupOf(step)} · {step.name}  ({KindName(step)} · {step.StartMs:0}ms 부터 {step.DurationMs:0}ms)";
                    yield return fx.PlayStepRoutine(step, context, showScenery);
                }
            } while (loop);

            Clear();
            _playing = null;
        }

        private void Clear()
        {
            if (_current != null)
                Destroy(_current);
            _current = null;
        }

        private CardFx CreateFx()
        {
            CardFx fx = Instantiate(cards[_card].fx, fxLayer.transform);
            var rect = (RectTransform)fx.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return fx;
        }

        // 카드에 쓰인 이펙트만 (장면은 빼고), 연출 순서대로
        private static List<CardFxStep> EffectsOf(CardFx fx) => fx.Steps.Where(step => !step.IsScene).ToList();

        // 쓰임 줄과, 처음 나오는 순서대로 묶은 종류
        private void ReadEffects(CardFx fx)
        {
            _effectLabels.Clear();
            _kinds.Clear();
            List<CardFxStep> effects = EffectsOf(fx);
            for (int i = 0; i < effects.Count; i++)
            {
                CardFxStep step = effects[i];
                _effectLabels.Add($"{fx.GroupOf(step)}  {step.StartMs:0000}  {step.name}");

                string name = KindName(step);
                Kind kind = _kinds.FirstOrDefault(k => k.Name == name);
                if (kind == null)
                {
                    kind = new Kind { Name = name };
                    _kinds.Add(kind);
                }
                kind.Effects.Add(i);
            }
        }

        // 공통 이펙트 종류 이름 (클래스 이름에서 Fx 를 뗀다)
        private static string KindName(CardFxStep step)
        {
            string name = step.GetType().Name;
            return name.EndsWith("Fx") ? name.Substring(0, name.Length - 2) : name;
        }
    }
}
