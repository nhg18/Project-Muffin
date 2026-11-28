using System.Collections;
using System.Collections.Generic;
using Chapchu.Core;
using Chapchu.Game;
using Chapchu.UI.Fx;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 게임 시작 연출 (10-ui.md 5-2 「게임 시작 연출」, 프로토타입 v2 intro) — 새 판을 시작할 때 한 번.
    /// ① 턴 순서 카드 섞기 → ② 번호 공개 · 「{이름}부터 시작!」 → ③ 카드가 각자 자리로 날아가며 캐릭터 · 이름표 등장 → 턴 순서 띠 등장. 손패 배분은 뒤따르는 뽑기 알림이 한다.
    /// 방장은 이 길이(GameServer.OpeningSeconds)만큼 첫 턴을 멈춰 두므로 건너뛰기는 없다 — 눌러도 빨리 시작되지 않는다.
    /// 「게임 시작!」 외침은 화면 전환(15-screen 9-4)이 이미 했으므로 여기서는 하지 않는다. 표시만 — 판정과 상관없다.
    /// </summary>
    public class GameStartIntroView : MonoBehaviour
    {
        /// <summary>카드 한 장이 갈 자리 — 순서대로 넘긴다.</summary>
        public struct Seat
        {
            public string Name;
            public Color Color;
            public Sprite Body;
            public Sprite Accessory;
            public bool IsMe;
            /// <summary>카드가 날아갈 화면 좌표 (캐릭터 가운데).</summary>
            public Vector2 ScreenPoint;
            /// <summary>카드가 닿을 때 튀어나오는 것 — 처음에는 숨겨 둔다.</summary>
            public RectTransform[] Reveal;
        }

        private const string ShufflingText = "턴 순서 정하는 중…";
        private const string FirstFormat = "{0}부터 시작!";
        private const string FirstMeText = "내가 먼저 시작!";

        [Header("부품 (꺼 둔 채로 저장)")]
        [Tooltip("연출 전체 — 재생하는 동안만 켠다. 화면 가운데가 원점.")]
        [SerializeField] private RectTransform root;
        [Tooltip("카드 틀 — 사람 수만큼 복제한다.")]
        [SerializeField] private StartIntroCardView cardTemplate;
        [Tooltip("제목 판 「턴 순서 정하는 중…」 → 「{이름}부터 시작!」.")]
        [SerializeField] private RectTransform titlePanel;
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField] private TMP_Text titleText;
        [Tooltip("카드가 닿은 자리에서 퍼지는 고리 틀 — 그 사람 색 테. root 밖, root 와 같은 화면 전체 부모 아래에 둔다.")]
        [SerializeField] private UIShape ringTemplate;

        [Header("배치 (시안 v2 × 2)")]
        [Tooltip("카드 줄 가운데의 높이 — 화면 가운데 기준 (시안 y 262 → +16).")]
        [SerializeField] private float rowY = 16f;
        [SerializeField] private float cardWidth = 184f;
        [SerializeField] private float cardGap = 44f;
        [Tooltip("섞을 때 위아래로 흔드는 폭 (위, 아래).")]
        [SerializeField] private Vector2 shuffleLift = new Vector2(44f, -36f);
        [Tooltip("날아갈 때 가운데에서 떠오르는 높이.")]
        [SerializeField] private float flyLift = 80f;
        [SerializeField] private float ringSize = 180f;

        [Header("시간 (초) — 바꾸면 GameServer.OpeningIntroSeconds 도")]
        [SerializeField] private float shuffleSeconds = 1.5f;
        [SerializeField] private int shuffleSteps = 4;
        [SerializeField] private float flipAt = 1.65f;
        [SerializeField] private float flipInterval = 0.28f;
        [SerializeField] private float flipHalfSeconds = 0.11f;
        [SerializeField] private float flipPopSeconds = 0.22f;
        [Tooltip("마지막 공개 뒤 자리로 보내기 전까지.")]
        [SerializeField] private float afterFlipSeconds = 0.5f;
        [SerializeField] private float flyDelay = 0.45f;
        [SerializeField] private float flyInterval = 0.2f;
        [SerializeField] private float flySeconds = 0.42f;
        [SerializeField] private float revealSeconds = 0.5f;
        [SerializeField] private float ringSeconds = 0.52f;
        [Tooltip("마지막 카드가 떠난 뒤 끝내기까지.")]
        [SerializeField] private float tailSeconds = 0.5f;
        [Tooltip("끝나기 이만큼 전에 턴 순서 띠가 나온다.")]
        [SerializeField] private float stripLead = 0.15f;

        private readonly List<StartIntroCardView> _cards = new List<StartIntroCardView>();
        private readonly Dictionary<RectTransform, Vector3> _hidden = new Dictionary<RectTransform, Vector3>(); // 숨긴 것 → 원래 크기

        private void Awake()
        {
            if (root == null || cardTemplate == null || titlePanel == null || titleGroup == null || titleText == null || ringTemplate == null)
                Debug.LogError($"[{nameof(GameStartIntroView)}] 참조 중 비어 있는 것이 있습니다.", this);

            root.gameObject.SetActive(false);
            cardTemplate.gameObject.SetActive(false);
            ringTemplate.gameObject.SetActive(false);
        }

        // 중간에 화면을 떠나도 숨긴 좌석이 숨은 채 남지 않게
        private void OnDisable() => Finish();

        /// <summary>연출의 길이 — 방장이 첫 턴을 멈춰 두는 GameServer.OpeningIntroSeconds 와 같아야 한다.</summary>
        public float DurationFor(int count) => flipAt + flipInterval * count + afterFlipSeconds + flyDelay + flyInterval * count + tailSeconds;

        /// <summary>
        /// seats 는 턴 순서대로. 끝날 때까지 기다리는 코루틴 — 연출 큐의 한 단계.
        /// strip 은 마지막에 튀어나오는 턴 순서 띠 (없어도 된다).
        /// </summary>
        public IEnumerator Play(IReadOnlyList<Seat> seats, RectTransform strip)
        {
            int count = seats.Count;
            if (count == 0)
                yield break;

            float duration = DurationFor(count);
            if (duration > GameServer.OpeningIntroSeconds(count) + 0.01)
                Debug.LogWarning($"[{nameof(GameStartIntroView)}] 연출 {duration:0.00}초가 방장이 멈춰 두는 {GameServer.OpeningIntroSeconds(count):0.00}초보다 길다 — 첫 턴 시간을 먹는다.", this);

            // 처음 — 자리 · 띠를 숨기고 카드를 가운데 줄에 뒷면으로
            for (int i = 0; i < count; i++)
                Hide(seats[i].Reveal);
            if (strip != null)
                Hide(new[] { strip });

            // 화면 전환 덮개가 걷힌 뒤에 시작한다 (10-ui.md 2-2 #7 · 15-screen 9-4) — 자리는 먼저 숨겨 덮개가 걷힐 때 보였다 사라지지 않게
            while (ScreenTransition.IsPlaying)
                yield return null;

            root.gameObject.SetActive(true);
            titleText.text = ShufflingText;
            titleGroup.alpha = 1f;
            Vector2 titleHome = titlePanel.anchoredPosition;

            float step = cardWidth + cardGap;
            float x0 = -(count - 1) * step / 2f;
            for (int i = 0; i < count; i++)
            {
                StartIntroCardView card = Instantiate(cardTemplate, root);
                card.gameObject.SetActive(true);
                card.Set(i + 1, seats[i].Name, seats[i].Color, seats[i].Body, seats[i].Accessory, seats[i].IsMe);
                card.Rect.anchoredPosition = new Vector2(x0 + i * step, rowY);
                card.Rect.localScale = Vector3.one;
                card.Group.alpha = 1f;
                _cards.Add(card);
            }
            titlePanel.SetAsLastSibling();

            float startedAt = Time.time;

            // ① 섞기 — 자리를 몇 번 바꿨다가 제자리로 (카드 i = 턴 순서 i 번째)
            SoundManager.Play(Sfx.CardDraw);
            float segment = shuffleSeconds / (shuffleSteps + 1);
            int[][] slots = new int[shuffleSteps][];
            for (int s = 0; s < shuffleSteps; s++)
                slots[s] = Permutation(count);
            for (int i = 0; i < count; i++)
            {
                Sequence sequence = DOTween.Sequence().SetTarget(_cards[i].Rect);
                for (int s = 0; s < shuffleSteps; s++)
                {
                    float y = rowY + (s % 2 == 0 ? shuffleLift.x : shuffleLift.y);
                    sequence.Append(_cards[i].Rect.DOAnchorPos(new Vector2(x0 + slots[s][i] * step, y), segment).SetEase(Ease.InOutSine));
                }
                sequence.Append(_cards[i].Rect.DOAnchorPos(new Vector2(x0 + i * step, rowY), segment).SetEase(Ease.InOutSine));
            }
            for (int s = 1; s < shuffleSteps; s += 2)
                StartCoroutine(PlayAt(startedAt + segment * s, Sfx.CardDraw));

            // ② 순서대로 뒤집기
            for (int i = 0; i < count; i++)
            {
                yield return WaitUntil(startedAt + flipAt + flipInterval * i);
                Flip(_cards[i]);
            }

            float t2 = flipAt + flipInterval * count + afterFlipSeconds;
            yield return WaitUntil(startedAt + t2 - 0.25f);
            titleText.text = seats[0].IsMe ? FirstMeText : string.Format(FirstFormat, seats[0].Name);
            titlePanel.DOKill();
            titlePanel.localScale = Vector3.one * 1.08f;
            titlePanel.DOScale(1f, 0.2f).SetEase(Ease.OutQuad);

            yield return WaitUntil(startedAt + t2 + 0.35f);
            titleGroup.DOFade(0f, 0.26f);
            titlePanel.DOAnchorPosY(titleHome.y + 32f, 0.26f);

            // ③ 자리로 — 한 장씩 날아가고, 닿으면 그 자리가 튀어나온다
            for (int i = 0; i < count; i++)
            {
                yield return WaitUntil(startedAt + t2 + flyDelay + flyInterval * i);
                Fly(_cards[i], seats[i]);
            }

            float t3 = t2 + flyDelay + flyInterval * count + tailSeconds;
            yield return WaitUntil(startedAt + t3 - stripLead);
            if (strip != null)
                Reveal(strip);

            yield return WaitUntil(startedAt + t3);
            titlePanel.anchoredPosition = titleHome;
            Finish();
        }

        private void Flip(StartIntroCardView card)
        {
            RectTransform rect = card.Rect;
            rect.DOKill();
            DOTween.Sequence().SetTarget(rect)
                .Append(rect.DOScaleX(0f, flipHalfSeconds).SetEase(Ease.InQuad))
                .AppendCallback(() =>
                {
                    card.ShowFront(true);
                    SoundManager.Play(Sfx.CardPlay);
                })
                .Append(rect.DOScale(new Vector3(1.12f, 1.12f, 1f), flipPopSeconds * 0.6f).SetEase(Ease.OutQuad))
                .Append(rect.DOScale(Vector3.one, flipPopSeconds * 0.4f).SetEase(Ease.InOutQuad));
        }

        private void Fly(StartIntroCardView card, Seat seat)
        {
            RectTransform rect = card.Rect;
            Vector2 from = rect.anchoredPosition;
            Vector2 to = ToLocal(seat.ScreenPoint);
            Vector2 middle = Vector2.Lerp(from, to, 0.5f) + new Vector2(0f, flyLift);
            float first = flySeconds * 0.55f;

            rect.DOKill();
            card.Group.DOKill();
            DOTween.Sequence().SetTarget(rect)
                .Append(rect.DOAnchorPos(middle, first).SetEase(Ease.InSine))
                .Join(rect.DOScale(0.7f, first).SetEase(Ease.InSine))
                .Append(rect.DOAnchorPos(to, flySeconds - first).SetEase(Ease.OutSine))
                .Join(rect.DOScale(0.3f, flySeconds - first).SetEase(Ease.OutSine))
                .Join(card.Group.DOFade(0f, flySeconds - first).SetEase(Ease.InQuad));

            DOVirtual.DelayedCall(flySeconds * 0.95f, () =>
            {
                if (seat.Reveal != null)
                {
                    foreach (RectTransform part in seat.Reveal)
                        Reveal(part);
                }
                Ring(to, seat.Color);
                SoundManager.Play(Sfx.TrapSet);
            }).SetTarget(rect);
        }

        // 닿은 자리에서 퍼지는 고리
        private void Ring(Vector2 at, Color color)
        {
            UIShape ring = Instantiate(ringTemplate, ringTemplate.transform.parent); // 연출 판(root) 밖 — 판을 끈 뒤에도 끝까지 퍼진다. 둘 다 화면 전체라 좌표가 같다
            ring.gameObject.SetActive(true);
            ring.BorderColor = color;
            RectTransform rect = ring.rectTransform;
            rect.anchoredPosition = at;
            rect.sizeDelta = new Vector2(ringSize, ringSize);
            rect.localScale = Vector3.one * 0.3f;
            ring.color = Color.clear;
            ring.canvasRenderer.SetAlpha(1f);
            DOTween.Sequence().SetTarget(rect)
                .Append(rect.DOScale(1.25f, ringSeconds).SetEase(Ease.OutCubic))
                .Join(DOVirtual.Float(1f, 0f, ringSeconds, a => ring.canvasRenderer.SetAlpha(a)).SetEase(Ease.InQuad))
                .OnComplete(() => Destroy(ring.gameObject));
        }

        private void Hide(RectTransform[] parts)
        {
            if (parts == null)
                return;

            foreach (RectTransform part in parts)
            {
                if (part == null || _hidden.ContainsKey(part))
                    continue;
                part.DOKill();
                _hidden[part] = part.localScale;
                part.localScale = Vector3.zero;
            }
        }

        // 숨긴 것을 원래 크기로 튀어나오게 (시안 scale .35 → 1, cubic-bezier(.3,1.7,.5,1))
        private void Reveal(RectTransform part)
        {
            if (part == null || !_hidden.TryGetValue(part, out Vector3 scale))
                return;

            _hidden.Remove(part);
            part.DOKill();
            part.localScale = scale * 0.35f;
            part.DOScale(scale, revealSeconds).SetEase(Ease.OutBack, 2.6f);
        }

        // 끝 · 중단 — 카드를 치우고 숨긴 것을 모두 되돌린다
        private void Finish()
        {
            foreach (StartIntroCardView card in _cards)
            {
                if (card == null)
                    continue;
                card.Rect.DOKill();
                card.Group.DOKill();
                Destroy(card.gameObject);
            }
            _cards.Clear();

            foreach (KeyValuePair<RectTransform, Vector3> pair in _hidden)
            {
                if (pair.Key == null)
                    continue;
                pair.Key.DOKill();
                pair.Key.localScale = pair.Value;
            }
            _hidden.Clear();

            if (titleGroup != null)
                titleGroup.DOKill();
            if (titlePanel != null)
                titlePanel.DOKill();
            if (root != null)
                root.gameObject.SetActive(false);
        }

        // 0 ~ count−1 을 섞은 것 — 섞는 한 번에 카드들이 서로 자리를 바꾼다
        private static int[] Permutation(int count)
        {
            int[] order = new int[count];
            for (int i = 0; i < count; i++)
                order[i] = i;
            for (int i = count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }

        private Vector2 ToLocal(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, null, out Vector2 local);
            return local;
        }

        private static IEnumerator WaitUntil(float time)
        {
            while (Time.time < time)
                yield return null;
        }

        private static IEnumerator PlayAt(float time, Sfx sfx)
        {
            yield return WaitUntil(time);
            SoundManager.Play(sfx);
        }
    }
}
