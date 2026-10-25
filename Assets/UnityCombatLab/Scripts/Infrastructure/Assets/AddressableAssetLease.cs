using System;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UnityCombatLab.Infrastructure.Assets
{
    /// <summary>
    /// 読込済み資産の参照カウント一つを所有します。生成した表示を破棄してから解放します。
    /// [KO] 로드된 자산 참조 카운트 하나를 소유합니다. 생성한 표시를 파괴한 후 해제합니다.
    /// [EN] Owns one reference count for a loaded asset. Release after destroying its generated views.
    /// </summary>
    public sealed class AddressableAssetLease<T> : IDisposable where T : UnityEngine.Object
    {
        /// <summary>
        /// この所有者だけが解放する読込結果。
        /// [KO] 이 소유자만 해제할 로딩 결과입니다.
        /// [EN] Load result released exclusively by this owner.
        /// </summary>
        private AsyncOperationHandle<T> _handle;

        /// <summary>
        /// 二重解放と解放後の参照を拒否します。
        /// [KO] 중복 해제와 해제 후 참조를 거부합니다.
        /// [EN] Prevents duplicate release and access after disposal.
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// 所有期間中だけ使用できる原型または画像。
        /// [KO] 소유 기간에만 사용할 원형 또는 이미지입니다.
        /// [EN] Prefab or image usable only during ownership.
        /// </summary>
        public T Asset => _isDisposed ? throw new ObjectDisposedException(nameof(AddressableAssetLease<T>)) : _handle.Result;

        /// <summary>
        /// 共通ローダーが成功したhandleの所有権を渡します。
        /// [KO] 공통 로더가 성공한 handle 소유권을 전달합니다.
        /// [EN] Accepts ownership of a successful handle from the shared loader.
        /// </summary>
        internal AddressableAssetLease(AsyncOperationHandle<T> handle)
            => _handle = handle;

        /// <summary>
        /// この読込の参照カウントだけを一度減らします。
        /// [KO] 이 로딩의 참조 카운트만 한 번 줄입니다.
        /// [EN] Decrements this load's reference count exactly once.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            if (_handle.IsValid())
                Addressables.Release(_handle);

            _handle = default;
        }
    }
}
