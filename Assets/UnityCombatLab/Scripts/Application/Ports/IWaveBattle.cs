using UnityCombatLab.Application.Battle;

namespace UnityCombatLab.Application.Ports
{
    /// <summary>
    /// 一ウェーブの計算と表示を進め、最後の演出が終わったかを Application へ返します。
    /// [KO] 한 웨이브의 계산·표시를 진행하고 마지막 연출 완료 여부를 Application에 반환합니다.
    /// [EN] Advances one wave's simulation and presentation and reports final presentation completion to Application.
    /// </summary>
    public interface IWaveBattle : IRunBattle
    {
        /// <summary>
        /// この戦闘が所有し、Dispose で解放する計算とイベントの通知元。
        /// [KO] 이 전투가 소유하고 Dispose에서 해제할 계산·이벤트 알림 원본입니다.
        /// [EN] Simulation and event source owned and disposed by this battle.
        /// </summary>
        BattleRunner Runner { get; }

        /// <summary>
        /// 確定した勝敗に対応する最後の命中・死亡表示まで終わったか。
        /// [KO] 확정 승패에 대응하는 마지막 명중·사망 표시까지 끝났는지 나타냅니다.
        /// [EN] Whether the final impacts and deaths for the committed outcome have finished displaying.
        /// </summary>
        bool IsPresentationComplete { get; }

        /// <summary>
        /// Run が決めた経過秒で計算と表示を進めます。
        /// [KO] Run이 정한 경과 초로 계산·표시를 진행합니다.
        /// [EN] Advances simulation and presentation using elapsed seconds selected by the run.
        /// </summary>
        void Advance(float combatDeltaSeconds);

        /// <summary>
        /// 自分の表示へ停止・一倍・二倍の再生速度を反映します。
        /// [KO] 자신의 표시에 정지·1배·2배 재생 속도를 반영합니다.
        /// [EN] Applies paused, normal or double playback speed to owned presentation.
        /// </summary>
        void SetPlaybackSpeed(int speed);
    }
}
