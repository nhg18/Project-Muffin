﻿using ParrelSync;
using UnityEditor;
using UnityEngine;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// 카드 패치 알림 창 — cards.json 이 바뀌었는데 카드 에셋에 아직 반영되지 않았으면 뜬다 (26-card-data.md 7-3).
    /// 에디터를 열었을 때, 그리고 cards.json 이 새로 들어왔을 때(pull · 브랜치 전환) 확인한다. 「카드 패치하기」 를 누르면 CardImporter 가 에셋을 고친다.
    /// 막는 창이 아니다 — 닫아 두고 메뉴(Chapchu/카드 패치하기)로 나중에 해도 된다. 패치하지 않은 채로는 빌드가 되지 않는다 (CardPatchBuildCheck).
    /// </summary>
    public class CardPatchWindow : EditorWindow
    {
        private const string WindowTitle = "카드 패치";
        private const string PendingText = "cards.json 이 바뀌었습니다.\n카드 에셋 · Deck_Main 에 아직 반영되지 않았습니다.";
        private const string AppliedText = "카드 에셋이 cards.json 과 같습니다.\n방금 패치했다면 바뀐 Card_*.asset · Deck_Main.asset 을 커밋하세요.";
        private const string FailedText = "반영하지 못한 카드가 있습니다. 콘솔의 오류를 확인하세요.";
        private const string PlayingText = "플레이 중에는 패치하지 않습니다. 플레이를 끝낸 뒤 누르세요.";
        private const string ButtonText = "카드 패치하기";
        private const float ButtonHeight = 56f;
        private const int ButtonFontSize = 18;

        // 에디터를 켠 뒤 한 번만 스스로 뜬다 — 스크립트를 고칠 때마다(도메인 리로드) 다시 뜨지 않게
        private const string CheckedOnLoadKey = "Chapchu.CardPatchWindow.CheckedOnLoad";

        private static readonly Vector2 WindowSize = new Vector2(420f, 170f);

        private bool _applied;
        private bool _failed; // 눌렀는데도 반영되지 않았다

        [InitializeOnLoadMethod]
        private static void CheckOnLoad()
        {
            if (SessionState.GetBool(CheckedOnLoadKey, false))
                return;

            SessionState.SetBool(CheckedOnLoadKey, true);
            CheckOnNextUpdate();
        }

        // 다음 에디터 틱에 본다 — 에디터 시작 · 에셋 가져오기가 끝난 뒤. delayCall 은 에디터가 뒤에 있으면 앞으로 올 때까지 불리지 않아 쓰지 않는다
        private static void CheckOnNextUpdate()
        {
            EditorApplication.update -= CheckOnce;
            EditorApplication.update += CheckOnce;
        }

        private static void CheckOnce()
        {
            EditorApplication.update -= CheckOnce;
            OpenIfPending();
        }

        // 자동 실행(배치 모드)에는 누를 사람이 없고, 플레이 중에는 에셋을 고치지 않는다. 클론(ParrelSync)은 원본 프로젝트의 에셋을 같이 쓴다 — 원본에서 한다
        private static void OpenIfPending()
        {
            if (Application.isBatchMode || ClonesManager.IsClone() || EditorApplication.isPlayingOrWillChangePlaymode || CardImporter.IsApplied())
                return;

            CardPatchWindow window = GetWindow<CardPatchWindow>(true, WindowTitle);
            window.minSize = WindowSize;
            window.Refresh();
        }

        private void OnEnable() => Refresh();

        private void OnFocus() => Refresh();

        private void OnGUI()
        {
            if (_failed)
                EditorGUILayout.HelpBox(FailedText, MessageType.Error);
            else
                EditorGUILayout.HelpBox(_applied ? AppliedText : PendingText, _applied ? MessageType.Info : MessageType.Warning);

            bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
            if (playing)
                EditorGUILayout.HelpBox(PlayingText, MessageType.None);

            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(playing || _applied))
            {
                var style = new GUIStyle(GUI.skin.button) { fontSize = ButtonFontSize };
                if (GUILayout.Button(ButtonText, style, GUILayout.Height(ButtonHeight)))
                {
                    CardImporter.Import();
                    Refresh();
                    _failed = !_applied;
                }
            }
        }

        private void Refresh()
        {
            _applied = CardImporter.IsApplied();
            if (_applied)
                _failed = false;

            Repaint();
        }

        /// <summary>cards.json 이 새로 들어오면(pull · 브랜치 전환 · 손으로 고침) 다시 확인한다.</summary>
        private class CardSheetWatcher : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                foreach (string path in importedAssets)
                {
                    if (!CardImporter.IsCardSheet(path))
                        continue;

                    CheckOnNextUpdate();
                    return;
                }
            }
        }
    }
}
