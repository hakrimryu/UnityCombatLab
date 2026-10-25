using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityCombatLab.Common;
using UnityCombatLab.Application.Ports;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityCombatLab.Infrastructure.Scenes
{
    /// <summary>
    /// 現在のシーンを Single モードで非同期に置き換えます。
    /// [KO] 현재 씬을 Single 모드로 비동기 교체합니다.
    /// [EN] Replaces the current scene asynchronously in Single mode.
    /// </summary>
    public sealed class SceneTransitionManager : ISceneTransition
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
        /// ビルド順に対応する切替先。Boot=0、Dungeon=1、Frontend=2。
        /// [KO] 빌드 순서에 대응하는 전환 대상이며 Boot=0, Dungeon=1, Frontend=2입니다.
        /// [EN] Destination matching build order: Boot=0, Dungeon=1, Frontend=2.
        /// </param>
        /// <param name="cancellationToken">
        /// 開始前は中止し、開始後は Unity の完了を待ってから通知する取消要求。
        /// [KO] 시작 전에는 중단하고 시작 후에는 Unity 완료를 기다린 뒤 알리는 취소 요청입니다.
        /// [EN] Cancels before starting, or reports cancellation after Unity finishes an active load.
        /// </param>
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
            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(buildIndex))
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
