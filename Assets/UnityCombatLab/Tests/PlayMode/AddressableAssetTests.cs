using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityCombatLab.Infrastructure.Assets;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace UnityCombatLab.Tests.PlayMode
{
    /// <summary>
    /// 実際のAddressables操作を任意の時点で完了させ、共通ローダーの解放責任を検証します。
    /// [KO] 실제 Addressables 작업을 원하는 시점에 완료시켜 공통 로더의 해제 책임을 검증합니다.
    /// [EN] Completes real Addressables operations at controlled times to verify shared-loader ownership.
    /// </summary>
    public sealed class AddressableAssetTests
    {
        /// <summary>
        /// このテストだけが要求する一時キー。
        /// [KO] 이번 테스트만 요청하는 임시 키입니다.
        /// [EN] Temporary key requested only by this test.
        /// </summary>
        private AssetReferenceGameObject _reference;
        /// <summary>
        /// 一時キーを供給元へ接続する登録。
        /// [KO] 임시 키를 공급자에 연결하는 등록입니다.
        /// [EN] Registration connecting the temporary key to its provider.
        /// </summary>
        private ResourceLocationMap _locator;
        /// <summary>
        /// 完了時刻と解放通知を確認する供給元。
        /// [KO] 완료 시점과 해제 알림을 확인하는 공급자입니다.
        /// [EN] Provider used to control completion and observe release.
        /// </summary>
        private ControlledAssetProvider _provider;
        /// <summary>
        /// 保存せずテスト内でだけ返すオブジェクト。
        /// [KO] 저장하지 않고 테스트 안에서만 반환할 오브젝트입니다.
        /// [EN] Unsaved object returned only within the test.
        /// </summary>
        private GameObject _asset;

        /// <summary>
        /// 保存済みカタログに触れず、一時キーを実際のResourceManagerへ登録します。
        /// [KO] 저장된 카탈로그를 건드리지 않고 임시 키를 실제 ResourceManager에 등록합니다.
        /// [EN] Registers a temporary key with the real ResourceManager without editing saved catalogs.
        /// </summary>
        [UnitySetUp]
        public IEnumerator RegisterProvider()
        {
            yield return Addressables.InitializeAsync().ToUniTask().ToCoroutine();
            var key = Guid.NewGuid().ToString("N");
            _reference = new AssetReferenceGameObject(key);
            _provider = new ControlledAssetProvider();
            // ResourceManagerは供給元IDの検索結果を保持するため、テスト間でも異なるIDを使う。
            _provider.Initialize(key, null);
            _asset = new GameObject("AddressableTestAsset");
            _locator = new ResourceLocationMap(key);
            _locator.Add(key, new ResourceLocationBase(key, key, _provider.ProviderId, typeof(GameObject)));
            Addressables.ResourceManager.ResourceProviders.Add(_provider);
            Addressables.AddResourceLocator(_locator);
        }

        /// <summary>
        /// 一時キーと供給元を解除し、他のテストへ持ち越しません。
        /// [KO] 임시 키와 공급자를 제거하여 다른 테스트로 남기지 않습니다.
        /// [EN] Removes temporary keys and providers so they do not affect other tests.
        /// </summary>
        [TearDown]
        public void RemoveProvider()
        {
            Addressables.RemoveResourceLocator(_locator);
            Addressables.ResourceManager.ResourceProviders.Remove(_provider);
            Object.Destroy(_asset);
        }

        /// <summary>
        /// 同じ資産の二つの所有者は、最後の解放まで資産を維持します。
        /// [KO] 같은 자산의 두 소유자는 마지막 해제까지 자산을 유지합니다.
        /// [EN] Two owners of one asset retain it until the final release.
        /// </summary>
        [UnityTest]
        public IEnumerator SharedLoadsReleaseOnlyAfterBothOwnersDispose()
            => UniTask.ToCoroutine(async () =>
            {
                var manager = new AddressableAssetManager();
                var firstLoad = manager.LoadAsync(_reference);
                var secondLoad = manager.LoadAsync(_reference);
                Assert.That(_provider.RequestCount, Is.EqualTo(1));
                _provider.Complete(_asset);
                using var first = await firstLoad;
                using var second = await secondLoad;
                Assert.That(first.Asset, Is.SameAs(second.Asset));
                first.Dispose();
                first.Dispose();
                Assert.That(_provider.ReleaseCount, Is.Zero);
                Assert.Throws<ObjectDisposedException>(() => _ = first.Asset);
                Assert.That(second.Asset, Is.SameAs(_asset));
                second.Dispose();
                second.Dispose();
                Assert.That(_provider.ReleaseCount, Is.EqualTo(1));
            });

        /// <summary>
        /// 開始前の取消はロードせず、開始後の取消は内部完了を待って一度だけ解放します。
        /// [KO] 시작 전 취소는 로드하지 않고 시작 후 취소는 내부 완료를 기다려 한 번만 해제합니다.
        /// [EN] Pre-cancellation avoids loading; in-flight cancellation waits for completion and releases once.
        /// </summary>
        [UnityTest]
        public IEnumerator CancellationWaitsForProviderAndReleasesResult()
            => UniTask.ToCoroutine(async () =>
            {
                var manager = new AddressableAssetManager();
                using var cancelled = new CancellationTokenSource();
                cancelled.Cancel();
                var wasCancelled = await manager.LoadAsync(_reference, cancelled.Token).SuppressCancellationThrow();
                Assert.That(wasCancelled.IsCanceled, Is.True);
                Assert.That(_provider.RequestCount, Is.Zero);
                using var pending = new CancellationTokenSource();
                var loading = manager.LoadAsync(_reference, pending.Token);
                pending.Cancel();
                Assert.That(loading.Status, Is.EqualTo(UniTaskStatus.Pending));
                Assert.That(_provider.ReleaseCount, Is.Zero);
                _provider.Complete(_asset);
                var result = await loading.SuppressCancellationThrow();
                Assert.That(result.IsCanceled, Is.True);
                Assert.That(_provider.ReleaseCount, Is.EqualTo(1));
            });

        /// <summary>
        /// 画面の終了後に届いたロード結果は、終了済みの所有範囲へ保存せず解放します。
        /// [KO] 화면 종료 후 도착한 로딩 결과를 종료된 소유 범위에 저장하지 않고 해제합니다.
        /// [EN] Releases a late load instead of retaining it in a disposed screen scope.
        /// </summary>
        [UnityTest]
        public IEnumerator DisposedScopeReleasesLateLoad()
            => UniTask.ToCoroutine(async () =>
            {
                using var scope = new AddressableAssetManager().CreateScope();
                var loading = scope.LoadAsync(_reference);
                scope.Dispose();
                Assert.That(_provider.ReleaseCount, Is.Zero);
                _provider.Complete(_asset);
                Exception failure = null;
                try
                {
                    await loading;
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                Assert.That(failure, Is.TypeOf<ObjectDisposedException>());
                Assert.That(_provider.ReleaseCount, Is.EqualTo(1));
                scope.Dispose();
                Assert.That(_provider.ReleaseCount, Is.EqualTo(1));
            });

        /// <summary>
        /// 供給元の失敗を呼出元へ返し、同じキーの再試行で新しい操作を生成します。
        /// [KO] 공급자 실패를 호출자에게 반환하고 같은 키 재시도 시 새 작업을 생성합니다.
        /// [EN] Returns provider failures and creates a fresh operation when retrying the same key.
        /// </summary>
        [UnityTest]
        public IEnumerator FailedHandleIsReleasedBeforeRetry()
            => UniTask.ToCoroutine(async () =>
            {
                var manager = new AddressableAssetManager();
                var expected = new InvalidOperationException("Controlled asset load failure");
                var originalHandler = ResourceManager.ExceptionHandler;
                ResourceManager.ExceptionHandler = (handle, exception) =>
                {
                    // 注入した失敗だけを捕捉し、無関係の例外は通常の記録先へ渡す。
                    if (exception != expected)
                        originalHandler?.Invoke(handle, exception);
                };
                try
                {
                    var loading = manager.LoadAsync(_reference);
                    _provider.Complete(null, expected);
                    Exception failure = null;
                    try
                    {
                        await loading;
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }
                    Assert.That(failure, Is.SameAs(expected));
                    var retry = manager.LoadAsync(_reference);
                    Assert.That(_provider.RequestCount, Is.EqualTo(2), "A failed cached handle must not block retry.");
                    _provider.Complete(_asset);
                    using var lease = await retry;
                    Assert.That(lease.Asset, Is.SameAs(_asset));
                    lease.Dispose();
                    Assert.That(_provider.ReleaseCount, Is.EqualTo(1));
                }
                finally
                {
                    ResourceManager.ExceptionHandler = originalHandler;
                }
            });
    }
}
