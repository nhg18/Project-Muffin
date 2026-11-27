using System;
using System.Collections.Generic;
using Chapchu.UI.RoomScreen;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Chapchu.Core
{
    /// <summary>효과음 종류. 파일 · 음량은 SoundManager 프리팹의 표에서 고른다.</summary>
    public enum Sfx
    {
        Tap,        // 버튼 누름 (모든 화면, SoundManager 가 직접 듣는다)
        CardDraw,   // 카드 뽑기 — 덱에서 손으로 날아갈 때
        CardPlay,   // 카드가 판 가운데로 나옴 — 낸 카드 · 카운터 (연출 3 단계)
        TrapSet,    // 함정 설치
        TrapFire,   // 함정 발동 — 칸에서 튀어나옴
        Hit,        // 피해 (4 단계)
        Zap,        // 피폭 피해 (4 단계)
        Heal,       // 회복 (4 단계)
        CardGain,   // +n장 (4 단계)
        CardLose,   // −n장 (4 단계)
        MyTurn,     // 내 턴 시작
        TimerTick,  // 내 턴 마지막 초
        Reject,     // 방장이 내 요청을 거절
        PlayerOut,  // 누군가 탈락 (최종 사망)
        Chapchu,    // 찹츄 선언
        Win,        // 결과 화면 — 내가 이김 · 무승부
        Lose,       // 결과 화면 — 내가 짐
    }

    /// <summary>
    /// 타이틀 · 로비에서 배경음을 틀고, 효과음을 낸다. 설정의 효과음 · 배경음 스위치(17-lobby-ui-full 5-4)를 따른다.
    /// 소리 전체(기능 13)가 정해지기 전의 임시 (PM 2026-11-27 「어울리는 대로 만들어」).
    /// 배경음 = 처음 한 번 듣는 앞부분(intro) 뒤에 반복 부분(loop)을 샘플 단위로 이어 붙인다. 두 파일은 한 곡을 마디에 맞춰 자른 것이다.
    /// 효과음은 여러 개가 겹쳐도 되게 소스 몇 개를 돌려 쓰고, 자주 나는 소리는 음 높이를 조금씩 흔들어 똑같이 반복되지 않게 한다.
    /// </summary>
    public class SoundManager : SingletonPersistent<SoundManager>
    {
        // 앞부분을 예약해 틀 때까지의 여유. 예약해야 반복 부분이 빈틈 없이 이어진다
        private const double BgmScheduleLeadSeconds = 0.1;
        private const int SfxVoices = 8;
        // 같은 효과음이 이 안에 또 오면 새로 울리지 않는다 — 여러 좌석이 한꺼번에 맞을 때 같은 파일이 겹쳐 커지지 않게
        private const float SfxRepeatGapSeconds = 0.05f;

        // 배경음을 트는 화면 (PM 2026-11-27). 방 · 게임 · 결과에서는 멈춘다
        private static readonly string[] BgmScenes = { ScenePaths.Title, ScenePaths.Lobby };

        [Serializable]
        private struct SfxEntry
        {
            public Sfx id;
            public AudioClip clip;
            [Tooltip("파일 음량에 곱한다 — 파일끼리는 이미 맞춰 두었다")]
            [Range(0f, 1f)] public float volume;
            [Tooltip("음 높이를 이만큼(비율) 안에서 무작위로 — 0 이면 그대로")]
            [Range(0f, 0.2f)] public float pitchJitter;
        }

        [SerializeField] private AudioSource bgmIntroSource;
        [SerializeField] private AudioSource bgmLoopSource;
        [SerializeField] private AudioClip bgmIntro;
        [SerializeField] private AudioClip bgmLoop;
        [Tooltip("배경음 음량")]
        [SerializeField] [Range(0f, 1f)] private float bgmVolume = 0.5f;
        [Tooltip("배경음을 멈출 때 줄어드는 초")]
        [SerializeField] private float bgmStopFadeSeconds = 0.5f;
        [Tooltip("효과음 표 — 종류마다 한 줄")]
        [SerializeField] private SfxEntry[] sfx;

        private readonly Dictionary<Sfx, SfxEntry> _sfx = new();
        private readonly Dictionary<Sfx, (float time, AudioSource voice)> _lastSfx = new();
        private readonly List<RaycastResult> _pointerHits = new();
        private AudioSource[] _voices;
        private int _nextVoice;
        private bool _isBgmPlaying;

        // 누르고 있는 버튼 · 스위치 — 그 위에서 놓아야 Tap
        private Selectable _pressed;
        private Camera _pressedCamera;
        private Vector2 _pressPosition;
        private float _dragThreshold;
        private bool _dragged;

        /// <summary>효과음 스위치 (없으면 켬)</summary>
        public static bool IsSfxOn => PlayerPrefs.GetInt(PlayerPrefsKeys.SfxOn, 1) == 1;

        /// <summary>배경음 스위치 (없으면 켬)</summary>
        public static bool IsBgmOn => PlayerPrefs.GetInt(PlayerPrefsKeys.BgmOn, 1) == 1;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            if (bgmIntroSource == null)
                Debug.LogError($"[{nameof(SoundManager)}] {nameof(bgmIntroSource)} 참조가 비어 있습니다.", this);

            if (bgmLoopSource == null)
                Debug.LogError($"[{nameof(SoundManager)}] {nameof(bgmLoopSource)} 참조가 비어 있습니다.", this);

            if (bgmIntro == null)
                Debug.LogError($"[{nameof(SoundManager)}] {nameof(bgmIntro)} 참조가 비어 있습니다.", this);

            if (bgmLoop == null)
                Debug.LogError($"[{nameof(SoundManager)}] {nameof(bgmLoop)} 참조가 비어 있습니다.", this);

            foreach (SfxEntry entry in sfx ?? Array.Empty<SfxEntry>())
            {
                if (entry.clip == null)
                    Debug.LogError($"[{nameof(SoundManager)}] 효과음 {entry.id} 의 파일이 비어 있습니다.", this);
                if (_sfx.ContainsKey(entry.id))
                    Debug.LogError($"[{nameof(SoundManager)}] 효과음 표에 {entry.id} 가 두 줄 있습니다 — 뒤 줄을 씁니다.", this);
                _sfx[entry.id] = entry;
            }

            foreach (Sfx id in (Sfx[])Enum.GetValues(typeof(Sfx)))
            {
                if (!_sfx.ContainsKey(id))
                    Debug.LogError($"[{nameof(SoundManager)}] 효과음 표에 {id} 줄이 없습니다.", this);
            }

            _voices = new AudioSource[SfxVoices];
            for (int i = 0; i < SfxVoices; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        // 버튼은 화면마다 수십 개라 하나하나 물리지 않고, 누른 자리의 맨 위가 누를 수 있는 버튼 · 스위치면 그 위에서 놓을 때 소리를 낸다.
        // 끌어서 스크롤하거나 버튼 밖에서 놓아 클릭이 취소되면 내지 않는다
        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
                PressSelectable();

            if (_pressed == null) return;

            if (!_dragged && ((Vector2)Input.mousePosition - _pressPosition).sqrMagnitude >= _dragThreshold * _dragThreshold)
                _dragged = true;

            if (!Input.GetMouseButtonUp(0)) return;

            if (!_dragged && _pressed.transform is RectTransform rect
                && RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, _pressedCamera))
                Play(Sfx.Tap);
            _pressed = null;
        }

        /// <summary>효과음을 낸다. 효과음 스위치가 꺼져 있거나 SoundManager 가 없으면 아무것도 하지 않는다. volumeScale = 표의 음량에 곱할 값.</summary>
        public static void Play(Sfx id, float volumeScale = 1f)
        {
            if (Instance != null)
                Instance.PlaySfx(id, volumeScale);
        }

        /// <summary>배경음을 지금 화면 · 스위치에 맞춘다 — 설정에서 배경음 스위치를 바꾼 뒤 부른다. 효과음 스위치는 낼 때마다 읽는다.</summary>
        public void RefreshBgm()
        {
            bool wanted = IsBgmOn && bgmIntro != null && bgmLoop != null
                && Array.IndexOf(BgmScenes, SceneManager.GetActiveScene().name) >= 0;
            if (wanted == _isBgmPlaying) return;

            if (wanted)
                StartBgm();
            else
                StopBgm();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshBgm();

        private void PlaySfx(Sfx id, float volumeScale)
        {
            if (!IsSfxOn || !_sfx.TryGetValue(id, out SfxEntry entry) || entry.clip == null) return;

            float volume = entry.volume * Mathf.Clamp01(volumeScale);

            // 방금 울린 같은 소리가 아직 나고 있으면 겹치지 않고, 더 큰 쪽 음량만 따른다
            if (_lastSfx.TryGetValue(id, out var last) && Time.unscaledTime - last.time < SfxRepeatGapSeconds
                && last.voice.isPlaying && last.voice.clip == entry.clip)
            {
                last.voice.volume = Mathf.Max(last.voice.volume, volume);
                return;
            }

            AudioSource voice = NextVoice();
            voice.clip = entry.clip;
            voice.volume = volume;
            voice.pitch = 1f + UnityEngine.Random.Range(-entry.pitchJitter, entry.pitchJitter);
            voice.Play();
            _lastSfx[id] = (Time.unscaledTime, voice);
        }

        // 쉬고 있는 소스 — 다 울리는 중이면 가장 오래전에 쓴 것
        private AudioSource NextVoice()
        {
            for (int i = 0; i < _voices.Length; i++)
            {
                AudioSource voice = _voices[(_nextVoice + i) % _voices.Length];
                if (voice.isPlaying) continue;

                _nextVoice = (_nextVoice + i + 1) % _voices.Length;
                return voice;
            }

            AudioSource oldest = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            return oldest;
        }

        // 누른 자리의 맨 위가 누를 수 있는 버튼 · 스위치면 기억한다
        private void PressSelectable()
        {
            _pressed = null;
            _dragged = false;

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var pointer = new PointerEventData(eventSystem) { position = Input.mousePosition };
            _pointerHits.Clear();
            eventSystem.RaycastAll(pointer, _pointerHits);
            if (_pointerHits.Count == 0) return;

            RaycastResult hit = _pointerHits[0];
            Selectable selectable = hit.gameObject.GetComponentInParent<Selectable>();
            if (selectable == null || !selectable.IsInteractable()) return;

            // 누르고 있는 동안만 켜지는 버튼은 누르는 순간 반응하므로 소리도 그때 낸다
            if (selectable.GetComponent<HoldButton>() != null)
            {
                Play(Sfx.Tap);
                return;
            }

            _pressed = selectable;
            _pressedCamera = hit.module != null ? hit.module.eventCamera : null;
            _pressPosition = pointer.position;
            // 끌 수 있는 것(스크롤 목록) 안이면 이만큼 움직였을 때 클릭 대신 드래그가 된다 — EventSystem 과 같은 기준
            _dragThreshold = ExecuteEvents.GetEventHandler<IDragHandler>(hit.gameObject) != null
                ? eventSystem.pixelDragThreshold
                : float.PositiveInfinity;
        }

        private void StartBgm()
        {
            if (bgmIntroSource == null || bgmLoopSource == null) return;

            _isBgmPlaying = true;
            double startAt = AudioSettings.dspTime + BgmScheduleLeadSeconds;
            double introSeconds = (double)bgmIntro.samples / bgmIntro.frequency;
            Schedule(bgmIntroSource, bgmIntro, false, startAt);
            Schedule(bgmLoopSource, bgmLoop, true, startAt + introSeconds);
        }

        private void StopBgm()
        {
            _isBgmPlaying = false;
            FadeOut(bgmIntroSource);
            FadeOut(bgmLoopSource);
        }

        private void Schedule(AudioSource source, AudioClip clip, bool loop, double startAt)
        {
            source.DOKill();
            source.Stop();
            source.clip = clip;
            source.loop = loop;
            source.volume = bgmVolume;
            source.PlayScheduled(startAt);
        }

        private void FadeOut(AudioSource source)
        {
            if (source == null) return;

            source.DOKill();
            source.DOFade(0f, bgmStopFadeSeconds).OnComplete(source.Stop);
        }
    }
}
