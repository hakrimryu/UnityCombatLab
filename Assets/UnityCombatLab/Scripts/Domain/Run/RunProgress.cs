using System;
using System.Collections.Generic;
using UnityCombatLab.Common;

namespace UnityCombatLab.Domain.Run
{
    /// <summary>
    /// RunController が所有するウェーブ進行・確定強化・一度だけ作る報酬候補。
    /// [KO] RunController가 소유하는 웨이브 진행·확정 강화·한 번만 생성하는 보상 후보입니다.
    /// [EN] Wave progress, committed upgrades and once-generated reward offers owned by RunController.
    /// </summary>
    public sealed class RunProgress
    {

        /// <summary>
        /// Run 開始時に一度初期化し、ウェーブ間でも保持する抽選状態。
        /// [KO] Run 시작 시 한 번 초기화하고 웨이브 사이에도 유지하는 추첨 상태입니다.
        /// [EN] Draw state initialized once per run and retained across waves.
        /// </summary>
        private readonly RewardRandom _random;

        /// <summary>
        /// 現在のクリアで一度だけ作った変更不能な候補。
        /// [KO] 현재 클리어에서 한 번만 생성한 불변 후보입니다.
        /// [EN] Immutable offers generated once for the current clear.
        /// </summary>
        private IReadOnlyList<Define.RewardKind> _offers;

        /// <summary>
        /// 同じウェーブの時間と勝利を二度加算しないための状態。
        /// [KO] 같은 웨이브의 시간·승리를 두 번 더하지 않는 상태입니다.
        /// [EN] State preventing duplicate time and victory accounting for a wave.
        /// </summary>
        private bool _hasCompletedWave;

        /// <summary>
        /// Run 開始時に固定した入力データ。
        /// [KO] Run 시작 시 고정한 입력 데이터입니다.
        /// [EN] Input data frozen when this run started.
        /// </summary>
        public RunGameData Data { get; }

        /// <summary>
        /// 現在の1始まりウェーブ番号。
        /// [KO] 1부터 시작하는 현재 웨이브 번호입니다.
        /// [EN] Current one-based wave number.
        /// </summary>
        public int Wave { get; private set; } = 1;

        /// <summary>
        /// 勝利済みウェーブ数。
        /// [KO] 승리한 웨이브 수입니다.
        /// [EN] Number of victorious waves.
        /// </summary>
        public int ClearedWaves { get; private set; }

        /// <summary>
        /// 終了した各戦闘の tick 合計。待機時間は含みません。
        /// [KO] 종료한 각 전투의 tick 합계이며 대기 시간은 제외합니다.
        /// [EN] Sum of completed battle ticks, excluding waiting time.
        /// </summary>
        public int TotalTicks { get; private set; }

        /// <summary>
        /// 最後に終了したウェーブの勝敗。
        /// [KO] 마지막으로 종료한 웨이브의 승패입니다.
        /// [EN] Outcome of the most recently completed wave.
        /// </summary>
        public Define.BattleResult Outcome { get; private set; }

        /// <summary>
        /// 選択時の効果を保持する変更不能な強化状態。
        /// [KO] 선택 시점의 효과를 보관하는 불변 강화 상태입니다.
        /// [EN] Immutable upgrades retaining effects fixed at selection time.
        /// </summary>
        public RunRewards Rewards { get; private set; } = RunRewards.Empty;

        /// <summary>
        /// 最終勝利または敗北で Run が終了しているか。
        /// [KO] 최종 승리나 패배로 Run이 끝났는지 나타냅니다.
        /// [EN] Whether final victory or defeat has ended the run.
        /// </summary>
        public bool IsFinished => _hasCompletedWave && (Outcome != Define.BattleResult.Victory || Wave == Define.WavesPerRun);

        /// <summary>
        /// 固定データを受け取り、この Run 専用の乱数状態を作ります。
        /// [KO] 고정 데이터를 받고 이 Run 전용 난수 상태를 만듭니다.
        /// [EN] Receives fixed data and creates a random state dedicated to this run.
        /// </summary>
        public RunProgress(RunGameData data, uint seed)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            _random = new RewardRandom(seed);
        }

        /// <summary>
        /// 終了結果を一度だけ合計し、二重報酬や二重時間加算を拒否します。
        /// [KO] 종료 결과를 한 번만 합산하고 중복 보상·시간 합산을 거부합니다.
        /// [EN] Accounts for a wave once and rejects duplicate rewards or time accounting.
        /// </summary>
        public bool TryCompleteWave(int wave, Define.BattleResult result, int ticks)
        {
            if (wave != Wave || _hasCompletedWave || result == Define.BattleResult.Ongoing ||
                !Enum.IsDefined(typeof(Define.BattleResult), result) || ticks < 0 || ticks > Define.BattleTickLimit)
            {
                return false;
            }

            Outcome = result;
            TotalTicks += ticks;
            _hasCompletedWave = true;
            if (result == Define.BattleResult.Victory)
                ClearedWaves++;

            return true;
        }

        /// <summary>
        /// 上限未満の種類を ID 順に並べ、部分 Fisher–Yates で最大三候補を一度だけ作ります。
        /// [KO] 상한 미만 종류를 ID 순으로 정렬하고 부분 Fisher–Yates로 최대 세 후보를 한 번만 만듭니다.
        /// [EN] Orders eligible kinds by ID and draws up to three offers once with partial Fisher–Yates.
        /// </summary>
        public IReadOnlyList<Define.RewardKind> GetOffers()
        {
            if (!_hasCompletedWave || IsFinished)
                throw new InvalidOperationException("Offers require an intermediate victorious wave.");

            if (_offers != null)
                return _offers;

            var candidates = new List<Define.RewardKind>(6);
            for (var index = 0; index < 6; index++)
            {
                var kind = (Define.RewardKind)index;
                if (Rewards.GetStacks(kind) < Data.GetReward(kind).MaxStacks)
                    candidates.Add(kind);
            }

            candidates.Sort((left, right) => string.CompareOrdinal(RewardData.GetId(left), RewardData.GetId(right)));
            var offers = new Define.RewardKind[Math.Min(3, candidates.Count)];
            for (var index = 0; index < offers.Length; index++)
            {
                var chosen = index + _random.Next(candidates.Count - index);
                (candidates[index], candidates[chosen]) = (candidates[chosen], candidates[index]);
                offers[index] = candidates[index];
            }

            _offers = Array.AsReadOnly(offers);
            return _offers;
        }

        /// <summary>
        /// 提示済み候補だけを一度適用し、次のウェーブへ進めます。null は候補ゼロ時のみ有効です。
        /// [KO] 제시한 후보만 한 번 적용하고 다음 웨이브로 진행합니다. null은 후보가 없을 때만 유효합니다.
        /// [EN] Applies only an offered choice once and advances; null is valid only for an empty offer.
        /// </summary>
        public bool TryChoose(int wave, Define.RewardKind? kind)
        {
            if (wave != Wave || !_hasCompletedWave || IsFinished || _offers == null)
                return false;

            if (kind.HasValue)
            {
                var isOffered = false;
                foreach (var offered in _offers)
                    isOffered |= offered == kind.Value;

                if (!isOffered)
                    return false;

                var reward = Data.GetReward(kind.Value);
                Rewards = Rewards.Add(new RewardChoice(Wave, kind.Value, reward.GetIncrease(Data.GetWave(Wave).RewardStrengthBp)));
            }
            else if (_offers.Count != 0)
            {
                return false;
            }

            Wave++;
            _offers = null;
            _hasCompletedWave = false;
            Outcome = Define.BattleResult.Ongoing;
            return true;
        }

        /// <summary>
        /// Run 終了時だけ、画面や再現検査に渡す結果を作ります。
        /// [KO] Run 종료 시에만 화면·재현 검사에 전달할 결과를 만듭니다.
        /// [EN] Creates a result for display and replay checking only when the run has ended.
        /// </summary>
        public RunResult CreateResult(Guid runId, RunConfiguration configuration)
            => !IsFinished ? throw new InvalidOperationException("The run is still in progress.") :
                new RunResult(runId, configuration, Outcome, TotalTicks, ClearedWaves, Wave, Rewards);
    }
}
