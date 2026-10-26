using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityCombatLab.Application.Ports;
using UnityCombatLab.Common;
using UnityEngine;
using UnityCombatLab.Domain.Run;
using UnityCombatLab.Domain.Battle;
using UnityCombatLab.Infrastructure.Balance;

namespace UnityCombatLab.Infrastructure.Assets
{
    /// <summary>
    /// 一回のRunが使うHUD・能力設定・キャラクターを準備し、表示破棄後に解放します。
    /// [KO] 한 번의 Run이 사용할 HUD·능력 설정·캐릭터를 준비하고 표시 파괴 후 해제합니다.
    /// [EN] Prepares one run's HUD, stats and characters and releases them after view destruction.
    /// </summary>
    public sealed class RunAssetScope : IRunResources
    {
        /// <summary>
        /// Run専用の資産所有範囲と型付きUIカタログ。
        /// [KO] Run 전용 자산 소유 범위와 타입 지정 UI 카탈로그입니다.
        /// [EN] Run-specific asset ownership scope and typed UI catalog.
        /// </summary>
        private readonly AddressableAssetScope _scope;
        /// <summary>
        /// HUDと職業画像の型付き参照元。
        /// [KO] HUD와 직업 이미지의 타입 지정 참조 원본입니다.
        /// [EN] Source of typed HUD and role-image references.
        /// </summary>
        private readonly UiAssetCatalog _catalog;

        /// <summary>
        /// 編成画面と共有する職業別の能力設定・表示参照。
        /// [KO] 편성 화면과 공유하는 직업별 능력 설정·표시 참조입니다.
        /// [EN] Role-specific stat and visual references shared with formation.
        /// </summary>
        private readonly UnitAssetCatalog _unitCatalog;

        /// <summary>
        /// Run 開始時にコピーするウェーブと強化の設定。
        /// [KO] Run 시작 시 복사할 웨이브·강화 설정입니다.
        /// [EN] Wave and upgrade settings copied at run start.
        /// </summary>
        private readonly RunDefinition _runDefinition;

        /// <summary>
        /// この Run の五ウェーブに共通する検査済み入力。
        /// [KO] 이 Run의 다섯 웨이브가 공유하는 검사된 입력입니다.
        /// [EN] Validated input shared across this run's five waves.
        /// </summary>
        public RunGameData GameData { get; private set; }

        /// <summary>
        /// この Run が所有する操作 UI のロード済み prefab。
        /// [KO] 이 Run이 소유하는 조작 UI의 로드한 prefab입니다.
        /// [EN] Loaded controls prefab owned by this run.
        /// </summary>
        public GameObject RunBattlePrefab { get; private set; }

        /// <summary>
        /// このRunが保持し、Dungeonの生成処理へ貸すロード済み資産。
        /// [KO] 이번 Run에서 유지하고 Dungeon 생성 처리에 빌려줄 로드된 자산입니다.
        /// [EN] Loaded assets retained by this run and lent to Dungeon creation.
        /// </summary>
        private readonly Dictionary<Define.UnitRole, LoadedUnitAsset> _units = new();

        /// <summary>
        /// 職業からHUD画像を選ぶ読込結果。Run終了時に参照を消します。
        /// [KO] 직업으로 HUD 이미지를 선택할 로딩 결과이며 Run 종료 시 참조를 지웁니다.
        /// [EN] Loaded role-to-HUD images, cleared when the run ends.
        /// </summary>
        private readonly Dictionary<Define.UnitRole, Sprite> _icons = new();

        /// <summary>
        /// HUD Presenterへ渡す職業別画像。
        /// [KO] HUD Presenter에 전달할 직업별 이미지입니다.
        /// [EN] Images by role supplied to the HUD presenter.
        /// </summary>
        public IReadOnlyDictionary<Define.UnitRole, Sprite> Icons => _icons;

        /// <summary>
        /// 同じRunでの重複準備と破棄後の再開を防ぎます。
        /// [KO] 같은 Run에서 중복 준비와 파괴 후 재개를 막습니다.
        /// [EN] Prevents repeated preparation and resuming after disposal.
        /// </summary>
        private bool _isPrepared;
        /// <summary>
        /// 終了後の再利用と二重解放を防ぎます。
        /// [KO] 종료 후 재사용과 중복 해제를 막습니다.
        /// [EN] Prevents reuse and duplicate release after exit.
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// Dungeonの構成処理に渡す読込済みHUD。
        /// [KO] Dungeon 구성 처리에 전달할 로드된 HUD입니다.
        /// [EN] Loaded HUD supplied to Dungeon composition.
        /// </summary>
        public GameObject HudPrefab { get; private set; }

        /// <summary>
        /// App Scopeの共通サービスから、このRunだけの所有範囲を作ります。
        /// [KO] App Scope의 공통 서비스에서 이번 Run만의 소유 범위를 만듭니다.
        /// [EN] Creates this run's ownership scope from the app's shared service.
        /// </summary>
        public RunAssetScope(AddressableAssetManager assets, UiAssetCatalog catalog, UnitAssetCatalog unitCatalog, RunDefinition runDefinition)
        {
            _scope = (assets ?? throw new ArgumentNullException(nameof(assets))).CreateScope();
            _catalog = catalog;
            _unitCatalog = unitCatalog;
            _runDefinition = runDefinition;
        }

        /// <summary>
        /// Dungeonへ移る前に必要な資産を読み込み、失敗時は取得済み資産も解放します。
        /// [KO] Dungeon으로 이동하기 전에 필요한 자산을 로드하며 실패 시 취득한 자산도 해제합니다.
        /// [EN] Loads required assets before entering Dungeon and releases acquired assets on failure.
        /// </summary>
        public async UniTask PrepareAsync(CancellationToken cancellationToken)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(RunAssetScope));

            if (_isPrepared)
                throw new InvalidOperationException("This run has already started preparing assets.");

            _isPrepared = true;
            try
            {
                if (_catalog == null)
                    throw new InvalidOperationException("Run requires a UI asset catalog.");

                if (_unitCatalog == null)
                    throw new InvalidOperationException("Run requires a unit asset catalog.");

                var hud = await _scope.LoadAsync(_catalog.BattleHud, cancellationToken);
                var runBattle = await _scope.LoadAsync(_catalog.RunBattle, cancellationToken);
                foreach (Define.UnitRole role in Enum.GetValues(typeof(Define.UnitRole)))
                {
                    _icons.Add(role, await _scope.LoadAsync(_catalog.GetBattleIcon(role), cancellationToken));
                    var reference = _unitCatalog.Get(role);
                    var definition = await _scope.LoadAsync(reference.Definition, cancellationToken);
                    if (definition.Role != role)
                        throw new InvalidOperationException($"Unit definition does not match catalog role {role}.");

                    // prefabの依存先にある効果とモデルも、このハンドルが解放されるまで保持される。
                    var prefab = await _scope.LoadAsync(reference.Prefab, cancellationToken);
                    _units.Add(role, new LoadedUnitAsset(definition, prefab));
                }

                HudPrefab = hud;
                RunBattlePrefab = runBattle;
                if (_runDefinition == null)
                    throw new InvalidOperationException("Run requires wave and upgrade definitions.");

                var roles = new BattleUnitInput[6];
                for (var index = 0; index < roles.Length; index++)
                {
                    var role = (Define.UnitRole)index;
                    roles[index] = _units[role].Definition.CreateInput(index + 1,
                        index < Define.SlotsPerTeam ? Define.BattleTeam.Ally : Define.BattleTeam.Enemy,
                        (Define.BattleSlot)(index % Define.SlotsPerTeam));
                }

                GameData = _runDefinition.CreateSnapshot(roles);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// 準備済みの職業資産を返します。終了後の生成要求は拒否します。
        /// [KO] 준비된 직업 자산을 반환하며 종료 후 생성 요청은 거부합니다.
        /// [EN] Returns prepared role assets and rejects creation requests after disposal.
        /// </summary>
        public LoadedUnitAsset GetUnit(Define.UnitRole role)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(RunAssetScope));

            if (HudPrefab == null || !_units.TryGetValue(role, out var unit))
                throw new InvalidOperationException($"Run assets for {role} are not ready.");

            return unit;
        }

        /// <summary>
        /// Run調整役がHUD・ユニット・効果の破棄後に呼び、Runの資産を解放します。
        /// [KO] Run 조정자가 HUD·유닛·효과 파괴 후 호출하여 Run 자산을 해제합니다.
        /// [EN] Called by run coordination after HUD, unit and effect destruction to release run assets.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            HudPrefab = null;
            RunBattlePrefab = null;
            GameData = null;
            _icons.Clear();
            _units.Clear();
            _scope.Dispose();
        }
    }
}
