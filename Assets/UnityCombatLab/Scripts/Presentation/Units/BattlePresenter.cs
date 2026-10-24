using System;
using System.Collections.Generic;
using System.Linq;
using UnityCombatLab.Application.Battle;
using UnityCombatLab.Common;
using UnityCombatLab.Domain.Battle;
using UnityCombatLab.Presentation.UI;

namespace UnityCombatLab.Presentation.Units
{
    /// <summary>
    /// Runner の攻撃・被害・死亡通知を受け、該当キャラクターのアニメーションと被害数字をその場で指示します。
    /// [KO] Runner의 공격·피해·사망 알림을 받아 해당 캐릭터의 애니메이션과 피해 숫자 표시를 즉시 지시합니다.
    /// [EN] Receives runner attack, damage and death notifications and immediately directs character effects and damage labels.
    /// </summary>
    public sealed class BattlePresenter : IDisposable
    {
        /// <summary>
        /// 借用するイベント発行元。
        /// [KO] 빌려 쓰는 이벤트 발행 원본.
        /// [EN] Borrowed event source.
        /// </summary>
        private readonly BattleRunner _runner;

        /// <summary>
        /// イベントのユニット ID から _views の位置を探す表。ID 1 なら騎士の View を取り出します。
        /// [KO] 이벤트의 유닛 ID로 _views의 위치를 찾는 표입니다. ID 1이면 기사의 View를 찾습니다.
        /// [EN] Maps event unit IDs to positions in _views; ID 1 locates the knight view.
        /// </summary>
        private readonly Dictionary<int, int> _indices;

        /// <summary>
        /// 表示寿命の間だけ借用するユニット View。
        /// [KO] 표시 수명 동안만 빌려 쓰는 유닛 View.
        /// [EN] Unit views borrowed only for the presentation lifetime.
        /// </summary>
        private readonly UnitView[] _views;

        /// <summary>
        /// 確定被害の表示先。
        /// [KO] 확정 피해 표시 대상.
        /// [EN] Committed damage display targets.
        /// </summary>
        private readonly BattleHudView _hud;

        /// <summary>
        /// 古い挑戦のイベントを拒否する識別子。
        /// [KO] 이전 도전 이벤트를 거부하는 식별자.
        /// [EN] Identifier rejecting events from older runs.
        /// </summary>
        private readonly Guid _runId;

        /// <summary>
        /// 同一挑戦内の古いウェーブも拒否します。
        /// [KO] 같은 도전의 이전 웨이브도 거부합니다.
        /// [EN] Also rejects older waves in the same run.
        /// </summary>
        private readonly int _wave;

        /// <summary>
        /// 解除後の呼び出しを無効にします。
        /// [KO] 해제 후 호출을 무효화합니다.
        /// [EN] Invalidates calls after disposal.
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// BattleEntryPoint.Start が呼びます。各 View に今回の RunId を設定し、Runner からの通知を受け始めます。
        /// [KO] BattleEntryPoint.Start가 호출합니다. 각 View에 이번 RunId를 설정하고 Runner의 알림을 받기 시작합니다.
        /// [EN] Called by BattleEntryPoint.Start to assign this RunId to the views and start listening to runner events.
        /// </summary>
        public BattlePresenter(BattleRunner runner, Guid runId, int wave, UnitView[] views, BattleHudView hud)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _hud = hud != null ? hud : throw new ArgumentNullException(nameof(hud));
            if (views == null || views.Length != runner.ReadModel.Units.Count || views.Any(view => view == null) ||
                views.Distinct().Count() != views.Length)
            {
                throw new ArgumentException("Each battle slot requires its own UnitView.", nameof(views));
            }

            _views = (UnitView[])views.Clone();
            _indices = runner.ReadModel.Units.Select((unit, index) => (unit.Input.Id, index))
                .ToDictionary(pair => pair.Id, pair => pair.index);
            _runId = runId;
            _wave = wave;
            foreach (var view in _views)
            {
                view.Bind(runId);
            }

            _runner.EventOccurred += HandleEventOccurred;
        }

        /// <summary>
        /// 最後の死亡まで順序通りに表現してから終了します。
        /// [KO] 마지막 사망까지 순서대로 표현한 후 종료합니다.
        /// [EN] Presents through the final death in order before finishing.
        /// </summary>
        private void HandleEventOccurred(BattleEvent battleEvent)
        {
            if (_isDisposed || battleEvent.RunId != _runId || battleEvent.Wave != _wave)
            {
                return;
            }

            if (battleEvent.Kind == Define.BattleEventKind.BattleEnded)
            {
                foreach (var view in _views)
                {
                    if (view != null)
                    {
                        view.FinishBattle();
                    }
                }

                return;
            }

            // TargetId=0 は全体攻撃で、単一の移動先がない。配列位置 -1 と null で「対象一人なし」を表す。
            // その後に届く DamageApplied は各被害者の実 ID を持つので、一人ずつ被弾表示できる。
            var targetIndex = battleEvent.TargetId == 0 ? -1 : _indices[battleEvent.TargetId];
            var target = targetIndex < 0 ? null : _views[targetIndex];
            if (battleEvent.Kind == Define.BattleEventKind.AttackExecuted)
            {
                var actorIndex = _indices[battleEvent.ActorId];
                var actor = _views[actorIndex];
                var input = _runner.ReadModel.Units[actorIndex].Input;
                var state = _runner.GetUnit(input.Team, input.Slot);
                var isMelee = input.Role is Define.UnitRole.Knight or Define.UnitRole.SkeletonMelee or
                    Define.UnitRole.SkeletonCaptain;
                var isArea = battleEvent.TargetId == 0;
                // 全体攻撃、または弓使いの 4・8・12 回目の攻撃を強調する。余り 0 が周期の境目。
                // ここで決めるのは見た目だけ。特殊攻撃の被害と対象はコアが既に計算している。
                var isSpecial = isArea ||
                                (input.Role == Define.UnitRole.Ranger &&
                                 state.AttackCount % Define.RangerSpecialAttackPeriod == 0);
                if (actor != null && (isArea || target != null))
                {
                    var targetPosition = isArea ? actor.transform.position : target.transform.position;
                    actor.ShowAttack(_runId, targetPosition, isMelee && !isArea, isSpecial);
                }
            }
            else if (battleEvent.Kind == Define.BattleEventKind.DamageApplied)
            {
                if (target != null)
                {
                    target.ShowHit(_runId);
                }

                if (_hud != null && _hud.Units[targetIndex] != null)
                {
                    _hud.Units[targetIndex].ShowDamage(battleEvent.HealthDamage, battleEvent.ShieldDamage);
                }
            }
            else if (battleEvent.Kind == Define.BattleEventKind.UnitDied && target != null)
            {
                target.ShowDeath(_runId);
            }
        }

        /// <summary>
        /// イベントを解除してから所有演出を停止します。
        /// [KO] 이벤트를 해제한 후 소유 연출을 정지합니다.
        /// [EN] Detaches events before stopping owned presentation.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _runner.EventOccurred -= HandleEventOccurred;
            foreach (var view in _views)
            {
                if (view != null)
                {
                    view.Release();
                }
            }
        }
    }
}
