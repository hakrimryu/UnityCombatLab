using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace UnityCombatLab.Infrastructure.Assets
{
    /// <summary>
    /// 型付き参照の読込と失敗時の解放を共通化します。成功した資産は呼出側の所有期間まで保持します。
    /// [KO] 타입 지정 참조의 로딩과 실패 시 해제를 공통 처리하며 성공한 자산은 호출자의 소유 기간까지 유지합니다.
    /// [EN] Centralizes typed loading and failure cleanup; successful assets live until their caller releases them.
    /// </summary>
    public sealed class AddressableAssetManager
    {
        /// <summary>
        /// 画面またはRunが一括解放する資産の所有範囲を作ります。
        /// [KO] 화면 또는 Run이 일괄 해제할 자산 소유 범위를 만듭니다.
        /// [EN] Creates an asset scope for bulk release by a screen or run.
        /// </summary>
        public AddressableAssetScope CreateScope()
            => new(this);

        /// <summary>
        /// 内部ロード完了後に取消を確認し、成功時だけ解放責任を呼出側へ渡します。Unityのメインスレッドから呼びます。
        /// [KO] 내부 로드 완료 후 취소를 확인하고 성공할 때만 해제 책임을 호출자에게 넘깁니다. Unity 메인 스레드에서 호출합니다.
        /// [EN] Checks cancellation after internal completion and transfers release ownership only on success. Call on Unity's main thread.
        /// </summary>
        public async UniTask<AddressableAssetLease<T>> LoadAsync<T>(AssetReferenceT<T> reference,
            CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            if (!PlayerLoopHelper.IsMainThread)
                throw new InvalidOperationException("Addressable assets must be loaded on Unity's main thread.");

            if (reference == null || !reference.RuntimeKeyIsValid())
                throw new ArgumentException("A valid typed asset reference is required.", nameof(reference));

            cancellationToken.ThrowIfCancellationRequested();
            // AssetReference 自身にhandleを持たせず、各呼出しが独立した参照カウントを所有する。
            var handle = Addressables.LoadAssetAsync<T>(reference.RuntimeKey);
            try
            {
                var asset = await handle;
                cancellationToken.ThrowIfCancellationRequested();
                if (asset == null)
                    throw new InvalidOperationException($"Asset {reference.RuntimeKey} did not produce {typeof(T).Name}.");

                return new AddressableAssetLease<T>(handle);
            }
            catch
            {
                // 取消でも内部完了を待つので、この経路だけで失敗したhandleを解放できる。
                if (handle.IsValid())
                    Addressables.Release(handle);

                throw;
            }
        }
    }
}
