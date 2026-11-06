using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 카드 연출의 한 조각 — 장면 하나(카드별 그림 애니메이션) 또는 이펙트 하나(공통 이펙트 프리팹).
    /// 카드 연출 프리팹 안에 두면 <see cref="StartMs"/> 에 재생된다. 같은 이펙트 프리팹을 여러 개 두어 시각 · 값만 다르게 쓸 수 있다.
    /// 코루틴은 연출 루트(<see cref="CardFx"/>)에서 돌린다 — 이 오브젝트가 들어 있는 판이 투명해져 꺼져도 멈추지 않게.
    /// </summary>
    public abstract class CardFxStep : MonoBehaviour
    {
        [Tooltip("연출 시작부터 이 조각이 시작할 때까지 (ms). 마무리 조각은 마무리 시작 기준.")]
        [SerializeField] private float startMs;

        private readonly List<Coroutine> _routines = new List<Coroutine>();
        private MonoBehaviour _host;

        /// <summary>연출 시작부터 이 조각이 시작할 때까지 (ms).</summary>
        public float StartMs => startMs;

        /// <summary>시작부터 끝날 때까지 (ms). 시간표 · 시험장이 이 값으로 길이를 잡는다.</summary>
        public abstract float DurationMs { get; }

        /// <summary>카드별 장면(그림 애니메이션)이면 true, 이펙트면 false. 시험장은 이펙트만 목록에 올린다.</summary>
        public virtual bool IsScene => false;

        /// <summary>이 조각의 그림이 붙은 오브젝트. 이펙트 하나만 볼 때 이것을 화면 가운데로 옮긴다.</summary>
        public virtual RectTransform Visual => (RectTransform)transform;

        /// <summary>이 조각이 그려지는 화면(가장 가까운 Canvas)의 크기. 판이 투명해져 꺼져 있어도 찾는다.</summary>
        protected Vector2 ScreenSize => ((RectTransform)GetComponentInParent<Canvas>(true).transform).rect.size;

        private MonoBehaviour Host
        {
            get
            {
                if (_host == null)
                    _host = GetComponentInParent<CardFx>(true);
                return _host != null ? (MonoBehaviour)_host : this;
            }
        }

        /// <summary>재생 전에 자기 그림을 처음 상태로 돌린다 (대개 투명).</summary>
        public virtual void ResetState()
        {
            StopRoutines();
        }

        /// <summary>지금 재생한다. target 은 마무리 조각에서만 대상 자리, 컷인이면 null.</summary>
        public abstract void Play(CardFxContext context, RectTransform target);

        protected void StartRoutine(IEnumerator routine)
        {
            _routines.Add(Host.StartCoroutine(routine));
        }

        protected void StopRoutines()
        {
            foreach (Coroutine routine in _routines)
            {
                if (routine != null && Host != null)
                    Host.StopCoroutine(routine);
            }
            _routines.Clear();
        }

        protected static float Seconds(float ms) => ms / 1000f;

        protected static void SetAlpha(Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        // 그림 색 그대로 alpha 로 번쩍였다가 사라진다
        protected void Flash(Graphic graphic, float alpha, float durationMs)
        {
            graphic.DOKill();
            SetAlpha(graphic, alpha);
            graphic.DOFade(0f, Seconds(durationMs)).SetEase(Ease.OutQuad).SetLink(gameObject);
        }

        // 시작부터 ms 가 지날 때까지 기다린다 (매 프레임 확인 — 시각이 밀리지 않게)
        protected static IEnumerator WaitUntilMs(float start, float ms)
        {
            while ((Time.time - start) * 1000f < ms)
                yield return null;
        }
    }
}
