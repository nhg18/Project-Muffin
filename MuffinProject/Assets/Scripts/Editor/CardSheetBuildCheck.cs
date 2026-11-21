using System.Collections.Generic;
using Chapchu.Cards;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// 값 파일(cards.json)이 카드 에셋에 얹히지 않으면 빌드를 멈춘다 (26-card-data.md 8절).
    /// 게임은 틀린 카드를 에셋의 옛 값으로 돌리고 에러만 남긴다 — 기기에서는 그 에러가 보이지 않아, 틀린 값이 그대로 나갈 수 있다.
    /// </summary>
    public class CardSheetBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            List<string> problems = FindProblems();
            if (problems.Count > 0)
                throw new BuildFailedException("카드 값 파일이 틀려 빌드를 멈춘다 (26-card-data.md 8절)\n" + string.Join("\n", problems));
        }

        /// <summary>값 파일을 쥔 덱을 모두 보고, 틀린 곳을 한 줄씩 돌려준다. 다 맞으면 빈 목록.</summary>
        public static List<string> FindProblems()
        {
            var problems = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(DeckData)}"))
            {
                var deck = AssetDatabase.LoadAssetAtPath<DeckData>(AssetDatabase.GUIDToAssetPath(guid));
                if (deck != null)
                    problems.AddRange(deck.FindSheetProblems());
            }

            return problems;
        }
    }
}
