using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// 紙吹雪 ParticleSystem の位置・色・大きさを、Overlay Canvas 上の四角形として描きます。
    /// [KO] 색종이 ParticleSystem의 위치·색·크기를 Overlay Canvas 위 사각형으로 그립니다.
    /// [EN] Draws confetti particle positions, colors and sizes as quads on an overlay canvas.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiConfettiView : MaskableGraphic
    {
        /// <summary>
        /// 勝利イラストから紙吹雪を放つ Local 空間の ParticleSystem。
        /// [KO] 승리 일러스트에서 색종이를 방출할 Local 공간 ParticleSystem입니다.
        /// [EN] Local-space particle systems emitting confetti from the victory illustration.
        /// </summary>
        [SerializeField] private ParticleSystem[] _emitters;

        /// <summary>
        /// 色と形が 3×3 に並ぶ購入元の紙吹雪テクスチャ。
        /// [KO] 색과 모양이 3×3으로 배치된 원본 색종이 텍스처입니다.
        /// [EN] Source confetti texture containing a three-by-three grid of colors and shapes.
        /// </summary>
        [SerializeField] private Texture _texture;

        /// <summary>
        /// 同じ配列を各放出元の粒子取得に繰り返し使います。
        /// [KO] 각 방출원의 입자를 가져올 때 반복해서 사용할 배열입니다.
        /// [EN] Buffer reused to retrieve particles from each emitter.
        /// </summary>
        private ParticleSystem.Particle[] _particles;

        /// <summary>
        /// 粒子が残っている間だけ描画を更新するフラグ。
        /// [KO] 입자가 남아 있는 동안만 그리기를 갱신할 플래그입니다.
        /// [EN] Whether drawing should update while particles remain alive.
        /// </summary>
        private bool _isPlaying;

        /// <summary>
        /// 次の UI 更新で放出元を先頭から進めるかどうか。
        /// [KO] 다음 UI 갱신에서 방출원을 처음부터 진행할지 여부입니다.
        /// [EN] Whether to advance the emitters from the beginning on the next UI update.
        /// </summary>
        private bool _shouldRestart;

        /// <summary>
        /// UI の標準材質で描く紙吹雪テクスチャ。
        /// [KO] UI 표준 재질로 그릴 색종이 텍스처입니다.
        /// [EN] Confetti texture drawn with the standard UI material.
        /// </summary>
        public override Texture mainTexture => _texture != null ? _texture : base.mainTexture;

        /// <summary>
        /// UI の描画を登録し、編集中は標準パーティクル再生の表示も更新します。
        /// [KO] UI 그리기를 등록하고 편집 중에는 기본 파티클 재생의 표시도 갱신합니다.
        /// [EN] Registers UI rendering and refreshes standard particle playback while editing.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= RefreshEditorPreview;
            if (UnityEngine.Application.isPlaying || !gameObject.scene.IsValid()) return;
            UnityEditor.EditorApplication.update += RefreshEditorPreview;
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// 標準の再生操作で動く粒子を Canvas 上へ描き、背景より前に表示します。
        /// [KO] 기본 재생 조작으로 움직이는 입자를 Canvas에 그려 배경보다 앞에 표시합니다.
        /// [EN] Draws particles driven by standard playback on the canvas in front of its background.
        /// </summary>
        private void RefreshEditorPreview()
        {
            if (this == null || UnityEngine.Application.isPlaying || _emitters == null) return;
            var hasCanvas = canvas != null;
            var hasParticles = false;
            foreach (var emitter in _emitters)
            {
                if (emitter == null) continue;
                var particleRenderer = emitter.GetComponent<ParticleSystemRenderer>();
                if (particleRenderer != null) particleRenderer.forceRenderingOff = hasCanvas;
                hasParticles |= emitter.particleCount > 0;
            }

            hasParticles &= hasCanvas;
            if (!hasParticles && !_isPlaying) return;
            if (hasParticles) EnsureInitialized();
            _isPlaying = hasParticles;
            SetVerticesDirty();
            UnityEditor.SceneView.RepaintAll();
        }
#endif

        /// <summary>
        /// 最初の再生で参照を確認し、最大粒子数分の取得領域を確保します。
        /// [KO] 첫 재생 시 참조를 확인하고 최대 입자 수만큼 가져오기 영역을 확보합니다.
        /// [EN] Checks references and allocates retrieval space for the maximum particle count on first play.
        /// </summary>
        private void EnsureInitialized()
        {
            if (_particles != null) return;
            if (_texture == null || _emitters == null || _emitters.Length == 0)
                throw new InvalidOperationException("UI confetti requires a texture and particle emitters.");
            var capacity = 0;
            foreach (var emitter in _emitters)
            {
                if (emitter == null || emitter.main.simulationSpace != ParticleSystemSimulationSpace.Local)
                    throw new InvalidOperationException("UI confetti emitters must use local simulation space.");
                capacity = Mathf.Max(capacity, emitter.main.maxParticles);
            }

            _particles = new ParticleSystem.Particle[capacity];
        }

        /// <summary>
        /// 残った粒子を消し、次の UI 更新で放出を開始します。ゲーム中は Canvas で描きます。
        /// [KO] 남은 입자를 지우고 다음 UI 갱신에서 방출을 시작하며 게임 중에는 Canvas로 그립니다.
        /// [EN] Clears remaining particles and schedules emission for the next UI update, drawing through the canvas in game.
        /// </summary>
        public void Play()
        {
            EnsureInitialized();
            Clear();
            if (UnityEngine.Application.isPlaying)
                foreach (var emitter in _emitters)
                    emitter.GetComponent<ParticleSystemRenderer>().enabled = false;
            _shouldRestart = true;
            _isPlaying = true;
            SetVerticesDirty();
        }

        /// <summary>
        /// 全放出元を停止して残った粒子を消します。
        /// [KO] 모든 방출원을 정지하고 남은 입자를 지웁니다.
        /// [EN] Stops all emitters and clears remaining particles.
        /// </summary>
        public void Clear()
        {
            _isPlaying = false;
            _shouldRestart = false;
            if (_emitters != null)
                foreach (var emitter in _emitters)
                    if (emitter != null)
                        emitter.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetVerticesDirty();
        }

        /// <summary>
        /// UI の時間で粒子を進めてから描画を更新し、全粒子の終了後は停止します。
        /// [KO] UI 시간으로 입자를 진행한 뒤 그리기를 갱신하고 모든 입자가 끝나면 정지합니다.
        /// [EN] Advances particles with UI time before rebuilding their graphics and stops when all particles finish.
        /// </summary>
        private void LateUpdate()
        {
            if (!UnityEngine.Application.isPlaying || !_isPlaying) return;
            var seconds = Time.unscaledDeltaTime;
            if (seconds <= 0f) return;
            var isAlive = false;
            foreach (var emitter in _emitters)
            {
                // 子の有効化が終わってから放出し、同じ更新で得た粒子を Canvas に渡す。
                // Simulate は進行後に一時停止するため、通常更新との二重進行は起きない。
                emitter.Simulate(seconds, false, _shouldRestart, false);
                isAlive |= emitter.particleCount > 0;
            }

            _shouldRestart = false;
            _isPlaying = isAlive;
            SetVerticesDirty();
        }

        /// <summary>
        /// 各粒子を回転した四角形へ変換し、同じ粒子には同じ色柄のタイルを割り当てます。
        /// [KO] 각 입자를 회전한 사각형으로 변환하고 같은 입자에는 같은 색·모양 타일을 배정합니다.
        /// [EN] Converts particles to rotated quads and assigns a stable color-and-shape tile to each particle.
        /// </summary>
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (!_isPlaying || _particles == null) return;
            foreach (var emitter in _emitters)
            {
                var count = emitter.GetParticles(_particles);
                // 奥行きは UI 面へ投影し、親 Canvas の順序で重ねる。
                var toCanvas = transform.worldToLocalMatrix * emitter.transform.localToWorldMatrix;
                for (var index = 0; index < count; index++)
                {
                    var particle = _particles[index];
                    var halfSize = particle.GetCurrentSize3D(emitter) * 0.5f;
                    var rotation = Quaternion.Euler(particle.rotation3D);
                    var right = rotation * new Vector3(halfSize.x, 0, 0);
                    var up = rotation * new Vector3(0, halfSize.y, 0);
                    var tint = particle.GetCurrentColor(emitter) * color;
                    // 0〜8 を 3 列のタイルへ変換する。0 は左下、3 はその一段上。
                    var tile = (int)(particle.randomSeed % 9);
                    var row = tile / 3;
                    var uvMin = new Vector2(tile % 3, row) / 3f;
                    var uvMax = uvMin + Vector2.one / 3f;
                    var first = vertices.currentVertCount;
                    vertices.AddVert((Vector2)toCanvas.MultiplyPoint3x4(particle.position - right - up), tint, uvMin);
                    vertices.AddVert((Vector2)toCanvas.MultiplyPoint3x4(particle.position - right + up), tint,
                        new Vector2(uvMin.x, uvMax.y));
                    vertices.AddVert((Vector2)toCanvas.MultiplyPoint3x4(particle.position + right + up), tint, uvMax);
                    vertices.AddVert((Vector2)toCanvas.MultiplyPoint3x4(particle.position + right - up), tint,
                        new Vector2(uvMax.x, uvMin.y));
                    vertices.AddTriangle(first, first + 1, first + 2);
                    vertices.AddTriangle(first + 2, first + 3, first);
                }
            }
        }

        /// <summary>
        /// 親パネルとともに非表示になった時、粒子と Canvas の描画を消します。
        /// [KO] 부모 패널과 함께 비활성화될 때 입자와 Canvas 그리기를 지웁니다.
        /// [EN] Clears particles and canvas rendering when hidden with the parent panel.
        /// </summary>
        protected override void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= RefreshEditorPreview;
            if (_emitters != null)
                foreach (var emitter in _emitters)
                {
                    if (emitter == null) continue;
                    var particleRenderer = emitter.GetComponent<ParticleSystemRenderer>();
                    if (particleRenderer != null) particleRenderer.forceRenderingOff = false;
                }
#endif
            Clear();
            base.OnDisable();
        }
    }
}
