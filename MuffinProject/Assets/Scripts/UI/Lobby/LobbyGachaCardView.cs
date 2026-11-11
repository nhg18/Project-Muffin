using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 뽑기 결과 카드 한 장 — 뒷면(등급 색 안쪽 테)을 누르면 0.45초에 뒤집혀 앞면(그림 · 등급 · 「새 아이템」 / 「중복 +N」)이 보인다.
    /// 기준: 「통합 화면 UI v2」 로비 .gcard
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class LobbyGachaCardView : MonoBehaviour
    {
        private const float FlipSeconds = 0.45f;
        private const string DuplicateFormat = "중복 +{0}";

        [SerializeField] private RectTransform inner;
        [SerializeField] private GameObject back;
        [SerializeField] private Image backGradeRing;
        [SerializeField] private GameObject front;
        [SerializeField] private RoundBox frontBox;
        [SerializeField] private Image characterImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text captionText;
        [SerializeField] private RoundBox tagBox;
        [SerializeField] private TMP_Text tagText;
        [SerializeField] private GameObject duplicateRow;
        [SerializeField] private TMP_Text duplicateText;
        [SerializeField] private GameObject newText;

        private Button _button;

        public bool IsOpen { get; private set; }

        public HubGachaResult Result { get; private set; }

        public event Action<LobbyGachaCardView> OnClicked;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (inner == null || back == null || backGradeRing == null || front == null || frontBox == null || characterImage == null
                || iconImage == null || captionText == null || tagBox == null || tagText == null || duplicateRow == null || duplicateText == null || newText == null)
                Debug.LogError($"[{nameof(LobbyGachaCardView)}] 참조가 비어 있습니다.", this);
        }

        private void OnEnable() => _button.onClick.AddListener(HandleClicked);

        private void OnDisable()
        {
            _button.onClick.RemoveListener(HandleClicked);
            inner.DOKill();
            inner.localEulerAngles = Vector3.zero;
        }

        /// <summary>뒷면으로 놓는다. slotIcon = 캐릭터가 아닌 아이템의 슬롯 아이콘.</summary>
        public void Bind(HubGachaResult result, Sprite slotIcon)
        {
            Result = result;
            IsOpen = false;
            HubItem item = result.Item;
            Color grade = LobbyStyle.GradeColor(item.Grade);
            bool isCharacter = item.Slot == HubSlot.Character;

            inner.DOKill();
            inner.localEulerAngles = Vector3.zero;
            back.SetActive(true);
            front.SetActive(false);
            backGradeRing.color = grade;
            frontBox.BorderColor = grade;
            frontBox.FillColor = item.Grade == HubGrade.Legend ? LobbyStyle.LegendCardFill : Color.white;
            characterImage.gameObject.SetActive(isCharacter);
            characterImage.color = item.IsDefault ? Color.white : LobbyStyle.Silhouette;
            iconImage.gameObject.SetActive(!isCharacter);
            iconImage.sprite = slotIcon;
            captionText.gameObject.SetActive(!isCharacter);
            captionText.text = LobbyHubData.SlotName(item.Slot);
            tagBox.BorderColor = grade;
            tagText.color = grade;
            tagText.text = LobbyHubData.GradeName(item.Grade);
            duplicateRow.SetActive(result.IsDuplicate);
            duplicateText.text = string.Format(DuplicateFormat, result.Refund);
            newText.SetActive(!result.IsDuplicate);
        }

        /// <summary>앞면으로 뒤집는다 — 반 바퀴 돈 순간 면을 바꾼다.</summary>
        public void Open()
        {
            if (IsOpen)
                return;

            IsOpen = true;
            inner.DOKill();
            DOTween.Sequence().SetTarget(inner).SetUpdate(true)
                .Append(inner.DOLocalRotate(new Vector3(0f, 90f, 0f), FlipSeconds * 0.45f).SetEase(Ease.InQuad))
                .AppendCallback(ShowFront)
                .Append(inner.DOLocalRotate(Vector3.zero, FlipSeconds * 0.55f).SetEase(Ease.OutBack));
        }

        private void ShowFront()
        {
            back.SetActive(false);
            front.SetActive(true);
            inner.localEulerAngles = new Vector3(0f, -90f, 0f);
        }

        private void HandleClicked() => OnClicked?.Invoke(this);
    }
}
