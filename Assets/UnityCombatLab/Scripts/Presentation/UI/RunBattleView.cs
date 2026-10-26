using System;
using System.Collections.Generic;
using TMPro;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.Styling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// Dungeon のウェーブ表示・報酬・停止モーダルを表示し、入力を Presenter へ渡します。
    /// [KO] Dungeon의 웨이브 표시·보상·정지 모달을 표시하고 입력을 Presenter에 전달합니다.
    /// [EN] Displays Dungeon wave information, rewards and pause modals and forwards input to its presenter.
    /// </summary>
    public sealed class RunBattleView : MonoBehaviour
    {

        /// <summary>
        /// 現在ウェーブと全五ウェーブ数のラベル。
        /// [KO] 현재 웨이브·전체 다섯 웨이브 수 라벨입니다.
        /// [EN] Label for current wave and the five-wave total.
        /// </summary>
        [SerializeField] private TMP_Text _wave;

        /// <summary>
        /// 現在ウェーブの tick に基づく時間表示。
        /// [KO] 현재 웨이브 tick 기반 시간 표시입니다.
        /// [EN] Tick-derived timer for the current wave.
        /// </summary>
        [SerializeField] private TMP_Text _time;

        /// <summary>
        /// 獲得した強化を種類別に表示するラベル。
        /// [KO] 획득 강화를 종류별로 표시하는 라벨입니다.
        /// [EN] Label listing acquired upgrades by kind.
        /// </summary>
        [SerializeField] private TMP_Text _upgrades;

        /// <summary>
        /// 一倍または二倍を示すボタンのラベル。
        /// [KO] 1배·2배를 표시할 버튼 라벨입니다.
        /// [EN] Button label showing one or two times speed.
        /// </summary>
        [SerializeField] private TMP_Text _speedText;

        /// <summary>
        /// 戦闘時計に含めない開始・クリア案内。
        /// [KO] 전투 시간에 포함하지 않는 시작·클리어 안내입니다.
        /// [EN] Introduction and clear guidance excluded from combat time.
        /// </summary>
        [SerializeField] private TMP_Text _banner;

        /// <summary>
        /// コードで速度切替へ接続する共通ボタン。
        /// [KO] 코드로 속도 전환에 연결하는 공통 버튼입니다.
        /// [EN] Common button connected in code to speed changes.
        /// </summary>
        [SerializeField] private ActionButtonView _speed;

        /// <summary>
        /// コードで一時停止へ接続する共通ボタン。
        /// [KO] 코드로 일시정지에 연결하는 공통 버튼입니다.
        /// [EN] Common button connected in code to pause.
        /// </summary>
        [SerializeField] private ActionButtonView _pause;

        /// <summary>
        /// 報酬カードを所有し、背後への入力を遮るモーダル。
        /// [KO] 보상 카드를 소유하고 뒤 입력을 차단하는 모달입니다.
        /// [EN] Modal owning reward cards and blocking input behind them.
        /// </summary>
        [SerializeField] private CanvasGroup _rewards;

        /// <summary>
        /// 最大三候補を表示する共通親の派生カード。
        /// [KO] 최대 세 후보를 표시하는 공통 부모 파생 카드입니다.
        /// [EN] Cards derived from the common base displaying up to three offers.
        /// </summary>
        [SerializeField] private RewardCardView[] _cards;

        /// <summary>
        /// 候補ゼロ時だけ表示する続行ボタン。
        /// [KO] 후보가 없을 때만 표시하는 진행 버튼입니다.
        /// [EN] Continue button displayed only for an empty offer.
        /// </summary>
        [SerializeField] private ActionButtonView _continue;

        /// <summary>
        /// 再開と音量・終了をまとめる一時停止モーダル。
        /// [KO] 재개·음량·종료를 묶는 일시정지 모달입니다.
        /// [EN] Pause modal containing resume, volume and exit.
        /// </summary>
        [SerializeField] private CanvasGroup _pausePanel;

        /// <summary>
        /// 明示再開を通知するボタン。
        /// [KO] 명시적 재개를 알릴 버튼입니다.
        /// [EN] Button requesting explicit resume.
        /// </summary>
        [SerializeField] private ActionButtonView _resume;

        /// <summary>
        /// 終了確認を開くボタン。
        /// [KO] 종료 확인을 여는 버튼입니다.
        /// [EN] Button opening exit confirmation.
        /// </summary>
        [SerializeField] private ActionButtonView _exit;

        /// <summary>
        /// 一時停止より上で入力を受ける終了確認モーダル。
        /// [KO] 일시정지 위에서 입력을 받는 종료 확인 모달입니다.
        /// [EN] Exit confirmation receiving input above pause.
        /// </summary>
        [SerializeField] private CanvasGroup _confirmation;

        /// <summary>
        /// 確認を取り消すボタン。
        /// [KO] 확인을 취소하는 버튼입니다.
        /// [EN] Button cancelling exit confirmation.
        /// </summary>
        [SerializeField] private ActionButtonView _cancelExit;

        /// <summary>
        /// 勝敗を記録せずタイトルへ戻ることを確認するボタン。
        /// [KO] 승패 기록 없이 타이틀로 돌아가도록 확인하는 버튼입니다.
        /// [EN] Button confirming return to Title without an outcome record.
        /// </summary>
        [SerializeField] private ActionButtonView _confirmExit;

        /// <summary>
        /// 停止中も動く共通マスター音量のスライダー。
        /// [KO] 정지 중에도 동작하는 공통 마스터 음량 슬라이더입니다.
        /// [EN] Shared master-volume slider usable during pause.
        /// </summary>
        [SerializeField] private UnityEngine.UI.Slider _volume;

        /// <summary>
        /// 共通音量を百分率で表示するラベル。
        /// [KO] 공통 음량을 백분율로 표시할 라벨입니다.
        /// [EN] Label displaying shared volume as a percentage.
        /// </summary>
        [SerializeField] private TMP_Text _volumeText;

        /// <summary>
        /// 入力 listener の重複登録を防ぐ初期化状態。
        /// [KO] 입력 listener 중복 등록을 막는 초기화 상태입니다.
        /// [EN] Initialization state preventing duplicate listener registration.
        /// </summary>
        private bool _isInitialized;

        /// <summary>
        /// OS から最後に通知されたフォーカス状態。
        /// [KO] OS가 마지막으로 알린 포커스 상태입니다.
        /// [EN] Most recently reported focus state.
        /// </summary>
        private bool _hasFocus = true;

        /// <summary>
        /// OS がアプリを一時中断しているか。
        /// [KO] OS가 앱을 일시 중단했는지 나타냅니다.
        /// [EN] Whether the OS has paused the application.
        /// </summary>
        private bool _isAppPaused;

        /// <summary>
        /// この画面の文字・背景と共通パレットの対応。
        /// [KO] 이 화면의 글자·배경과 공통 팔레트의 대응입니다.
        /// [EN] Mapping from this screen's labels and backgrounds to the shared palette.
        /// </summary>
        [SerializeField] private PaletteGraphicBinding[] _paletteBindings;

        /// <summary>
        /// 通常時の時間ラベル色。
        /// [KO] 평상시 시간 라벨 색상입니다.
        /// [EN] Normal timer-label color.
        /// </summary>
        private Color _timerNormal;

        /// <summary>
        /// 残り15秒で使う敵側の警告色。
        /// [KO] 남은 15초에 사용하는 적 측 경고 색상입니다.
        /// [EN] Enemy warning color used for the final fifteen seconds.
        /// </summary>
        private Color _timerWarning;

        /// <summary>
        /// 自分の画面とカードへ共通配色を適用し、残り時間の強調色を保持します。
        /// [KO] 자신의 화면·카드에 공통 색상을 적용하고 남은 시간 강조색을 보관합니다.
        /// [EN] Applies shared colors to this screen and cards and retains the remaining-time warning color.
        /// </summary>
        public void ApplyPalette(GameColorPalette colors)
        {
            Validate();
            if (colors == null || _paletteBindings == null || _paletteBindings.Length == 0)
                throw new InvalidOperationException("Run controls require palette bindings.");

            foreach (var binding in _paletteBindings)
            {
                if (binding == null)
                    throw new InvalidOperationException("A run controls palette binding is missing.");

                binding.Apply(colors);
            }

            foreach (var card in _cards)
                card.ApplyPalette(colors);

            _timerNormal = colors.GetColor(Define.PaletteColor.OnBackground);
            _timerWarning = colors.GetColor(Define.PaletteColor.Enemy);
        }

        /// <summary>
        /// フォーカスと OS 停止の両方が解除されているか。
        /// [KO] 포커스·OS 정지가 모두 해제되었는지 나타냅니다.
        /// [EN] Whether focus is present and the OS has not paused the app.
        /// </summary>
        public bool HasFocus => _hasFocus && !_isAppPaused;

        /// <summary>
        /// 同じ報酬表示の三候補。
        /// [KO] 같은 보상 화면의 세 후보입니다.
        /// [EN] Three card views belonging to the same reward offer.
        /// </summary>
        public IReadOnlyList<RewardCardView> Cards => _cards;

        /// <summary>
        /// 速度ボタンの入力を通知します。
        /// [KO] 속도 버튼 입력을 알립니다.
        /// [EN] Reports speed-button input.
        /// </summary>
        public event Action SpeedRequested;

        /// <summary>
        /// 一時停止ボタンの入力を通知します。
        /// [KO] 일시정지 버튼 입력을 알립니다.
        /// [EN] Reports pause-button input.
        /// </summary>
        public event Action PauseRequested;

        /// <summary>
        /// 再開ボタンの入力を通知します。
        /// [KO] 재개 버튼 입력을 알립니다.
        /// [EN] Reports resume-button input.
        /// </summary>
        public event Action ResumeRequested;

        /// <summary>
        /// 終了確認を開く要求を通知します。
        /// [KO] 종료 확인을 열 요청을 알립니다.
        /// [EN] Reports requests to open exit confirmation.
        /// </summary>
        public event Action ExitRequested;

        /// <summary>
        /// 終了確認の取消を通知します。
        /// [KO] 종료 확인 취소를 알립니다.
        /// [EN] Reports exit-confirmation cancellation.
        /// </summary>
        public event Action CancelExitRequested;

        /// <summary>
        /// 確認した Run 終了を通知します。
        /// [KO] 확인한 Run 종료를 알립니다.
        /// [EN] Reports confirmed run exit.
        /// </summary>
        public event Action ConfirmExitRequested;

        /// <summary>
        /// 空の報酬候補からの続行を通知します。
        /// [KO] 빈 보상 후보에서 진행하도록 알립니다.
        /// [EN] Reports continuation from an empty reward offer.
        /// </summary>
        public event Action ContinueRequested;

        /// <summary>
        /// 最上位画面へ送る Esc 入力を通知します。
        /// [KO] 최상위 화면에 전달할 Esc 입력을 알립니다.
        /// [EN] Reports Escape input for the topmost screen.
        /// </summary>
        public event Action EscapeRequested;

        /// <summary>
        /// フォーカスまたは OS 停止の変化を通知します。
        /// [KO] 포커스·OS 정지 변경을 알립니다.
        /// [EN] Reports focus or OS pause changes.
        /// </summary>
        public event Action FocusChanged;

        /// <summary>
        /// スライダーで選んだマスター音量を通知します。
        /// [KO] 슬라이더에서 선택한 마스터 음량을 알립니다.
        /// [EN] Reports master volume chosen on the slider.
        /// </summary>
        public event Action<float> VolumeRequested;

        /// <summary>
        /// 保存済み参照を検査し、各ボタンとスライダーをコードで一度だけ接続します。
        /// [KO] 저장 참조를 검사하고 각 버튼·슬라이더를 코드에서 한 번만 연결합니다.
        /// [EN] Validates saved references and connects buttons and volume once in code.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;

            Validate();
            _speed.SetClickHandler(() => SpeedRequested?.Invoke());
            _pause.SetClickHandler(() => PauseRequested?.Invoke());
            _resume.SetClickHandler(() => ResumeRequested?.Invoke());
            _exit.SetClickHandler(() => ExitRequested?.Invoke());
            _cancelExit.SetClickHandler(() => CancelExitRequested?.Invoke());
            _confirmExit.SetClickHandler(() => ConfirmExitRequested?.Invoke());
            _continue.SetClickHandler(() => ContinueRequested?.Invoke());
            _volume.onValueChanged.AddListener(HandleVolumeChanged);
            _hasFocus = UnityEngine.Application.isFocused;
            _isInitialized = true;
        }

        /// <summary>
        /// 新しい Run の表示を始める前に、欠落した参照を明示的に拒否します。
        /// [KO] 새 Run 표시 전에 누락 참조를 명시적으로 거부합니다.
        /// [EN] Explicitly rejects missing references before displaying a new run.
        /// </summary>
        public void Validate()
        {
            if (_wave == null || _time == null || _upgrades == null || _speedText == null || _banner == null ||
                _speed == null || _pause == null || _rewards == null || _continue == null || _pausePanel == null ||
                _resume == null || _exit == null || _confirmation == null || _cancelExit == null || _confirmExit == null ||
                _volume == null || _volumeText == null || _cards == null || _cards.Length != 3)
            {
                throw new InvalidOperationException("Run battle UI requires all authored references and three cards.");
            }

            foreach (var card in _cards)
                if (card == null)
                    throw new InvalidOperationException("A reward card reference is missing.");
        }

        /// <summary>
        /// 状態だけからモーダル・バナー・操作可否を表示します。後ろの画面への入力は遮断します。
        /// [KO] 상태만으로 모달·배너·조작 가능 여부를 표시하며 뒤 화면 입력은 차단합니다.
        /// [EN] Displays modals, banner and interactability from state while blocking input to screens behind modals.
        /// </summary>
        public void ShowState(Define.RunPhase phase, int wave, int speed, bool isPaused, bool isConfirmingExit, bool canControlBattle, bool shouldShowClear)
        {
            _wave.text = $"WAVE {wave} / {Define.WavesPerRun}";
            _speedText.text = $"×{speed}";
            _speed.GetComponent<UnityEngine.UI.Button>().interactable = canControlBattle && !isPaused;
            _pause.GetComponent<UnityEngine.UI.Button>().interactable = canControlBattle && !isPaused;
            _banner.gameObject.SetActive(!isPaused && (phase == Define.RunPhase.WaveIntro || (phase == Define.RunPhase.WaveClear && shouldShowClear)));
            _banner.text = phase == Define.RunPhase.WaveIntro ? $"WAVE {wave}" : "WAVE CLEAR";
            SetModal(_rewards, phase == Define.RunPhase.RewardSelection, true);
            SetModal(_pausePanel, isPaused && !isConfirmingExit, true);
            SetModal(_confirmation, isPaused && isConfirmingExit, true);
        }

        /// <summary>
        /// tick から得た秒数だけを戦闘時間として表示します。
        /// [KO] tick에서 얻은 초만 전투 시간으로 표시합니다.
        /// [EN] Displays only tick-derived seconds as combat time.
        /// </summary>
        public void ShowClock(int ticks)
        {
            var seconds = ticks / Define.TicksPerSecond;
            _time.text = $"{seconds / 60:00}:{seconds % 60:00} / 01:30";
            _time.color = ticks >= Define.BattleTickLimit - 15 * Define.TicksPerSecond ? _timerWarning : _timerNormal;
        }

        /// <summary>
        /// 候補数に合わせてカードを中央へ並べ、候補ゼロ時だけ次へボタンを表示します。
        /// [KO] 후보 수에 맞춰 카드를 중앙에 배치하고 후보가 없을 때만 다음 버튼을 표시합니다.
        /// [EN] Centers cards for the offer count and displays Continue only when no offers exist.
        /// </summary>
        public void ShowOfferCount(int count)
        {
            _continue.gameObject.SetActive(count == 0);
            for (var index = 0; index < _cards.Length; index++)
            {
                _cards[index].gameObject.SetActive(index < count);
                var card = (RectTransform)_cards[index].transform;
                card.anchoredPosition = new Vector2((index - (count - 1) * 0.5f) * 400, card.anchoredPosition.y);
            }
        }

        /// <summary>
        /// 現在の累積強化を表示します。
        /// [KO] 현재 누적 강화를 표시합니다.
        /// [EN] Displays current cumulative upgrades.
        /// </summary>
        public void ShowUpgrades(string text)
            => _upgrades.text = text;

        /// <summary>
        /// 共通音量を通知なしで表示します。
        /// [KO] 공통 음량을 알림 없이 표시합니다.
        /// [EN] Displays shared volume without firing input notifications.
        /// </summary>
        public void ShowVolume(float value)
        {
            _volume.SetValueWithoutNotify(value);
            _volumeText.text = $"音量 {Mathf.RoundToInt(value * 100)}%";
        }

        /// <summary>
        /// 表示・入力可否・背後への遮断を同時に切り替えます。
        /// [KO] 표시·입력 가능 여부·뒤 입력 차단을 함께 전환합니다.
        /// [EN] Updates visibility, interactability and blocking of input behind a modal together.
        /// </summary>
        private static void SetModal(CanvasGroup group, bool isVisible, bool acceptsInput)
        {
            group.gameObject.SetActive(isVisible);
            group.interactable = acceptsInput;
            group.blocksRaycasts = isVisible;
        }

        /// <summary>
        /// スライダーの値を所有 Presenter に渡します。
        /// [KO] 슬라이더 값을 소유 Presenter에 전달합니다.
        /// [EN] Forwards the slider value to the owning presenter.
        /// </summary>
        private void HandleVolumeChanged(float value)
            => VolumeRequested?.Invoke(value);

        /// <summary>
        /// フォーカスがある間だけ Esc を入力として通知します。
        /// [KO] 포커스가 있을 때만 Esc를 입력으로 알립니다.
        /// [EN] Reports Escape input only while focused.
        /// </summary>
        private void Update()
        {
            if (_isInitialized && HasFocus && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                EscapeRequested?.Invoke();
        }

        /// <summary>
        /// 最新フォーカスを保持して所有者へ通知します。
        /// [KO] 최신 포커스를 보관하고 소유자에 알립니다.
        /// [EN] Stores the latest focus state and notifies the owner.
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;
            FocusChanged?.Invoke();
        }

        /// <summary>
        /// OS の停止状態を保持して所有者へ通知します。
        /// [KO] OS 정지 상태를 보관하고 소유자에 알립니다.
        /// [EN] Stores OS pause state and notifies the owner.
        /// </summary>
        private void OnApplicationPause(bool isPaused)
        {
            _isAppPaused = isPaused;
            FocusChanged?.Invoke();
        }

        /// <summary>
        /// 自分の音量 listener と外部通知を解除します。
        /// [KO] 자신의 음량 listener·외부 알림을 해제합니다.
        /// [EN] Releases the owned volume listener and external notifications.
        /// </summary>
        private void OnDestroy()
        {
            if (_volume != null && _isInitialized)
                _volume.onValueChanged.RemoveListener(HandleVolumeChanged);

            SpeedRequested = null;
            PauseRequested = null;
            ResumeRequested = null;
            ExitRequested = null;
            CancelExitRequested = null;
            ConfirmExitRequested = null;
            ContinueRequested = null;
            EscapeRequested = null;
            FocusChanged = null;
            VolumeRequested = null;
        }
    }
}
