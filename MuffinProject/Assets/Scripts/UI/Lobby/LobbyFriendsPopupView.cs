using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    public enum LobbyFriendsTab { Friends, Requests, Add }

    /// <summary>
    /// 친구 창 — 탭 셋(친구 · 요청 · 추가). 친구 줄의 「⋯」 은 떠 있는 작은 메뉴(친구 삭제 · 차단)를 연다.
    /// 목록을 바꾸는 일은 프레젠터가 하고, 여기는 LobbyHubData 를 읽어 그린다.
    /// 기준: 「통합 화면 UI v2」 로비 friendsBody · placeFriendMenu, docs/systems/13-friend.md
    /// </summary>
    public class LobbyFriendsPopupView : MonoBehaviour
    {
        private const string FriendsTabFormat = "친구 {0}";
        private const string RequestsTabText = "요청";
        private const string RequestsTabFormat = "요청 {0}";
        private const string NoFriendsText = "아직 친구가 없어요.";
        private const string NoRequestsText = "받거나 보낸 요청이 없어요.";
        private const string NoResultText = "찾는 플레이어가 없어요.";

        [SerializeField] private GameObject content;
        [SerializeField] private RectTransform frame;
        [SerializeField] private Button closeButton;
        [SerializeField] private LobbyTabView[] tabs;

        [Header("추가 탭")]
        [SerializeField] private GameObject addHead;
        [SerializeField] private TMP_Text myCodeText;
        [SerializeField] private Button copyButton;
        [SerializeField] private TMP_InputField searchField;
        [SerializeField] private Button searchButton;

        [Header("목록")]
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private LobbyPersonRowView rowTemplate;
        [SerializeField] private TMP_Text incomingHeader;
        [SerializeField] private TMP_Text outgoingHeader;
        [SerializeField] private TMP_Text emptyText;

        [Header("줄 메뉴")]
        [SerializeField] private RectTransform menu;
        [SerializeField] private TMP_Text menuName;
        [SerializeField] private Button menuDeleteButton;
        [SerializeField] private Button menuBlockButton;
        [SerializeField] private Button menuCatcher;

        [Tooltip("메뉴와 「⋯」 버튼 사이 틈 (캔버스 px)")]
        [SerializeField] private float menuGap = 12f;

        private readonly List<LobbyPersonRowView> _rows = new List<LobbyPersonRowView>();
        private LobbyHubData _data;
        private List<HubSearchResult> _results;
        private HubPerson _menuTarget;
        private int _used;

        public bool IsOpen => content.activeSelf;

        public LobbyFriendsTab Tab { get; private set; }

        public event Action OnCloseRequested;
        public event Action OnCopyRequested;
        public event Action<HubPerson> OnAcceptRequested;
        public event Action<HubPerson> OnDeclineRequested;
        public event Action<HubPerson> OnCancelRequested;
        public event Action<HubSearchResult> OnFriendRequestRequested;
        public event Action<HubPerson> OnDeleteRequested;
        public event Action<HubPerson> OnBlockRequested;

        private void Awake()
        {
            if (content == null || frame == null || closeButton == null || tabs == null || tabs.Length != 3
                || addHead == null || myCodeText == null || copyButton == null || searchField == null || searchButton == null)
                Debug.LogError($"[{nameof(LobbyFriendsPopupView)}] 창 · 추가 탭 참조가 비어 있습니다.", this);

            if (scroll == null || listContent == null || rowTemplate == null || incomingHeader == null || outgoingHeader == null || emptyText == null
                || menu == null || menuName == null || menuDeleteButton == null || menuBlockButton == null || menuCatcher == null)
                Debug.LogError($"[{nameof(LobbyFriendsPopupView)}] 목록 · 메뉴 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            closeButton.onClick.AddListener(HandleCloseClicked);
            copyButton.onClick.AddListener(HandleCopyClicked);
            searchButton.onClick.AddListener(HandleSearchClicked);
            searchField.onSubmit.AddListener(HandleSearchSubmitted);
            menuDeleteButton.onClick.AddListener(HandleMenuDeleteClicked);
            menuBlockButton.onClick.AddListener(HandleMenuBlockClicked);
            menuCatcher.onClick.AddListener(HandleMenuCatcherClicked);
            foreach (LobbyTabView tab in tabs)
                tab.OnClicked += HandleTabClicked;
            foreach (LobbyPersonRowView row in _rows)
                Subscribe(row);
        }

        private void OnDisable()
        {
            closeButton.onClick.RemoveListener(HandleCloseClicked);
            copyButton.onClick.RemoveListener(HandleCopyClicked);
            searchButton.onClick.RemoveListener(HandleSearchClicked);
            searchField.onSubmit.RemoveListener(HandleSearchSubmitted);
            menuDeleteButton.onClick.RemoveListener(HandleMenuDeleteClicked);
            menuBlockButton.onClick.RemoveListener(HandleMenuBlockClicked);
            menuCatcher.onClick.RemoveListener(HandleMenuCatcherClicked);
            foreach (LobbyTabView tab in tabs)
                tab.OnClicked -= HandleTabClicked;
            foreach (LobbyPersonRowView row in _rows)
                Unsubscribe(row);
        }

        /// <summary>창을 연다. 탭을 정하지 않으면 받은 요청이 있을 때 요청 탭, 없으면 친구 탭.</summary>
        public void Show(LobbyHubData data, LobbyFriendsTab? tab = null)
        {
            _data = data;
            _results = null;
            searchField.text = string.Empty;
            content.SetActive(true);
            ShowTab(tab ?? (data.RequestsIn.Count > 0 ? LobbyFriendsTab.Requests : LobbyFriendsTab.Friends));
        }

        public void Hide()
        {
            CloseMenu();
            content.SetActive(false);
        }

        public void ShowTab(LobbyFriendsTab tab)
        {
            Tab = tab;
            scroll.verticalNormalizedPosition = 1f;
            Refresh();
        }

        /// <summary>지금 탭을 데이터대로 다시 그린다.</summary>
        public void Refresh()
        {
            if (!IsOpen)
                return;

            CloseMenu();
            int incoming = _data.RequestsIn.Count;
            tabs[0].SetLabel(string.Format(FriendsTabFormat, _data.Friends.Count));
            tabs[1].SetLabel(incoming > 0 ? string.Format(RequestsTabFormat, incoming) : RequestsTabText);
            for (int i = 0; i < tabs.Length; i++)
                tabs[i].SetPressed(i == (int)Tab);

            addHead.SetActive(Tab == LobbyFriendsTab.Add);
            myCodeText.text = _data.MyCode;
            incomingHeader.gameObject.SetActive(false);
            outgoingHeader.gameObject.SetActive(false);
            emptyText.gameObject.SetActive(false);
            _used = 0;

            switch (Tab)
            {
                case LobbyFriendsTab.Friends:
                    foreach (HubPerson friend in _data.SortedFriends())
                        NextRow().BindFriend(friend);
                    ShowEmpty(_data.Friends.Count == 0, NoFriendsText);
                    break;

                case LobbyFriendsTab.Requests:
                    DrawRequests();
                    break;

                default:
                    if (_results == null)
                        break;

                    foreach (HubSearchResult result in _results)
                        NextRow().BindSearch(result);
                    ShowEmpty(_results.Count == 0, NoResultText);
                    break;
            }

            for (int i = _used; i < _rows.Count; i++)
                _rows[i].gameObject.SetActive(false);
        }

        /// <summary>그 닉네임의 줄을 잠깐 노랗게 — 방금 친구가 된 사람.</summary>
        public void Flash(string nickname)
        {
            for (int i = 0; i < _used; i++)
            {
                if (_rows[i].Person != null && _rows[i].Person.Nickname == nickname)
                    _rows[i].Flash();
            }
        }

        /// <summary>줄 메뉴가 열려 있으면 닫고 true.</summary>
        public bool CloseMenu()
        {
            if (!menu.gameObject.activeSelf)
                return false;

            _menuTarget = null;
            menu.gameObject.SetActive(false);
            menuCatcher.gameObject.SetActive(false);
            return true;
        }

        private void DrawRequests()
        {
            ShowEmpty(_data.RequestsIn.Count == 0 && _data.RequestsOut.Count == 0, NoRequestsText);
            if (_data.RequestsIn.Count > 0)
            {
                incomingHeader.gameObject.SetActive(true);
                incomingHeader.transform.SetSiblingIndex(_used);
                foreach (HubPerson request in _data.RequestsIn)
                    NextRow(1).BindRequestIn(request);
            }

            if (_data.RequestsOut.Count > 0)
            {
                int headers = _data.RequestsIn.Count > 0 ? 2 : 1;
                outgoingHeader.gameObject.SetActive(true);
                outgoingHeader.transform.SetSiblingIndex(_used + headers - 1);
                foreach (HubPerson request in _data.RequestsOut)
                    NextRow(headers).BindRequestOut(request);
            }
        }

        // 줄을 하나 꺼내 목록의 다음 자리에 둔다 — 모자라면 본을 복제한다. headersBefore = 이 줄 앞에 있는 머리글 수
        private LobbyPersonRowView NextRow(int headersBefore = 0)
        {
            if (_used == _rows.Count)
            {
                LobbyPersonRowView created = Instantiate(rowTemplate, listContent);
                _rows.Add(created);
                Subscribe(created);
            }

            LobbyPersonRowView row = _rows[_used];
            row.gameObject.SetActive(true);
            row.transform.SetSiblingIndex(_used + headersBefore);
            _used++;
            return row;
        }

        private void ShowEmpty(bool isEmpty, string text)
        {
            emptyText.gameObject.SetActive(isEmpty);
            emptyText.text = text;
        }

        private void Subscribe(LobbyPersonRowView row)
        {
            row.OnPrimaryClicked += HandleRowPrimaryClicked;
            row.OnSecondaryClicked += HandleRowSecondaryClicked;
            row.OnMenuClicked += HandleRowMenuClicked;
        }

        private void Unsubscribe(LobbyPersonRowView row)
        {
            row.OnPrimaryClicked -= HandleRowPrimaryClicked;
            row.OnSecondaryClicked -= HandleRowSecondaryClicked;
            row.OnMenuClicked -= HandleRowMenuClicked;
        }

        private void Search()
        {
            List<HubSearchResult> results = _data.Search(searchField.text);
            if (results == null)
                return;

            _results = results;
            Refresh();
        }

        // 메뉴는 「⋯」 아래에, 오른쪽 끝을 맞춰 띄운다. 창 아래가 모자라면 버튼 위로
        private void OpenMenu(LobbyPersonRowView row)
        {
            _menuTarget = row.Person;
            menuName.text = row.Person.Nickname;
            menu.gameObject.SetActive(true);
            menuCatcher.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(menu);

            var corners = new Vector3[4];
            row.MenuButtonRect.GetWorldCorners(corners);
            Vector3 bottomRight = frame.InverseTransformPoint(corners[3]);
            Vector3 topRight = frame.InverseTransformPoint(corners[2]);
            float y = bottomRight.y - menuGap;
            if (y - menu.rect.height < frame.rect.yMin)
                y = topRight.y + menuGap + menu.rect.height;

            menu.pivot = Vector2.one;
            menu.anchorMin = menu.anchorMax = new Vector2(0.5f, 0.5f);
            menu.anchoredPosition = new Vector2(bottomRight.x - frame.rect.center.x, y - frame.rect.center.y);
        }

        private void HandleCloseClicked() => OnCloseRequested?.Invoke();

        private void HandleCopyClicked() => OnCopyRequested?.Invoke();

        private void HandleSearchClicked() => Search();

        private void HandleSearchSubmitted(string text) => Search();

        private void HandleTabClicked(LobbyTabView tab) => ShowTab((LobbyFriendsTab)Array.IndexOf(tabs, tab));

        private void HandleMenuCatcherClicked() => CloseMenu();

        private void HandleMenuDeleteClicked()
        {
            HubPerson target = _menuTarget;
            CloseMenu();
            if (target != null)
                OnDeleteRequested?.Invoke(target);
        }

        private void HandleMenuBlockClicked()
        {
            HubPerson target = _menuTarget;
            CloseMenu();
            if (target != null)
                OnBlockRequested?.Invoke(target);
        }

        private void HandleRowPrimaryClicked(LobbyPersonRowView row)
        {
            if (row.Result != null)
                OnFriendRequestRequested?.Invoke(row.Result);
            else
                OnAcceptRequested?.Invoke(row.Person);
        }

        private void HandleRowSecondaryClicked(LobbyPersonRowView row)
        {
            if (Tab != LobbyFriendsTab.Requests || row.Person == null)
                return;

            if (_data.RequestsIn.Contains(row.Person))
                OnDeclineRequested?.Invoke(row.Person);
            else
                OnCancelRequested?.Invoke(row.Person);
        }

        private void HandleRowMenuClicked(LobbyPersonRowView row)
        {
            bool wasOpenForRow = _menuTarget == row.Person;
            CloseMenu();
            if (!wasOpenForRow)
                OpenMenu(row);
        }
    }
}
