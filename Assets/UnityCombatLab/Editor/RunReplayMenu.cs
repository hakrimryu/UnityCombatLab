using UnityCombatLab.Application.Run;
using UnityCombatLab.Bootstrap;
using UnityCombatLab.Domain.Run;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace UnityCombatLab.Editor
{
    /// <summary>
    /// 同じ編成と強化選択から攻撃・ダメージ・勝敗を再現できるか確認する Unity Editor メニューです。
    /// Run は、編成を決めて出発してから五ウェーブをクリアするか、途中で敗北するまでの一回の挑戦を指します。
    /// [KO] 같은 편성과 강화 선택으로 공격·피해·승패가 재현되는지 확인하는 Unity Editor 메뉴입니다.
    /// [KO] Run은 편성을 정해 출발한 뒤 5웨이브를 클리어하거나 중간에 패배할 때까지의 한 판을 뜻합니다.
    /// [EN] Unity Editor menu for checking that the same formation and upgrade choices reproduce attacks, damage and outcomes.
    /// [EN] A run is one attempt, from departing with a chosen formation until clearing five waves or losing along the way.
    /// </summary>
    public static class RunReplayMenu
    {
        /// <summary>
        /// 一回の挑戦が終わった後、Play モードを終了する前に Verify Last Run メニューから呼び出します。
        /// RunFlowCoordinator.LastReplay に保存された直近の記録を RunReplayVerifier で再計算し、一致結果または最初の相違を Console に表示します。記録がなければ警告を表示します。
        /// [KO] 한 판이 끝난 뒤 Play 모드를 종료하기 전에 Verify Last Run 메뉴에서 호출합니다.
        /// [KO] RunFlowCoordinator.LastReplay에 저장된 직전 기록을 RunReplayVerifier로 재계산하고 일치 여부 또는 처음 달라진 내용을 Console에 표시합니다. 기록이 없으면 경고를 표시합니다.
        /// [EN] Invoked through Verify Last Run after an attempt ends and before leaving Play mode.
        /// [EN] Recomputes the latest record in RunFlowCoordinator.LastReplay through RunReplayVerifier and reports a match or the first difference in Console. Warns if no record is available.
        /// </summary>
        [MenuItem("UnityCombatLab/Development/Verify Last Run")]
        public static void VerifyLastRun()
        {
            var scope = Object.FindFirstObjectByType<AppLifetimeScope>();
            var record = UnityEngine.Application.isPlaying && scope != null
                ? scope.Container.Resolve<RunFlowCoordinator>().LastReplay
                : null;
            if (record == null)
            {
                Debug.LogWarning("Finish a run, then invoke Verify Last Run before leaving Play mode.");
                return;
            }

            var result = RunReplayVerifier.Verify(record);
            var report = $"Replay {(result.IsMatch ? "MATCH" : "MISMATCH")}: {result.Message} " +
                         $"Expected={result.ExpectedHash:X16}, Actual={result.ActualHash:X16}, Seed={record.Result.Configuration.Seed}, " +
                         $"Waves={record.Result.ReachedWave}, Ticks={record.Result.Ticks}, Events={record.Events.Count}.";
            if (result.IsMatch)
                Debug.Log(report);
            else
                Debug.LogError(report);
        }
    }
}
