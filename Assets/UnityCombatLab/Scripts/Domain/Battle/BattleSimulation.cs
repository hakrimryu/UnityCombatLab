using System;
using System.Collections.Generic;
using System.Linq;
using UnityCombatLab.Common;

namespace UnityCombatLab.Domain.Battle
{
    /// <summary>
    /// 時刻や描画に依存しない単一ウェーブの戦闘所有者。
    /// [KO] 시간과 렌더링에 의존하지 않는 단일 웨이브 전투 소유자.
    /// [EN] Owns one wave of combat independently of clocks and rendering.
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
        /// 変更権はこのインスタンスだけが所有する。
        /// [KO] 이 인스턴스만 변경권 소유.
        /// [EN] State mutation belongs solely to this instance.
        /// </summary>
        private readonly BattleUnitSnapshot[] _states = new BattleUnitSnapshot[Define.MaxBattleUnitCount];

        /// <summary>
        /// 同期消費される一ティック分の所有バッファ。
        /// [KO] 동기 소비하는 한 틱 분량의 소유 버퍼.
        /// [EN] Owned single-tick buffer consumed synchronously.
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
        /// 入力を固定スロットへコピーし重複を拒否します。空陣営は初回 Step で境界判定します。
        /// [KO] 고정 슬롯에 입력 복사 후 중복 거부, 빈 팀은 첫 Step에서 경계 판정.
        /// [EN] Copies inputs into fixed slots and rejects duplicates; empty teams resolve on first Step.
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
        /// 未消費バッファから値をコピーします。参照は公開しません。
        /// [KO] 미소비 버퍼에서 값 복사, 참조 비공개.
        /// [EN] Copies a value from the pending buffer without exposing its storage.
        /// </summary>
        public BattleEvent GetEvent(int index)
        {
            return (uint)index >= EventCount ? throw new ArgumentOutOfRangeException(nameof(index)) : _events[index];
        }

        /// <summary>
        /// 同期消費の完了を通知します。必要な値をコピーした後だけ呼びます。
        /// [KO] 필요한 값을 복사한 뒤 동기 소비 완료 통지.
        /// [EN] Acknowledges synchronous consumption after required values have been copied.
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
        /// スロットを交互順の配列添字へ変換します。
        /// [KO] 슬롯을 교차 순서 배열 인덱스로 변환.
        /// [EN] Converts a slot to an interleaved array index.
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
        /// 生存対象だけを固定順に探します。
        /// [KO] 생존 대상만 고정 순서 탐색.
        /// [EN] Searches living targets in fixed order.
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
        /// 両陣営ゼロは敗北を優先します。
        /// [KO] 양 팀 0이면 패배 우선.
        /// [EN] Both teams empty resolves as defeat.
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
        /// 所有バッファへ正規順に追記します。
        /// [KO] 소유 버퍼에 정규 순서로 추가.
        /// [EN] Appends to the owned buffer in canonical order.
        /// </summary>
        private void Emit(Define.BattleEventKind kind, int actor, int target, int healthDamage = 0,
            int shieldDamage = 0)
        {
            _events[EventCount++] = new BattleEvent(_runId, _wave, CurrentTick, ++_sequence,
                actor, target, kind, healthDamage, shieldDamage, Result);
        }
    }
}
