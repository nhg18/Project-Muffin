using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 연출이 알아야 하는 화면 위치와, 결과를 보여 줄 때 부를 함수. 부르는 쪽(테스트 벤치 · 게임 화면)이 채운다.
    /// </summary>
    public class CardFxContext
    {
        /// <summary>흔들거나 확대할 판 전체. 연출 레이어는 이 밖에 있어야 같이 흔들리지 않는다.</summary>
        public RectTransform Stage;

        /// <summary>카드를 낸 사람의 자리.</summary>
        public RectTransform Caster;

        /// <summary>대상 자리들. 대상이 없는 카드는 비어 있다.</summary>
        public IReadOnlyList<RectTransform> Targets;

        /// <summary>대상 하나의 결과(피해 숫자 · 체력 바)를 지금 보여 달라는 요청.</summary>
        public Action<RectTransform> Impact;
    }
}
