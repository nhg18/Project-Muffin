using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 로비 왼쪽의 내 캐릭터 — 홈에서는 누르면 꾸미기, 섹션에서는 고른 아이템 미리보기.
    /// 그림이 있는 것은 기본 캐릭터 뿐이라 나머지는 검은 실루엣으로 보인다. 「재생」 은 기본 연출로 대신한다.
    /// 기준: 「통합 화면 UI v2」 로비 .hero · replay()
    /// </summary>
    public class LobbyHeroView : MonoBehaviour
    {
        private const float EnterSeconds = 0.3f;
        private const float ReplaySeconds = 1.5f;

        private static readonly Color DeathTint = new Color(0.55f, 0.55f, 0.55f, 1f);

        [SerializeField] private Button button;
        [SerializeField] private Image character;
        [SerializeField] private RectTransform cardPreview;
        [SerializeField] private CanvasGroup cardPreviewGroup;
        [SerializeField] private Image cardPreviewImage;

        [Tooltip("카드 미리보기가 날아오는 시작 위치 (캔버스 px) · 기울기")]
        [SerializeField] private Vector2 cardFlyOffset = new Vector2(120f, -60f);
        [SerializeField] private float cardFlyAngle = -20f;
        [SerializeField] private float cardRestAngle = 6f;

        private Vector2 _cardOrigin;
        private bool _isSilhouette;
        private Sequence _replay;

        public event Action OnClicked;

        private void Awake()
        {
            if (button == null || character == null || cardPreview == null || cardPreviewGroup == null || cardPreviewImage == null)
                Debug.LogError($"[{nameof(LobbyHeroView)}] 참조가 비어 있습니다.", this);

            _cardOrigin = cardPreview.anchoredPosition;
            cardPreviewGroup.alpha = 0f;
        }

        private void OnEnable() => button.onClick.AddListener(HandleClicked);

        private void OnDisable()
        {
            button.onClick.RemoveListener(HandleClicked);
            StopReplay();
        }

        /// <summary>그림이 없는 캐릭터면 실루엣으로.</summary>
        public void SetSilhouette(bool isSilhouette)
        {
            StopReplay();
            _isSilhouette = isSilhouette;
            character.color = BaseColor;
        }

        /// <summary>장착 — 캐릭터가 톡 튄다.</summary>
        public void Pop() => UIJuice.Pop(character.rectTransform, 0.94f);

        /// <summary>입장 연출 자리 — 0.3초에 나타난다.</summary>
        public void PlayEnter()
        {
            StopReplay();
            Color target = BaseColor;
            character.color = new Color(target.r, target.g, target.b, 0f);
            _replay = DOTween.Sequence().SetUpdate(true).Append(character.DOColor(target, EnterSeconds));
        }

        /// <summary>사망 연출 자리 — 잠깐 잿빛이 됐다 돌아온다.</summary>
        public void PlayDeath()
        {
            StopReplay();
            if (_isSilhouette)
                return;

            _replay = DOTween.Sequence().SetUpdate(true)
                .Append(character.DOColor(DeathTint, ReplaySeconds * 0.33f))
                .AppendInterval(ReplaySeconds * 0.47f)
                .Append(character.DOColor(Color.white, ReplaySeconds * 0.2f));
        }

        /// <summary>카드 뒷면 미리보기 — 오른쪽 아래에서 날아와 잠깐 보이고 사라진다. owned 가 아니면 뒷면 그림이 없어 옅게 보인다.</summary>
        public void PlayCard(bool owned)
        {
            StopReplay();
            cardPreviewImage.color = owned ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            cardPreview.anchoredPosition = _cardOrigin + cardFlyOffset;
            cardPreview.localEulerAngles = new Vector3(0f, 0f, cardFlyAngle);
            cardPreviewGroup.alpha = 0f;
            _replay = DOTween.Sequence().SetUpdate(true)
                .Append(cardPreview.DOAnchorPos(_cardOrigin, ReplaySeconds * 0.35f).SetEase(Ease.OutQuad))
                .Join(cardPreview.DOLocalRotate(new Vector3(0f, 0f, cardRestAngle), ReplaySeconds * 0.35f).SetEase(Ease.OutQuad))
                .Join(cardPreviewGroup.DOFade(1f, ReplaySeconds * 0.35f))
                .AppendInterval(ReplaySeconds * 0.5f)
                .Append(cardPreviewGroup.DOFade(0f, ReplaySeconds * 0.15f));
        }

        private Color BaseColor => _isSilhouette ? LobbyStyle.Silhouette : Color.white;

        private void StopReplay()
        {
            if (_replay != null && _replay.IsActive())
                _replay.Kill();

            _replay = null;
            character.color = BaseColor;
            cardPreviewGroup.alpha = 0f;
        }

        private void HandleClicked() => OnClicked?.Invoke();
    }
}
