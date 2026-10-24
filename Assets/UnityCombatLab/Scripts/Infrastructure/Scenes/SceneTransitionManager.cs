using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityCombatLab.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityCombatLab.Infrastructure.Scenes
{
    /// <summary>
    /// アプリ共通の Single シーン切替境界を提供します。
    /// [KO] 앱 공통 Single 씬 전환 경계를 제공합니다.
    /// [EN] Provides the app-wide boundary for Single scene transitions.
    /// </summary>
    public sealed class SceneTransitionManager
    {
        /// <summary>
        /// シーン切替が進行中かを示します。
        /// [KO] 씬 전환이 진행 중인지 나타냅니다.
        /// [EN] Indicates whether a scene transition is in progress.
        /// </summary>
        public bool IsLoading { get; private set; }

        /// <summary>
        /// 現在のシーンを非同期で置き換えます。
        /// [KO] 현재 씬을 비동기로 교체합니다.
        /// [EN] Replaces the current scene asynchronously.
        /// </summary>
        /// <param name="sceneType">
        /// 遷移先を表す識別子。
        /// [KO] 전환 대상을 나타내는 식별자입니다.
        /// [EN] The identifier of the destination scene.
        /// </param>
        /// <param name="cancellationToken">
        /// 開始前は中止し、開始後は Unity の完了を待ってから通知する取消要求。
        /// [KO] 시작 전에는 중단하고 시작 후에는 Unity 완료를 기다린 뒤 알리는 취소 요청입니다.
        /// [EN] Cancels before starting, or reports cancellation after Unity finishes an active load.
        /// </param>
        /// <returns>
        /// Unity のロード完了まで続く処理。
        /// [KO] Unity 로드 완료까지 유지되는 작업입니다.
        /// [EN] The operation lasting until Unity completes the load.
        /// </returns>
        public async UniTask LoadSceneAsync(Define.SceneType sceneType, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsLoading)
            {
                throw new InvalidOperationException("A scene transition is already in progress.");
            }

            if (!Enum.IsDefined(typeof(Define.SceneType), sceneType))
            {
                throw new ArgumentOutOfRangeException(nameof(sceneType), sceneType, "Unknown destination scene.");
            }

            var buildIndex = (int)sceneType;
            if (!Application.CanStreamedLevelBeLoaded(buildIndex))
            {
                throw new InvalidOperationException("The destination scene must be enabled in the build scene list.");
            }

            IsLoading = true;
            try
            {
                // Unity のロードは取消できないため、完了前に切替ロックを解放しない。
                var operation = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
                if (operation == null)
                {
                    throw new InvalidOperationException("Unity could not start the scene transition.");
                }

                await operation;
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
