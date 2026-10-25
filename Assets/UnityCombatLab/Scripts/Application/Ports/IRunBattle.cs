using System;

namespace UnityCombatLab.Application.Ports
{
    /// <summary>
    /// Runが開始と破棄だけを要求できる戦闘の境界。
    /// [KO] Run에서 시작과 해제만 요청할 수 있는 전투 경계입니다.
    /// [EN] Battle boundary through which a run can request startup and disposal.
    /// </summary>
    public interface IRunBattle : IDisposable
    {
        /// <summary>
        /// 資産とシーンの準備が完了した後に戦闘を開始します。
        /// [KO] 자산과 씬 준비 완료 후 전투를 시작합니다.
        /// [EN] Starts combat after assets and the scene are ready.
        /// </summary>
        void Start();
    }
}
