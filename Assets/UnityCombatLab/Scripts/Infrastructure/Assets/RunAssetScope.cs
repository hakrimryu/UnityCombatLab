using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityCombatLab.Application.Ports;
using UnityCombatLab.Common;
using UnityEngine;

namespace UnityCombatLab.Infrastructure.Assets
{
    /// <summary>
    /// 一回のRunが使うHUDを共通ローダーで準備し、表示破棄後に解放します。
    /// [KO] 한 번의 Run이 사용할 HUD를 공통 로더로 준비하고 표시 파괴 후 해제합니다.
    /// [EN] Prepares one run's HUD through the shared loader and releases it after view destruction.
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
        public RunAssetScope(AddressableAssetManager assets, UiAssetCatalog catalog)
        {
            _scope = (assets ?? throw new ArgumentNullException(nameof(assets))).CreateScope();
            _catalog = catalog;
        }

        /// <summary>
        /// カタログのHUDを一度読み込みます。失敗時は取得済み資産も解放します。
        /// [KO] 카탈로그의 HUD를 한 번 로드하며 실패 시 취득한 자산도 해제합니다.
        /// [EN] Loads the catalog's HUD once and releases acquired assets on failure.
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

                var hud = await _scope.LoadAsync(_catalog.BattleHud, cancellationToken);
                foreach (Define.UnitRole role in Enum.GetValues(typeof(Define.UnitRole)))
                    _icons.Add(role, await _scope.LoadAsync(_catalog.GetBattleIcon(role), cancellationToken));

                HudPrefab = hud;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Run調整役がHUD破棄後に呼び、Runの資産だけを解放します。
        /// [KO] Run 조정자가 HUD 파괴 후 호출하여 Run 자산만 해제합니다.
        /// [EN] Called by run coordination after HUD destruction to release only this run's assets.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            HudPrefab = null;
            _icons.Clear();
            _scope.Dispose();
        }
    }
}
