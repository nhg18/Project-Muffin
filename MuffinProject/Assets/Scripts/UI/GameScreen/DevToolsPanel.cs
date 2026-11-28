using System.Collections.Generic;
using Chapchu.Cards;
using Chapchu.Core;
using Chapchu.Network;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 개발자 도구 판 (10-ui.md 12절) — 개발자 모드(DevMode)가 켜진 기기에서만, 설정 판의 「개발자 도구」 로 연다. 시험용이라 모바일 기준 크기(글자 30 · 줄 88), 긴 목록은 스크롤.
    /// 내 화면만 바꾸는 것: 연출 속도(1 · 2 · 4 배) · 이펙트 끄기(3 단계 카드 연출을 건너뜀) · 5 단계 띠 · 실측 표 · 규칙 요약 · 조작법.
    /// 판을 바꾸는 것은 방장에게 요청한다 — 개발 방(매치가 아닌 방)에서만 받는다: 턴 시간 무제한 · 내 자리도 봇 · 카드 써 보기 · 새 판.
    /// </summary>
    public class DevToolsPanel : MonoBehaviour
    {
        private static readonly float[] Speeds = { 1f, 2f, 4f };

        [SerializeField] private PunGameServer server;
        [Tooltip("카드 써 보기 목록 — PunGameServer 와 같은 덱.")]
        [SerializeField] private DeckData deck;
        [Tooltip("설정 판 — 「개발자 도구」 버튼을 누르면 판을 닫고 이 판을 연다.")]
        [SerializeField] private GameMenuView menu;

        [Header("판")]
        [SerializeField] private GameObject sheet;
        [SerializeField] private Button closeButton;
        [Tooltip("속도 1 · 2 · 4 배 순서.")]
        [SerializeField] private Button[] speedButtons;
        [SerializeField] private Button effectsButton;
        [SerializeField] private Button unlimitedButton;
        [SerializeField] private Button seatBotButton;
        [SerializeField] private Button bandButton;
        [SerializeField] private Button newGameButton;
        [Tooltip("카드 써 보기 — 카드마다 이 버튼을 복제해 목록에 넣는다 (꺼 둔 채로 저장).")]
        [SerializeField] private Button cardButtonTemplate;
        [SerializeField] private RectTransform cardList;
        [SerializeField] private TMP_Text meterText;

        [Header("5 단계 띠 (판 밖, 화면 위)")]
        [SerializeField] private GameObject stageBand;
        [SerializeField] private TMP_Text stageText;

        [Header("색")]
        [SerializeField] private Color onColor = new Color32(0xff, 0xc1, 0x2e, 0xFF);
        [SerializeField] private Color offColor = Color.white;

        // 내 화면 설정 — 「새 판」 으로 씬을 다시 열어도 이어진다
        private static float _speed = 1f;
        private static bool _showBand;

        /// <summary>3 단계 카드별 연출을 건너뛴다 (GameScreenPresenter 가 읽는다).</summary>
        public static bool EffectsOff { get; private set; }

        // 판을 바꾸는 것 — 이 판의 방장이 받은 기준이 아니라 내가 보낸 기준이다 (새 판이면 처음으로)
        private bool _unlimited;
        private bool _seatBot;
        private readonly List<Button> _cardButtons = new List<Button>();

        private void Awake()
        {
            if (server == null || deck == null || menu == null || sheet == null || closeButton == null || speedButtons == null || speedButtons.Length != Speeds.Length
                || effectsButton == null || unlimitedButton == null || seatBotButton == null || bandButton == null || newGameButton == null
                || cardButtonTemplate == null || cardList == null || meterText == null || stageBand == null || stageText == null)
                Debug.LogError($"[{nameof(DevToolsPanel)}] 참조 중 비어 있는 것이 있습니다.", this);

            sheet.SetActive(false);
            cardButtonTemplate.gameObject.SetActive(false);
            if (!DevMode.IsOn)
            {
                // 개발자 모드가 아니면 아무것도 바꾸지 않는다 — 이전에 켠 내 화면 설정도 풀린다
                EffectsOff = false;
                _speed = 1f;
                _showBand = false;
                stageBand.SetActive(false);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            menu.DevToolsClicked += Open;
            closeButton.onClick.AddListener(Close);
            for (int i = 0; i < speedButtons.Length; i++)
            {
                float speed = Speeds[i];
                speedButtons[i].onClick.AddListener(() => SetSpeed(speed));
            }
            effectsButton.onClick.AddListener(ToggleEffects);
            unlimitedButton.onClick.AddListener(ToggleUnlimited);
            seatBotButton.onClick.AddListener(ToggleSeatBot);
            bandButton.onClick.AddListener(ToggleBand);
            newGameButton.onClick.AddListener(RequestNewGame);
            DevStageMeter.OnStageChanged += ShowStage;
        }

        private void OnDisable()
        {
            menu.DevToolsClicked -= Open;
            closeButton.onClick.RemoveListener(Close);
            foreach (Button button in speedButtons)
                button.onClick.RemoveAllListeners();
            effectsButton.onClick.RemoveListener(ToggleEffects);
            unlimitedButton.onClick.RemoveListener(ToggleUnlimited);
            seatBotButton.onClick.RemoveListener(ToggleSeatBot);
            bandButton.onClick.RemoveListener(ToggleBand);
            newGameButton.onClick.RemoveListener(RequestNewGame);
            DevStageMeter.OnStageChanged -= ShowStage;
        }

        private void Start()
        {
            BuildCardList();
            stageBand.SetActive(false);
            Refresh();
        }

        // 연출 속도 — 멈춘 동안(GameClock, 봇전에서 설정 판)은 건드리지 않는다. 판 규칙의 시간(턴 · 반응 시간)은 그대로다
        private void Update()
        {
            if (!GameClock.IsPaused && !Mathf.Approximately(Time.timeScale, _speed))
                Time.timeScale = _speed;

            if (sheet.activeSelf)
                meterText.text = DevStageMeter.Table();
            if (_showBand && DevStageMeter.Current != null)
                stageText.text = $"{DevStageMeter.Current}  {DevStageMeter.Elapsed:0.0}초";
        }

        private void OnDestroy()
        {
            if (!GameClock.IsPaused)
                Time.timeScale = 1f; // 다음 씬(결과 · 로비)은 보통 속도
        }

        private void Open()
        {
            sheet.SetActive(true);
            Refresh();
        }

        private void Close() => sheet.SetActive(false);

        private void SetSpeed(float speed)
        {
            _speed = speed;
            Refresh();
        }

        private void ToggleEffects()
        {
            EffectsOff = !EffectsOff;
            Refresh();
        }

        private void ToggleBand()
        {
            _showBand = !_showBand;
            ShowStage(DevStageMeter.Current);
            Refresh();
        }

        private void ToggleUnlimited()
        {
            _unlimited = !_unlimited;
            server.RequestDev(PunGameServer.DevCommand.UnlimitedTurnTime, _unlimited ? 1 : 0);
            Refresh();
        }

        private void ToggleSeatBot()
        {
            _seatBot = !_seatBot;
            server.RequestDev(PunGameServer.DevCommand.SeatBot, _seatBot ? 1 : 0);
            Refresh();
        }

        private void RequestNewGame()
        {
            DevStageMeter.Reset();
            server.RequestDev(PunGameServer.DevCommand.NewGame, 0);
            Close();
        }

        private void GiveCard(int cardId) => server.RequestDev(PunGameServer.DevCommand.GiveCard, cardId);

        private void ShowStage(string stage)
        {
            stageBand.SetActive(_showBand && stage != null);
            if (stage != null)
                stageText.text = stage;
        }

        // 덱에 든 카드 종류마다 한 줄 — 누르면 그 카드 한 장이 내 손패로 (덱 · 버림 더미에 남아 있을 때)
        private void BuildCardList()
        {
            foreach (DeckData.Entry entry in deck.Entries)
            {
                CardData card = entry.card;
                if (card == null || entry.count <= 0) continue;

                Button button = Instantiate(cardButtonTemplate, cardList);
                button.gameObject.SetActive(true);
                button.name = card.cardName;
                button.GetComponentInChildren<TMP_Text>().text = card.cardName;
                int cardId = card.id;
                button.onClick.AddListener(() => GiveCard(cardId));
                _cardButtons.Add(button);
            }
        }

        private void Refresh()
        {
            for (int i = 0; i < speedButtons.Length; i++)
                Paint(speedButtons[i], Mathf.Approximately(_speed, Speeds[i]));
            Paint(effectsButton, EffectsOff);
            Paint(unlimitedButton, _unlimited);
            Paint(seatBotButton, _seatBot);
            Paint(bandButton, _showBand);

            // 판을 바꾸는 것은 방 안 · 매치가 아닌 방에서만 (방장이 다시 거른다)
            bool devRoom = PhotonNetwork.InRoom && !Chapchu.Network.BotRoster.IsMatchRoom;
            unlimitedButton.interactable = devRoom;
            seatBotButton.interactable = devRoom;
            newGameButton.interactable = devRoom;
            foreach (Button button in _cardButtons)
                button.interactable = devRoom;
        }

        private void Paint(Button button, bool on)
        {
            if (button.targetGraphic != null)
                button.targetGraphic.color = on ? onColor : offColor;
        }
    }
}
