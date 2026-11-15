using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 만화 컷인 장면이 아는 사람들 — 시안 play 의 P (from · tg · me · tk · others · okeys · avs · hit). 컷인 하나(3 · 4 단계)가 끝날 때까지 같은 것을 쓴다.
    /// </summary>
    public sealed class ComicContext
    {
        private readonly List<CardFxSeat> _others = new List<CardFxSeat>();
        private readonly List<Vector2> _hits = new List<Vector2>();

        /// <summary>뼈대가 넘긴 카드 연출 정보 (FxLayer · 자리 · 카드 그림 · 4 단계 알림).</summary>
        public CardFxContext Fx { get; }

        /// <summary>카드를 낸 사람 (시안 P.from).</summary>
        public CardFxSeat From { get; }

        /// <summary>맞는 사람 (시안 P.tg) — <see cref="CardFxContext.MainTarget"/>.</summary>
        public CardFxSeat Target { get; }

        /// <summary>낸 사람이 아닌 사람들, 턴 순서 (시안 P.others) — <see cref="CardFxContext.Others"/>.</summary>
        public IReadOnlyList<CardFxSeat> Others => _others;

        /// <summary>낸 사람의 강아지 키 (시안 P.me).</summary>
        public string FromKey => DogKeyOf(From);

        /// <summary>맞는 사람의 강아지 키 (시안 P.tk).</summary>
        public string TargetKey => DogKeyOf(Target);

        /// <summary>장면 값의 흔들림 + 장면이 더한 흔들림 (시각, 세기).</summary>
        public IReadOnlyList<Vector2> Hits => _hits;

        /// <summary>컷인이 만든다 — 장면이 직접 만들 일은 없다.</summary>
        public ComicContext(CardFxContext fx, IEnumerable<Vector2> hits)
        {
            Fx = fx;
            From = fx.CasterSeat;
            Target = fx.MainTarget;
            _others.AddRange(fx.Others);
            if (hits != null)
                _hits.AddRange(hits);
        }

        /// <summary>
        /// 자리의 강아지 키 (<see cref="FxDog.Keys"/>, 시안 DOGPILE.keyOf) — 이 화면의 주인 = 나(na), 나머지는 턴 순서대로 초코 · 콩이 · 보리.
        /// 자리마다 정해진 캐릭터 값이 생기면 그것으로 바꾼다.
        /// </summary>
        public string DogKeyOf(CardFxSeat seat)
        {
            if (seat == null || seat.IsMe)
                return FxDog.Keys[0];
            int order = 0;
            foreach (CardFxSeat other in Fx.Seats)
            {
                if (other == null || other.IsMe)
                    continue;
                order++;
                if (other == seat)
                    break;
            }
            return FxDog.Keys[Mathf.Clamp(order, 1, FxDog.Keys.Count - 1)];
        }

        /// <summary>자리의 얼굴 그림 (시안 P.avs · avOf) — 지금은 모두 <see cref="CardFxContext.Character"/>.</summary>
        public Sprite AvatarOf(CardFxSeat seat) => Fx.Character;

        /// <summary>장면 흔들림을 더한다 (시안 P.hit(at, m)) — at 초에 세기 m 으로 흔들리고 0.45 초 동안 잦아든다.</summary>
        public void Hit(float at, float magnitude)
        {
            _hits.Add(new Vector2(at, magnitude));
        }
    }
}
