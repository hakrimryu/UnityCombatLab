using System;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace UnityCombatLab.Tests.PlayMode
{
    /// <summary>
    /// テストが成功・失敗の時刻を決め、実際のAddressablesから解放通知を受ける供給元です。
    /// [KO] 테스트가 성공·실패 시점을 정하고 실제 Addressables에서 해제 알림을 받는 공급자입니다.
    /// [EN] Provider whose completion is controlled by tests and whose releases come from real Addressables.
    /// </summary>
    internal sealed class ControlledAssetProvider : ResourceProviderBase
    {
        /// <summary>
        /// テストがまだ完了させていない一件の要求。
        /// [KO] 테스트에서 아직 완료시키지 않은 요청 한 건입니다.
        /// [EN] One request awaiting completion by the test.
        /// </summary>
        private ProvideHandle _pending;

        /// <summary>
        /// キャッシュからの再利用を除いた実際の要求数。
        /// [KO] 캐시 재사용을 제외한 실제 요청 횟수입니다.
        /// [EN] Actual provider requests, excluding cache reuse.
        /// </summary>
        public int RequestCount { get; private set; }

        /// <summary>
        /// 最後の所有者が返却した資産数。
        /// [KO] 마지막 소유자가 반환한 자산 횟수입니다.
        /// [EN] Asset releases made by the final owner.
        /// </summary>
        public int ReleaseCount { get; private set; }

        /// <summary>
        /// 要求を保持し、テストが取消や破棄を先に実行できるようにします。
        /// [KO] 요청을 보관하여 테스트가 취소나 파괴를 먼저 실행할 수 있게 합니다.
        /// [EN] Retains a request so the test can cancel or dispose before completion.
        /// </summary>
        public override void Provide(ProvideHandle provideHandle)
        {
            _pending = provideHandle;
            RequestCount++;
        }

        /// <summary>
        /// 保持した要求を成功または指定した失敗で完了します。
        /// [KO] 보관한 요청을 성공 또는 지정한 실패로 완료합니다.
        /// [EN] Completes the retained request successfully or with the supplied failure.
        /// </summary>
        public void Complete(GameObject asset, Exception failure = null)
            => _pending.Complete(asset, failure == null, failure);

        /// <summary>
        /// ResourceManagerが所有者数をゼロにした通知を記録します。
        /// [KO] ResourceManager가 소유자 수를 0으로 만든 알림을 기록합니다.
        /// [EN] Records ResourceManager notifying that the owner count reached zero.
        /// </summary>
        public override void Release(IResourceLocation location, object asset)
            => ReleaseCount++;
    }
}
