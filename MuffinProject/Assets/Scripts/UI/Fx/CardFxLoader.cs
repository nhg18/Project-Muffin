using System;
using System.Collections;
using Chapchu.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 카드 연출을 재생할 때 불러오고, 끝나면 메모리에서 내린다 (10-ui 5-1 — 카드 하나의 그림이 모바일에서 십수 MB).
    /// 프리팹은 Resources/CardFx/Card_{카드 ID}.prefab 에 둔다 (<see cref="NameOf"/>). 연출이 없는 카드는 프리팹도 없다.
    /// 씬 · 프리팹에서 직접 참조하면 그 씬을 열 때 그림까지 같이 올라오므로 참조하지 않고 이름으로 부른다.
    /// </summary>
    public static class CardFxLoader
    {
        /// <summary>카드의 연출 프리팹 이름 — Card_{카드 ID}.</summary>
        public static string NameOf(int cardId) => "Card_" + cardId;

        /// <summary>
        /// 연출 프리팹을 비동기로 불러와 parent 아래에 만들고 created 로 넘긴다 (그림을 읽는 동안 화면이 멈추지 않게).
        /// 프리팹이 없으면 null 을 넘긴다 — required 면 오류로 남기고, 아니면 조용히 넘긴다 (시험장 · 게임 화면: 연출이 없는 카드).
        /// </summary>
        public static IEnumerator CreateRoutine(string fxName, Transform parent, Action<CardFx> created, bool required)
        {
            ResourceRequest request = Resources.LoadAsync<GameObject>(ResourcePaths.CardFx + fxName);
            yield return request;

            var go = request.asset as GameObject;
            CardFx prefab = go != null ? go.GetComponent<CardFx>() : null;
            // 규약이 어긋나면 Resources 는 조용히 null 을 준다. 원인이 보이게 남긴다.
            if (go != null && prefab == null)
                Debug.LogError($"[{nameof(CardFxLoader)}] Resources/{ResourcePaths.CardFx}{fxName}.prefab 루트에 {nameof(CardFx)} 컴포넌트가 없다");
            else if (go == null && required)
                Debug.LogError($"[{nameof(CardFxLoader)}] Resources/{ResourcePaths.CardFx}{fxName}.prefab 이 없다");
            created(prefab == null ? null : Object.Instantiate(prefab, parent));
        }

        /// <summary>
        /// 연출을 지우고, 그 연출만 쓰던 그림 · 프리팹을 메모리에서 내린다. yield 로 기다리지 말고 따로 돌린 뒤 들고 있던 참조를 바로 비운다:
        /// <c>StartCoroutine(CardFxLoader.ReleaseRoutine(fx)); fx = null;</c>
        /// 지워진 연출이라도 스크립트 변수가 가리키고 있으면 그 필드의 그림까지 "쓰는 중" 으로 잡혀 내려가지 않는다.
        /// </summary>
        public static IEnumerator ReleaseRoutine(CardFx fx)
        {
            if (fx != null)
                Object.Destroy(fx.gameObject);
            fx = null;
            // Destroy 는 프레임 끝에 일어나고, 지워진 오브젝트에 걸린 트윈은 다음 프레임에 정리된다 — 그 뒤에 내린다
            yield return null;
            yield return Resources.UnloadUnusedAssets();
        }
    }
}
