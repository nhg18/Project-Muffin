using Chapchu.UI.Title;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>로비 뷰들이 실행 중에 바꾸는 색 · 모양 — 「통합 화면 UI v2」 키트 값.</summary>
    public static class LobbyStyle
    {
        public static readonly Color Ink = new Color32(0x2A, 0x24, 0x20, 0xFF);
        public static readonly Color Ink2 = new Color32(0x6E, 0x64, 0x5C, 0xFF);
        public static readonly Color Turn = new Color32(0xFF, 0xC1, 0x2E, 0xFF);
        public static readonly Color Hp = new Color32(0xEC, 0x3B, 0x46, 0xFF);
        public static readonly Color Ok = new Color32(0x3C, 0xBF, 0x72, 0xFF);
        public static readonly Color Off = new Color32(0xB8, 0xAE, 0xA4, 0xFF);
        public static readonly Color Soft = new Color32(0xF6, 0xF0, 0xE7, 0xFF);
        public static readonly Color Band = new Color32(0xCF, 0xEE, 0xDD, 0xFF);
        public static readonly Color Warn = new Color32(0xFF, 0x9F, 0x1A, 0xFF);
        public static readonly Color WarnFill = new Color32(0xFF, 0xF1, 0xE0, 0xFF);
        public static readonly Color Track = new Color32(0xEF, 0xE8, 0xDE, 0xFF);
        public static readonly Color OfflineDot = new Color32(0xE4, 0xDC, 0xD2, 0xFF);
        public static readonly Color PackFrame = new Color32(0xFF, 0x9F, 0x1A, 0xFF);
        public static readonly Color LegendCardFill = new Color32(0xFF, 0xF7, 0xDC, 0xFF);
        public static readonly Color RareBurst = new Color32(0xE9, 0xDC, 0xFF, 0xFF);

        /// <summary>그림이 없는 아이템 자리 — 캐릭터 그림을 검은 실루엣으로 (시안 .sil)</summary>
        public static readonly Color Silhouette = new Color(0f, 0f, 0f, 0.28f);

        /// <summary>등급 색 — 일반 회색 · 고급 파랑 · 희귀 보라 · 개판 금색</summary>
        public static Color GradeColor(HubGrade grade) => grade switch
        {
            HubGrade.Common   => new Color32(0x9A, 0x90, 0x88, 0xFF),
            HubGrade.Uncommon => new Color32(0x2F, 0x9B, 0xE0, 0xFF),
            HubGrade.Rare     => new Color32(0xB1, 0x8A, 0xE0, 0xFF),
            _                 => new Color32(0xE3, 0xA0, 0x08, 0xFF),
        };

        public static Color StatusColor(HubFriendStatus status) => status switch
        {
            HubFriendStatus.Online => Ok,
            HubFriendStatus.InGame => Turn,
            _                      => OfflineDot,
        };

        /// <summary>버튼을 켜고 끈다 — 꺼지면 회색 모양이 되고 눌리지 않는다 (시안 .btn:disabled).</summary>
        public static void SetEnabled(Button button, bool enabled)
        {
            button.interactable = enabled;
            if (button.targetGraphic != null)
                button.targetGraphic.raycastTarget = enabled;

            if (button.TryGetComponent(out PressableButton press))
                press.SetDimmed(!enabled);
        }

        /// <summary>천 단위 쉼표 (시안 toLocaleString)</summary>
        public static string Number(int value) => value.ToString("N0");
    }
}
