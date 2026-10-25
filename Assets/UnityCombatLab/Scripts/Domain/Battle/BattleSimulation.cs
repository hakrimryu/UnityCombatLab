using System;
using System.Collections.Generic;
using System.Linq;
using UnityCombatLab.Common;

namespace UnityCombatLab.Domain.Battle
{
    /// <summary>
    /// 一回のウェーブを 20Hz で進め、攻撃・被害・死亡と勝敗を計算します。
    /// [KO] 한 웨이브를 20Hz로 진행하며 공격·피해·사망과 승패를 계산합니다.
    /// [EN] Advances one wave at 20Hz and calculates attacks, damage, deaths and the outcome.
    /// </summary>
    public sealed class BattleSimulation
    {
        /// <summary>
        /// 各行動の攻撃と最大対象数の被害、各ユニットの死亡一回、戦闘終了一回を確保します。
        /// [KO] 행동별 공격·최대 대상 수의 피해, 유닛별 사망 한 번과 전투 종료 한 번의 공간을 확보합니다.
        /// [EN] Reserves each action and its maximum damage targets, one death per unit, and one battle end.
        /// </summary>
        private const int EventBufferCapacity =
            Define.MaxBattleUnitCount * (1 + Define.SlotsPerTeam) + Define.MaxBattleUnitCount + 1;

        /// <summary>
        /// 全ユニットの現在の HP・盾・攻撃回数を保持する配列。
        /// [KO] 모든 유닛의 현재 HP·방어막·공격 횟수를 보관하는 배열입니다.
        /// [EN] Array storing every unit's current health, shields and attack count.
        /// </summary>
        private readonly BattleUnitSnapshot[] _states = new BattleUnitSnapshot[Define.MaxBattleUnitCount];

        /// <summary>
        /// 一回の Step で発生したイベントを、通知完了まで保持する配列。
        /// [KO] 한 번의 Step에서 발생한 이벤트를 알림 완료까지 보관하는 배열입니다.
        /// [EN] Array retaining events from one Step until delivery completes.
        /// </summary>
        private readonly BattleEvent[] _events = new BattleEvent[EventBufferCapacity];

        /// <summary>
        /// 呼び出し元から渡された挑戦識別子。
        /// [KO] 호출자가 전달한 도전 식별자.
        /// [EN] Caller-supplied run identifier.
        /// </summary>
        private readonly Guid _runId;

        /// <summary>
        /// 呼び出し元のウェーブ番号。
        /// [KO] 호출자 웨이브 번호.
        /// [EN] Caller-supplied wave number.
        /// </summary>
        private readonly int _wave;

        /// <summary>
        /// 戦闘全体で増加するイベント連番。
        /// [KO] 전투 전체에서 증가하는 이벤트 순번.
        /// [EN] Event sequence across the battle.
        /// </summary>
        private int _sequence;

        /// <summary>
        /// 完了したティック数。
        /// [KO] 완료된 틱 수.
        /// [EN] Completed tick count.
        /// </summary>
        public int CurrentTick { get; private set; }

        /// <summary>
        /// 終了後は変化しない結果。
        /// [KO] 종료 후 불변인 결과.
        /// [EN] Result fixed after termination.
        /// </summary>
        public Define.BattleResult Result { get; private set; }

        /// <summary>
        /// 未消費イベント数。
        /// [KO] 미소비 이벤트 수.
        /// [EN] Number of unconsumed events.
        /// </summary>
        public int EventCount { get; private set; }

        /// <summary>
        /// 開始データを陣営・スロット順に並べ、重複を検査します。空陣営の勝敗は最初の Step で判定します。
        /// [KO] 시작 데이터를 팀·슬롯 순서로 배치하고 중복을 검사하며 빈 팀의 승패는 첫 Step에서 판정합니다.
        /// [EN] Orders starting data by team and slot and checks duplicates; the first Step resolves empty teams.
        /// </summary>
        public BattleSimulation(Guid runId, int wave, IReadOnlyList<BattleUnitInput> units)
        {
            if (runId == Guid.Empty)
            {
                throw new ArgumentException("Run ID is required.", nameof(runId));
            }

            if (wave < 1 || wave > Define.WavesPerRun)
            {
                throw new ArgumentOutOfRangeException(nameof(wave));
            }

            if (units == null)
            {
                throw new ArgumentNullException(nameof(units));
            }

            if (units.Count > Define.MaxBattleUnitCount)
            {
                throw new ArgumentException("Unit count exceeds the available battle slots.", nameof(units));
            }

            _runId = runId;
            _wave = wave;
            foreach (var input in units)
            {
                var unit = input ?? throw new ArgumentException("Unit must not be null.", nameof(units));
                var index = Index(unit.Team, unit.Slot);
                if (_states[index].Input != null)
                {
                    throw new ArgumentException("Duplicate slot.", nameof(units));
                }

                if (_states.Any(existing => existing.Input?.Id == unit.Id))
                {
                    throw new ArgumentException("Duplicate unit ID.", nameof(units));
                }

                _states[index] = new BattleUnitSnapshot(unit, unit.Health, unit.Shield, 0, Define.FirstAttackTick);
            }
        }

        /// <summary>
        /// 不変の状態コピーを返します。空スロットは既定値です。
        /// [KO] 불변 상태 복사본 반환, 빈 슬롯은 기본값.
        /// [EN] Returns a value snapshot; empty slots return default.
        /// </summary>
        public BattleUnitSnapshot GetUnit(Define.BattleTeam team, Define.BattleSlot slot) => _states[Index(team, slot)];

        /// <summary>
        /// まだ通知していないイベントを発生順の番号で取得します。
        /// [KO] 아직 알리지 않은 이벤트를 발생 순서 번호로 조회합니다.
        /// [EN] Gets an undelivered event by its occurrence-order index.
        /// </summary>
        public BattleEvent GetEvent(int index)
        {
            return (uint)index >= EventCount ? throw new ArgumentOutOfRangeException(nameof(index)) : _events[index];
        }

        /// <summary>
        /// Runner が全イベントを通知した後、イベント数を 0 に戻します。
        /// [KO] Runner가 모든 이벤트를 알린 뒤 이벤트 수를 0으로 되돌립니다.
        /// [EN] Resets the event count after the runner has delivered all events.
        /// </summary>
        public void AcknowledgeEvents() => EventCount = 0;

        /// <summary>
        /// 50ms進めます。未消費イベントは上書きせず拒否し、終了後は無操作です。
        /// [KO] 50ms 진행, 미소비 이벤트 덮어쓰기 거부, 종료 후 무효.
        /// [EN] Advances 50ms, rejects overwriting unconsumed events, and does nothing after termination.
        /// </summary>
        public void Step()
        {
            if (Result != Define.BattleResult.Ongoing)
            {
                return;
            }

            if (EventCount != 0)
            {
                throw new InvalidOperationException("Consume and acknowledge events before stepping.");
            }

            if (TryEnd())
            {
                return;
            }

            CurrentTick++;
            // 攻撃可能時刻は各行動で再評価しても変わらず、死亡だけが先行行動で変わる。
            for (var actor = 0; actor < _states.Length; actor++)
            {
                var state = _states[actor];
                if (!state.IsAlive || state.NextAttackTick > CurrentTick)
                {
                    continue;
                }

                var target = FindTarget(actor);
                if (target < 0)
                {
                    continue;
                }

                var attackCount = state.AttackCount + 1;
                var role = state.Input.Role;
                var isArea = BattleCalculation.IsAreaAttack(role, attackCount);

                Emit(Define.BattleEventKind.AttackExecuted, state.Input.Id, isArea ? 0 : _states[target].Input.Id);
                if (isArea)
                {
                    for (var slot = 0; slot < Define.SlotsPerTeam; slot++)
                    {
                        var areaTarget = GetOpponentIndex(actor, slot);
                        if (_states[areaTarget].IsAlive)
                        {
                            ApplyDamage(actor, areaTarget, attackCount);
                        }
                    }
                }
                else
                {
                    ApplyDamage(actor, target, attackCount);
                }

                _states[actor] = new BattleUnitSnapshot(state.Input, state.Health, state.Shield,
                    attackCount, CurrentTick + state.Input.IntervalTicks);
                if (TryEnd())
                {
                    return;
                }
            }

            // 制限ティックの攻撃と全滅判定を先に完了する。
            if (CurrentTick >= Define.BattleTickLimit)
            {
                End(Define.BattleResult.Timeout);
            }
        }

        /// <summary>
        /// 陣営とスロットを配列番号に変換します。0/1 は味方/敵の前列、2/3 は後列上、4/5 は後列下です。
        /// [KO] 팀과 슬롯을 배열 번호로 변환합니다. 0/1은 아군/적 전열, 2/3은 위쪽 후열, 4/5는 아래쪽 후열입니다.
        /// [EN] Converts team and slot to an array index: 0/1 are ally/enemy front, 2/3 upper-back, and 4/5 lower-back.
        /// </summary>
        private static int Index(Define.BattleTeam team, Define.BattleSlot slot)
        {
            if ((uint)team >= Define.BattleTeamCount)
            {
                throw new ArgumentOutOfRangeException(nameof(team));
            }

            if ((uint)slot >= Define.SlotsPerTeam)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            return (int)slot * Define.BattleTeamCount + (int)team;
        }

        /// <summary>
        /// 生存している相手を探します。敵射手は後列上→後列下→前列、それ以外は前列→後列上→後列下の順です。
        /// [KO] 살아 있는 상대를 찾습니다. 적 사수는 위쪽 후열→아래쪽 후열→전열, 나머지는 전열→위쪽 후열→아래쪽 후열 순서입니다.
        /// [EN] Finds a living opponent: enemy ranged units search upper-back, lower-back, front; other roles search front, upper-back, lower-back.
        /// </summary>
        private int FindTarget(int actor)
        {
            var isRanged = _states[actor].Input.Role == Define.UnitRole.SkeletonRanged;
            for (var order = 0; order < Define.SlotsPerTeam; order++)
            {
                var slot = isRanged ? (order + (int)Define.BattleSlot.BackUpper) % Define.SlotsPerTeam : order;
                var target = GetOpponentIndex(actor, slot);
                if (_states[target].IsAlive)
                {
                    return target;
                }
            }

            return -1;
        }

        /// <summary>
        /// 交互配列の相手陣営と対象スロットから添字を求めます。
        /// [KO] 교차 배열의 상대 팀과 대상 슬롯으로 인덱스를 구합니다.
        /// [EN] Resolves an interleaved index from the opposing team and target slot.
        /// </summary>
        private static int GetOpponentIndex(int actor, int slot)
        {
            // 偶数0/2/4は味方、奇数1/3/5は敵。味方の前列actor=0が敵の後列上slot=1を狙うなら結果は3。
            var actorTeam = (Define.BattleTeam)(actor % Define.BattleTeamCount);
            var opponentTeam = actorTeam == Define.BattleTeam.Ally
                ? Define.BattleTeam.Enemy
                : Define.BattleTeam.Ally;
            return slot * Define.BattleTeamCount + (int)opponentTeam;
        }

        /// <summary>
        /// 計算済みの被害を盾、HP、死亡へ即時適用します。
        /// [KO] 계산한 피해를 방어막·HP·사망에 즉시 적용합니다.
        /// [EN] Immediately applies calculated damage to shield, health, and death.
        /// </summary>
        private void ApplyDamage(int actor, int target, int attackCount)
        {
            var state = _states[target];
            var attacker = _states[actor].Input;
            var finalDamage = BattleCalculation.CalculateDamage(attacker, attackCount, state.Input.Role);
            var shieldDamage = Math.Min(state.Shield, finalDamage);
            var healthDamage = Math.Min(state.Health, finalDamage - shieldDamage);
            _states[target] = new BattleUnitSnapshot(state.Input, state.Health - healthDamage,
                state.Shield - shieldDamage, state.AttackCount, state.NextAttackTick);
            Emit(Define.BattleEventKind.DamageApplied, attacker.Id, state.Input.Id, healthDamage, shieldDamage);
            if (!_states[target].IsAlive)
            {
                Emit(Define.BattleEventKind.UnitDied, attacker.Id, state.Input.Id);
            }
        }

        /// <summary>
        /// 全滅した陣営を確認して勝敗を確定します。両陣営とも全滅なら敗北です。
        /// [KO] 전멸한 팀을 확인해 승패를 확정하며 양 팀이 모두 전멸했다면 패배입니다.
        /// [EN] Checks eliminated teams and determines the outcome; both teams eliminated means defeat.
        /// </summary>
        private bool TryEnd()
        {
            var hasAlly = false;
            var hasEnemy = false;
            for (var i = 0; i < _states.Length; i++)
            {
                if (!_states[i].IsAlive)
                {
                    continue;
                }

                if (i % Define.BattleTeamCount == (int)Define.BattleTeam.Ally)
                {
                    hasAlly = true;
                }
                else
                {
                    hasEnemy = true;
                }
            }

            if (!hasAlly)
            {
                End(Define.BattleResult.Defeat);
            }
            else if (!hasEnemy)
            {
                End(Define.BattleResult.Victory);
            }

            return Result != Define.BattleResult.Ongoing;
        }

        /// <summary>
        /// 終了を一度だけ記録します。
        /// [KO] 종료를 한 번만 기록.
        /// [EN] Records termination exactly once.
        /// </summary>
        private void End(Define.BattleResult result)
        {
            Result = result;
            Emit(Define.BattleEventKind.BattleEnded, 0, 0);
        }

        /// <summary>
        /// イベントに連番を付け、今回の Step のイベント配列へ追加します。
        /// [KO] 이벤트에 순번을 붙여 이번 Step의 이벤트 배열에 추가합니다.
        /// [EN] Assigns an event sequence number and appends it to the current Step's event array.
        /// </summary>
        private void Emit(Define.BattleEventKind kind, int actor, int target, int healthDamage = 0,
            int shieldDamage = 0)
        {
            _events[EventCount++] = new BattleEvent(_runId, _wave, CurrentTick, ++_sequence,
                actor, target, kind, healthDamage, shieldDamage, Result);
        }
    }
}
