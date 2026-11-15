using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 연출이 알아야 하는 카드 · 자리 정보와, 결과를 보여 줄 때 부를 함수 · 결과 알림. 부르는 쪽(게임 화면 · 시험장)이 채운다.
    /// 연출 하나(3 단계 + 4 단계 장식)가 끝날 때까지 같은 것을 쓴다.
    /// </summary>
    public class CardFxContext
    {
        /// <summary>같이 쓰는 파티클 · 터짐 글자 · 말풍선 · 판 흔들기.</summary>
        public FxLayer Layer;

        /// <summary>카드를 낸 사람의 자리.</summary>
        public RectTransform Caster;

        /// <summary>대상 자리들. 대상이 없는 카드는 비어 있다.</summary>
        public IReadOnlyList<RectTransform> Targets = Array.Empty<RectTransform>();

        /// <summary>대상 하나의 결과(피해 숫자 · 체력 바)를 지금 보여 달라는 요청. 게임 화면에서는 결과가 연출 뒤에 따로 오므로 아무것도 하지 않는다.</summary>
        public Action<RectTransform> Impact = _ => { };

        /// <summary>카드 ID (11-card-list).</summary>
        public int CardId;

        /// <summary>카드 이름 — 컷인 제목.</summary>
        public string CardName;

        /// <summary>카드 그림.</summary>
        public Sprite CardArt;

        /// <summary>카드 뒷면 — 날아가는 카드 · 컷인 속 카드.</summary>
        public Sprite CardBack;

        /// <summary>캐릭터 그림 — 컷인에 나오는 강아지. 사람마다 그림이 생기기 전까지 모두 같다.</summary>
        public Sprite Character;

        /// <summary>낸 사람.</summary>
        public CardFxSeat CasterSeat;

        /// <summary>대상들 (<see cref="Targets"/> 와 같은 순서). 대상이 없는 카드는 비어 있다.</summary>
        public IReadOnlyList<CardFxSeat> TargetSeats = Array.Empty<CardFxSeat>();

        /// <summary>판에 남아 있는 모든 사람 (낸 사람 포함), 턴 순서.</summary>
        public IReadOnlyList<CardFxSeat> Seats = Array.Empty<CardFxSeat>();

        /// <summary>
        /// 4 단계: 누군가의 HP 가 화면에 바뀌었다 (자리, 전, 후). 공통 피해 · 회복 연출과 같은 순간에 온다.
        /// 장식이 숫자를 써야 할 때 받는다 (예: 운명 뒤집기의 「HP n」).
        /// </summary>
        public event Action<CardFxSeat, int, int> OnHpChanged;

        /// <summary>4 단계: 누군가 카드를 뽑았다 (자리, 장수). 예: 상상도 못한 정체의 뽑은 수.</summary>
        public event Action<CardFxSeat, int> OnCardsDrawn;

        /// <summary>대상 중 첫 사람, 없으면 낸 사람이 아닌 첫 사람, 그것도 없으면 낸 사람 (시안 CI2 의 P.tg).</summary>
        public CardFxSeat MainTarget
        {
            get
            {
                if (TargetSeats.Count > 0)
                    return TargetSeats[0];
                foreach (CardFxSeat seat in Seats)
                {
                    if (seat != CasterSeat)
                        return seat;
                }
                return CasterSeat;
            }
        }

        /// <summary>낸 사람이 아닌 사람들 (시안 CI2 의 P.others).</summary>
        public IEnumerable<CardFxSeat> Others
        {
            get
            {
                foreach (CardFxSeat seat in Seats)
                {
                    if (seat != CasterSeat)
                        yield return seat;
                }
            }
        }

        /// <summary>자리 하나를 ActorNumber 로 찾는다. 없으면 null.</summary>
        public CardFxSeat SeatOf(int actor)
        {
            foreach (CardFxSeat seat in Seats)
            {
                if (seat.Actor == actor)
                    return seat;
            }
            return null;
        }

        /// <summary>부르는 쪽이 HP 결과를 그린 순간에 부른다.</summary>
        public void NotifyHpChanged(CardFxSeat seat, int before, int after) => OnHpChanged?.Invoke(seat, before, after);

        /// <summary>부르는 쪽이 뽑기 결과를 그린 순간에 부른다.</summary>
        public void NotifyCardsDrawn(CardFxSeat seat, int count) => OnCardsDrawn?.Invoke(seat, count);
    }
}
