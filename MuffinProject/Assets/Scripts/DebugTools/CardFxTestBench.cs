using System;
using System.Collections;
using System.Collections.Generic;
using Chapchu.Cards;
using Chapchu.Game;
using Chapchu.UI.Fx;
using Chapchu.UI.GameScreen;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.DebugTools
{
    /// <summary>
    /// 카드 연출 테스트 씬 (RPG 스킬 시험장처럼). 아래 손패에서 카드를 누르면 가운데로 날아가 그 카드의 연출이 재생되고,
    /// 연출이 결과를 보이라고 할 때 허수아비 자리의 체력이 준다. 규칙 · 네트워크는 거치지 않는다.
    /// 새 카드 연출은 <see cref="CardFx"/> 를 상속한 프리팹을 만들고 <see cref="entries"/> 에 한 줄 더한다.
    /// 키: 1~9 = 그 카드, Space = 마지막 카드 다시, R = 체력 초기화.
    /// </summary>
    public class CardFxTestBench : MonoBehaviour
    {
        /// <summary>시험할 카드 한 장. 카드 에셋 없이 화면에 보일 것만 적는다.</summary>
        [Serializable]
        public class Entry
        {
            public string cardName;
            public CardType type;
            public Sprite art;
            [TextArea] public string description;
            public CardFx fxPrefab;
            [Tooltip("테스트 표시용 피해. 규칙 수치가 아니다.")]
            public int damage;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        [Header("카드 프리팹 (손패와 같은 것)")]
        [SerializeField] private HandCardView actionCardPrefab;
        [SerializeField] private HandCardView counterCardPrefab;
        [SerializeField] private HandCardView trapCardPrefab;

        [Header("화면")]
        [Tooltip("흔들리는 판. 연출 레이어는 이 밖에 둔다.")]
        [SerializeField] private RectTransform stage;
        [SerializeField] private RectTransform handArea;
        [Tooltip("낸 카드가 날아가 멈추는 자리.")]
        [SerializeField] private RectTransform playSpot;
        [Tooltip("연출 프리팹을 만드는 곳. 화면 전체를 덮는다.")]
        [SerializeField] private RectTransform fxLayer;
        [SerializeField] private FxDummySeat caster;
        [SerializeField] private FxDummySeat target;
        [SerializeField] private TMP_Text captionText;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button resetButton;

        [Header("연출")]
        [Tooltip("손패에서 가운데까지 날아가는 시간.")]
        [SerializeField] private float flyDuration = 0.46f;
        [Tooltip("가운데에서 보여 주는 크기 배율.")]
        [SerializeField] private float playScale = 1.3f;
        [Tooltip("가운데에 멈춰 있는 시간 (연출 시작 전).")]
        [SerializeField] private float holdDuration = 0.35f;

        private readonly List<HandCardView> _handCards = new List<HandCardView>();
        private int _lastIndex = -1;
        private bool _busy;

        private void Awake()
        {
            if (actionCardPrefab == null || counterCardPrefab == null || trapCardPrefab == null || stage == null || handArea == null
                || playSpot == null || fxLayer == null || caster == null || target == null || captionText == null
                || replayButton == null || resetButton == null)
                Debug.LogError($"[{nameof(CardFxTestBench)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            replayButton.onClick.AddListener(HandleReplayClicked);
            resetButton.onClick.AddListener(HandleResetClicked);
        }

        private void OnDisable()
        {
            replayButton.onClick.RemoveListener(HandleReplayClicked);
            resetButton.onClick.RemoveListener(HandleResetClicked);
        }

        private void Start()
        {
            for (int i = 0; i < entries.Count; i++)
                _handCards.Add(CreateHandCard(i));
            captionText.text = entries.Count > 0 ? "카드를 누르면 연출이 나옵니다." : "entries 에 카드를 추가하세요.";
        }

        private void Update()
        {
            for (int i = 0; i < Mathf.Min(entries.Count, 9); i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    Play(i);
            }

            if (Input.GetKeyDown(KeyCode.Space))
                HandleReplayClicked();
            if (Input.GetKeyDown(KeyCode.R))
                HandleResetClicked();
        }

        /// <summary>목록의 index 번 카드를 낸다. 연출 중이면 무시한다.</summary>
        public void Play(int index)
        {
            if (_busy || index < 0 || index >= entries.Count)
                return;
            StartCoroutine(PlayRoutine(index));
        }

        private IEnumerator PlayRoutine(int index)
        {
            _busy = true;
            _lastIndex = index;
            Entry entry = entries[index];
            captionText.text = $"{entry.cardName} — {entry.description}";

            // 손패 자리에서 같은 카드를 하나 더 만들어 가운데로 날린다 (손패 카드는 남겨 둔다 — 몇 번이고 다시 낼 수 있게)
            HandCardView source = _handCards[index];
            HandCardView flying = Instantiate(PrefabOf(entry.type), fxLayer);
            flying.Setup(index, CreateCardData(entry));
            var flyingRect = (RectTransform)flying.transform;
            flyingRect.position = source.transform.position;
            flyingRect.localScale = source.transform.lossyScale.x / fxLayer.lossyScale.x * Vector3.one;
            CanvasGroup group = flying.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            yield return DOTween.Sequence().SetLink(flying.gameObject)
                .Append(flyingRect.DOMove(playSpot.position, flyDuration).SetEase(Ease.OutCubic))
                .Join(flyingRect.DOScale(playScale, flyDuration).SetEase(Ease.OutBack))
                .Join(flyingRect.DOLocalRotate(new Vector3(0f, 0f, 360f), flyDuration, RotateMode.LocalAxisAdd).SetEase(Ease.OutCubic))
                .AppendInterval(holdDuration)
                .Append(group.DOFade(0f, 0.2f))
                .Join(flyingRect.DOScale(playScale * 1.15f, 0.2f))
                .WaitForCompletion();
            Destroy(flying.gameObject);

            if (entry.fxPrefab != null)
            {
                CardFx fx = Instantiate(entry.fxPrefab, fxLayer);
                Stretch((RectTransform)fx.transform);
                var context = new CardFxContext
                {
                    Stage = stage,
                    Caster = (RectTransform)caster.transform,
                    Targets = new[] { (RectTransform)target.transform },
                    Impact = _ => target.TakeDamage(entry.damage),
                };
                yield return fx.PlayRoutine(context);
                Destroy(fx.gameObject);
            }
            else
            {
                Debug.LogWarning($"[{nameof(CardFxTestBench)}] {entry.cardName} 에 연출 프리팹이 없다 — 결과만 보인다.");
                target.TakeDamage(entry.damage);
            }

            _busy = false;
        }

        private void HandleReplayClicked()
        {
            Play(_lastIndex < 0 ? 0 : _lastIndex);
        }

        private void HandleResetClicked()
        {
            caster.ResetHp();
            target.ResetHp();
        }

        private HandCardView CreateHandCard(int index)
        {
            Entry entry = entries[index];
            HandCardView card = Instantiate(PrefabOf(entry.type), handArea);
            card.Setup(index, CreateCardData(entry));

            // 손패 카드에는 입력이 없다 — 시험장에서만 누를 수 있게 투명 판 + 버튼을 붙인다.
            Image hit = card.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.onClick.AddListener(() => Play(index));
            return card;
        }

        // 화면에 보이는 값만 채운 임시 카드 정의 — 에셋으로 저장하지 않는다.
        private static CardData CreateCardData(Entry entry)
        {
            CardData data = ScriptableObject.CreateInstance<CardData>();
            data.cardName = entry.cardName;
            data.type = entry.type;
            data.cardImage = entry.art;
            data.description = entry.description;
            return data;
        }

        private HandCardView PrefabOf(CardType type)
        {
            switch (type)
            {
                case CardType.Counter: return counterCardPrefab;
                case CardType.Trap: return trapCardPrefab;
                default: return actionCardPrefab;
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
