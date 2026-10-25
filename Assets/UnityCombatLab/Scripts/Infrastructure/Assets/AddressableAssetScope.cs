using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

namespace UnityCombatLab.Infrastructure.Assets
{
    /// <summary>
    /// 一つの画面やRunが使う型の異なる資産を所有し、終了後にまとめて解放します。
    /// [KO] 한 화면이나 Run이 사용하는 여러 타입의 자산을 소유하고 종료 후 함께 해제합니다.
    /// [EN] Owns assets of different types used by one screen or run and releases them together on exit.
    /// </summary>
    public sealed class AddressableAssetScope : IDisposable
    {
        /// <summary>
        /// 実際の読込と失敗処理を受け持つ共有サービス。
        /// [KO] 실제 로딩과 실패 처리를 맡는 공통 서비스입니다.
        /// [EN] Shared service responsible for loading and failures.
        /// </summary>
        private readonly AddressableAssetManager _assets;

        /// <summary>
        /// 成功した読込だけを取得順に保持します。
        /// [KO] 성공한 로딩만 취득 순서대로 보관합니다.
        /// [EN] Successful loads retained in acquisition order.
        /// </summary>
        private readonly List<IDisposable> _leases = new();

        /// <summary>
        /// 破棄中に完了した読込の結果を受け取らないための終了状態。
        /// [KO] 파괴 중 완료한 로딩 결과를 받지 않기 위한 종료 상태입니다.
        /// [EN] End state rejecting results arriving during disposal.
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// 共通サービスを使用する新しい所有範囲を作ります。
        /// [KO] 공통 서비스를 사용할 새 소유 범위를 만듭니다.
        /// [EN] Creates a new ownership scope using the shared service.
        /// </summary>
        internal AddressableAssetScope(AddressableAssetManager assets)
            => _assets = assets;

        /// <summary>
        /// 呼出しごとの読込を所有します。同じ参照を再度読む場合も対応する解放を一つ保持します。
        /// [KO] 호출마다 로딩을 소유하며 같은 참조를 다시 읽어도 대응하는 해제 하나를 보관합니다.
        /// [EN] Owns each load, retaining one matching release even when the same reference is requested again.
        /// </summary>
        public async UniTask<T> LoadAsync<T>(AssetReferenceT<T> reference, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(AddressableAssetScope));

            var lease = await _assets.LoadAsync(reference, cancellationToken);
            if (_isDisposed)
            {
                lease.Dispose();
                throw new ObjectDisposedException(nameof(AddressableAssetScope));
            }

            _leases.Add(lease);
            return lease.Asset;
        }

        /// <summary>
        /// 表示の破棄後に、取得と逆の順で全資産を解放します。遅い読込は戻った時点で解放します。
        /// [KO] 표시 파괴 후 취득 역순으로 모든 자산을 해제하며 늦은 로딩은 반환 시점에 해제합니다.
        /// [EN] Releases assets in reverse order after view destruction; late loads release when they return.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            for (var index = _leases.Count - 1; index >= 0; index--)
                _leases[index].Dispose();

            _leases.Clear();
        }
    }
}
