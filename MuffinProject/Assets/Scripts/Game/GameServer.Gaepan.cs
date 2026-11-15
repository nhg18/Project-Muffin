using System.Linq;

namespace Chapchu.Game
{
    // GameServer.Gaepan.cs — 121 개판 오분전: 바퀴 세기 · 반반 기준 판정 (11-card-list 121, 프로토타입 v2 gpTick)
    public partial class GameServer
    {
        // 확정 (11-card-list 121 · 사용자 2026-11-13).
        public const int GaepanRounds = 5;
        // 판정 공개 뒤 HP 0 까지 — 프로토타입 v2 판정 팻말 연출(4명 기준 최대): 기준 룰렛 0.52 + 1.6 + 0.26 → 수치 표 1.14 → 「꼴찌는?!」 0.65.
        // 처형씬(6.75초)은 넣지 않는다 (사용자 2026-11-13 — 2.3초를 이 길이로 바꿈). 화면은 GaepanJudgeView 가 같은 시간표로 그린다.
        public const double GaepanJudgeSeconds = 4.17;

        private int _gaepanCaster = -1; // 개판을 건 사람. -1 이면 개판 없음 — 건 사람이 죽거나 나가도 이어진다
        private int _gaepanLeft;        // 남은 바퀴
        private int _gaepanTurns;       // 이번 바퀴에 지난 턴 수 — 살아 있는 사람 수만큼이면 한 바퀴
        private bool _gaepanFresh;      // 건 턴의 끝은 세지 않는다
        private double _gaepanJudgeAt = -1; // 판정 공개 중 — 이 시각에 꼴찌의 HP 를 0 으로. 음수면 없음
        private int _gaepanLoser = -1;

        internal bool IsGaepanJudging => _gaepanJudgeAt >= 0;

        /// <summary>개판 시작 (StartGaepanEffect). 이미 개판이면 처음부터 다시 센다.</summary>
        internal void StartGaepan(int caster)
        {
            bool again = _gaepanCaster != -1;
            _gaepanCaster = caster;
            _gaepanLeft = GaepanRounds;
            _gaepanTurns = 0;
            _gaepanFresh = true;
            _events.Add(new GaepanChanged(caster, _gaepanLeft, again));
        }

        // 턴 하나가 끝났다 (실제 턴 · 스킵된 턴 모두, 사용자 2026-11-13). 한 바퀴면 1 줄이고, 0 이면 판정을 연다.
        private void TickGaepan()
        {
            if (_gaepanCaster == -1)
                return;
            if (_gaepanFresh)
            {
                _gaepanFresh = false;
                return;
            }

            int[] alive = AliveActors();
            if (++_gaepanTurns < System.Math.Max(1, alive.Length))
                return;

            _gaepanTurns = 0;
            _gaepanLeft--;
            int caster = _gaepanCaster;
            if (_gaepanLeft > 0)
            {
                _events.Add(new GaepanChanged(caster, _gaepanLeft, false));
                return;
            }

            _gaepanCaster = -1;
            _events.Add(new GaepanChanged(caster, 0, false));
            if (alive.Length < 2)
                return; // 판정 없음 (11-card-list 121 예외)

            bool byHp = _random.Next(2) == 0;
            int ValueOf(int actor) => byHp ? _players[actor].Hp : _players[actor].Hand.Count;
            int lowest = alive.Min(ValueOf);
            int[] tied = alive.Where(actor => ValueOf(actor) == lowest).ToArray();
            _gaepanLoser = tied[_random.Next(tied.Length)];
            _gaepanJudgeAt = _clock() + GaepanJudgeSeconds;
            _events.Add(new GaepanJudged(_gaepanLoser, byHp, lowest, tied, alive, alive.Select(ValueOf).ToArray()));
        }

        // 판정 공개가 끝났다 — 꼴찌의 HP 를 0 으로 (피해가 아니라 설정). 사망 대기는 보통대로 (06 3절).
        private void FinishGaepanJudge(double now)
        {
            int loser = _gaepanLoser;
            _gaepanJudgeAt = -1;
            _gaepanLoser = -1;
            if (loser == -1 || !IsInGame(loser) || _players[loser].Life != LifeState.Alive)
                return; // 공개 중에 나갔거나 이미 사망 대기 — 그대로

            SetHp(loser, 0);
            CheckDeathPending(now);
        }

        // 112 대소동 · 게임 시작 — 개판은 처음으로
        private void ResetGaepan()
        {
            bool had = _gaepanCaster != -1;
            _gaepanCaster = -1;
            _gaepanLeft = 0;
            _gaepanTurns = 0;
            _gaepanFresh = false;
            _gaepanJudgeAt = -1;
            _gaepanLoser = -1;
            if (had)
                _events.Add(new GaepanChanged(-1, 0, false));
        }

        // 게임에 남은 생존자 (사망 대기 제외) — 바퀴의 길이 · 판정 후보
        private int[] AliveActors() => _turnOrder.Where(actor => IsInGame(actor) && _players[actor].Life == LifeState.Alive).ToArray();

        /// <summary>개판 오분전이 걸려 있다 (121).</summary>
        public bool IsGaepanActive => _gaepanCaster != -1;
    }
}
