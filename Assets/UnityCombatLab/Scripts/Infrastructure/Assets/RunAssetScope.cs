using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityCombatLab.Application.Ports;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UnityCombatLab.Infrastructure.Assets
{
    /// <summary>
    /// 一回の挑戦で読み込む HUD の handle を所有し、失敗・取消でも一度だけ解放します。
    /// [KO] 한 번의 도전에서 로드하는 HUD handle을 소유하고 실패·취소에도 한 번만 해제합니다.
    /// [EN] Owns one run's HUD handle and releases it once, including failure and cancellation.
    /// </summary>
    public sealed class RunAssetScope : IRunResources
    {
        /// <summary>
        /// App Scopeが各Runへ渡すHUDの登録アドレス。
        /// [KO] App Scope가 각 Run에 전달하는 HUD 등록 주소입니다.
        /// [EN] HUD address supplied to each run by App Scope.
        /// </summary>
        private readonly string _address;

        /// <summary>
        /// 準備完了後にDungeonの構成処理へ渡すHUD原型。
        /// [KO] 준비 완료 후 Dungeon 구성 처리로 넘길 HUD 원형입니다.
        /// [EN] HUD prefab handed to Dungeon composition after preparation.
        /// </summary>
        public GameObject HudPrefab { get; private set; }

        /// <summary>
        /// 今回の挑戦で読むHUDアドレスを保存します。
        /// [KO] 이번 도전에서 읽을 HUD 주소를 저장합니다.
        /// [EN] Stores the HUD address to load for this run.
        /// </summary>
        public RunAssetScope(string address)
            => _address = address;

        /// <summary>
        /// Runからの準備要求でHUDを読み込み、構成処理が参照できるよう保持します。
        /// [KO] Run의 준비 요청으로 HUD를 로드하고 구성 처리에서 참조하도록 보관합니다.
        /// [EN] Loads and retains the HUD on a run preparation request for later composition.
        /// </summary>
        public async UniTask PrepareAsync(CancellationToken cancellationToken)
            => HudPrefab = await LoadHudAsync(_address, cancellationToken);

        /// <summary>
        /// 結果を使用する前から記録する、この Scope の読み込み所有権。
        /// [KO] 결과를 사용하기 전부터 기록하는 이 Scope의 로딩 소유권입니다.
        /// [EN] Owned loading handle recorded before its result can be used.
        /// </summary>
        private AsyncOperationHandle<GameObject>? _hud;

        /// <summary>
        /// 解放後の再読み込みと二重解放を防ぎます。
        /// [KO] 해제 후 재로딩과 이중 해제를 막습니다.
        /// [EN] Prevents loading after disposal and duplicate release.
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// 内部ロードの完了を待ち、その後に取消を確認して HUD の原型を返します。
        /// [KO] 내부 로드 완료를 기다린 뒤 취소를 확인하고 HUD 원형을 반환합니다.
        /// [EN] Waits for the internal load, checks cancellation, and returns the HUD prefab.
        /// </summary>
        public async UniTask<GameObject> LoadHudAsync(string address, CancellationToken cancellationToken)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(RunAssetScope));

            if (_hud.HasValue)
                throw new InvalidOperationException("This run already owns a HUD load.");

            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("HUD address is required.", nameof(address));

            cancellationToken.ThrowIfCancellationRequested();
            var handle = Addressables.LoadAssetAsync<GameObject>(address);
            _hud = handle;
            // 内部ロードを早期取消しない。遅い完了もこの Scope に戻してから、使用可否を判定する。
            var prefab = await handle;
            cancellationToken.ThrowIfCancellationRequested();
            if (_isDisposed)
                throw new OperationCanceledException(cancellationToken);

            if (prefab == null)
                throw new InvalidOperationException("The HUD address returned no prefab.");

            return prefab;
        }

        /// <summary>
        /// HUD インスタンスの破棄後に呼び、所有 handle を解放します。未完了なら完了時に解放します。
        /// [KO] HUD 인스턴스 파괴 후 호출해 소유 handle을 해제하며 미완료이면 완료 시 해제합니다.
        /// [EN] Releases the owned handle after HUD destruction, or on completion if still loading.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            HudPrefab = null;
            if (!_hud.HasValue)
                return;

            var handle = _hud.Value;
            _hud = null;
            if (!handle.IsValid())
                return;

            if (handle.IsDone)
                Addressables.Release(handle);
            else
                handle.Completed += completed => Addressables.Release(completed);
        }
    }
}
