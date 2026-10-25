using System;
using System.Collections.Generic;
using TMPro;
using UnityCombatLab.Common;
using UnityCombatLab.Presentation.ReadModels;
using UnityCombatLab.Presentation.Styling;
using UnityEngine;
using UnityEngine.UI;

namespace UnityCombatLab.Presentation.UI
{
    /// <summary>
    /// Frontend の独立した画面を表示し、ボタンの入力意図だけを Presenter へ通知します。
    /// [KO] Frontend의 독립 화면을 표시하고 버튼 입력 의도만 Presenter에 알립니다.
    /// [EN] Displays independent Frontend pages and sends button intentions to the presenter.
    /// </summary>
    public sealed class FrontendView : MonoBehaviour, IFrontendView
    {
        /// <summary>
        /// タイトル画面。
        /// [KO] 타이틀 화면입니다.
        /// [EN] Title page.
        /// </summary>
        [SerializeField] private GameObject _titlePage;

        /// <summary>
        /// 編成画面。
        /// [KO] 편성 화면입니다.
        /// [EN] Formation page.
        /// </summary>
        [SerializeField] private GameObject _formationPage;

        /// <summary>
        /// 結果画面。
        /// [KO] 결과 화면입니다.
        /// [EN] Results page.
        /// </summary>
        [SerializeField] private GameObject _resultsPage;

        /// <summary>
        /// 開始失敗の画面。
        /// [KO] 시작 실패 화면입니다.
        /// [EN] Startup failure page.
        /// </summary>
        [SerializeField] private GameObject _errorPage;

        /// <summary>
        /// タイトルから編成へ進むボタン。
        /// [KO] 타이틀에서 편성으로 가는 버튼입니다.
        /// [EN] Button opening formation from title.
        /// </summary>
        [SerializeField] private Button _titleStart;

        /// <summary>
        /// 編成からタイトルへ戻るボタン。
        /// [KO] 편성에서 타이틀로 돌아가는 버튼입니다.
        /// [EN] Button returning to title from formation.
        /// </summary>
        [SerializeField] private Button _formationBack;

        /// <summary>
        /// 結果から再編成へ進むボタン。
        /// [KO] 결과에서 재편성으로 가는 버튼입니다.
        /// [EN] Button opening formation after results.
        /// </summary>
        [SerializeField] private Button _reform;

        /// <summary>
        /// 結果からタイトルへ戻るボタン。
        /// [KO] 결과에서 타이틀로 돌아가는 버튼입니다.
        /// [EN] Button returning to title from results.
        /// </summary>
        [SerializeField] private Button _resultTitle;

        /// <summary>
        /// 失敗した出発を再試行するボタン。
        /// [KO] 실패한 출발을 재시도하는 버튼입니다.
        /// [EN] Button retrying a failed departure.
        /// </summary>
        [SerializeField] private Button _retry;

        /// <summary>
        /// 失敗画面からタイトルへ戻るボタン。
        /// [KO] 실패 화면에서 타이틀로 돌아가는 버튼입니다.
        /// [EN] Button returning to title from the error page.
        /// </summary>
        [SerializeField] private Button _errorTitle;

        /// <summary>
        /// 騎士・弓使い・魔法使いを選択するカード。
        /// [KO] 기사·궁수·마법사를 선택할 카드입니다.
        /// [EN] Cards selecting knight, ranger and mage.
        /// </summary>
        [SerializeField] private Button[] _heroButtons;
        /// <summary>
        /// 英雄の実際の能力と選択状態を示すカード文字。
        /// [KO] 영웅의 실제 능력과 선택 상태를 보여줄 카드 글자입니다.
        /// [EN] Card text showing actual hero stats and selection.
        /// </summary>
        [SerializeField] private TMP_Text[] _heroTexts;
        /// <summary>
        /// 前列・後列上・後列下の順の移動先。
        /// [KO] 전열·위쪽 후열·아래쪽 후열 순서의 이동 위치입니다.
        /// [EN] Destination slots in front, upper-back and lower-back order.
        /// </summary>
        [SerializeField] private Button[] _slotButtons;
        /// <summary>
        /// 各配置枠と、現在そこにいる英雄を示す文字。
        /// [KO] 각 배치 슬롯과 현재 영웅을 나타내는 글자입니다.
        /// [EN] Text identifying each slot and its current hero.
        /// </summary>
        [SerializeField] private TMP_Text[] _slotTexts;
        /// <summary>
        /// 標準・やさしいの選択ボタン。
        /// [KO] 표준·쉬움 선택 버튼입니다.
        /// [EN] Standard and easy difficulty buttons.
        /// </summary>
        [SerializeField] private Button[] _difficultyButtons;
        /// <summary>
        /// 難易度の選択印と説明。
        /// [KO] 난이도 선택 표시와 설명입니다.
        /// [EN] Difficulty selection marks and descriptions.
        /// </summary>
        [SerializeField] private TMP_Text[] _difficultyTexts;
        /// <summary>
        /// 編成を確定して出発するボタン。
        /// [KO] 편성을 확정하고 출발할 버튼입니다.
        /// [EN] Button committing formation and departing.
        /// </summary>
        [SerializeField] private Button _depart;
        /// <summary>
        /// Scene 全体の操作を終了処理中に停止する Group。
        /// [KO] 종료 처리 중 씬 전체 조작을 멈출 Group입니다.
        /// [EN] Group disabling scene input during cleanup.
        /// </summary>
        [SerializeField] private CanvasGroup _inputGroup;
        /// <summary>
        /// 同じ結果画面内で切り替える、既存の勝利・敗北 prefab。
        /// [KO] 같은 결과 화면에서 전환할 기존 승리·패배 prefab입니다.
        /// [EN] Existing victory and defeat prefabs switched within the result page.
        /// </summary>
        [SerializeField] private GameObject _victory;
        /// <summary>
        /// 全滅または時間切れを表示する既存 prefab。
        /// [KO] 전멸 또는 시간 초과를 표시할 기존 prefab입니다.
        /// [EN] Existing prefab displaying defeat or timeout.
        /// </summary>
        [SerializeField] private GameObject _defeat;
        /// <summary>
        /// 既存の敗北イラスト内にある終了理由。
        /// [KO] 기존 패배 일러스트 안의 종료 이유입니다.
        /// [EN] Outcome reason within the existing defeat illustration.
        /// </summary>
        [SerializeField] private TMP_Text _defeatReason;
        /// <summary>
        /// RunResult の難易度・経過 tick・終了理由を示す文字。
        /// [KO] RunResult의 난이도·경과 tick·종료 이유를 표시할 글자입니다.
        /// [EN] Text showing RunResult difficulty, elapsed ticks and outcome reason.
        /// </summary>
        [SerializeField] private TMP_Text _resultDetails;
        /// <summary>
        /// Run の参照を持たない短いエラー説明。
        /// [KO] Run 참조가 없는 짧은 오류 설명입니다.
        /// [EN] Short error explanation holding no run references.
        /// </summary>
        [SerializeField] private TMP_Text _errorText;
        /// <summary>
        /// 共通音量のスライダー。
        /// [KO] 공통 음량 슬라이더입니다.
        /// [EN] Shared volume slider.
        /// </summary>
        [SerializeField] private Slider _volume;
        /// <summary>
        /// 現在音量の百分率。
        /// [KO] 현재 음량의 백분율입니다.
        /// [EN] Current volume percentage.
        /// </summary>
        [SerializeField] private TMP_Text _volumeText;

        /// <summary>
        /// 自作の文字・パネルに共通色を適用する接続。
        /// [KO] 자체 글자·패널에 공통 색을 적용할 연결입니다.
        /// [EN] Bindings applying shared colors to custom text and panels.
        /// </summary>
        [SerializeField] private PaletteGraphicBinding[] _paletteBindings;

        /// <summary>
        /// Esc を編成からの戻る入力として扱うための現在画面。
        /// [KO] Esc를 편성에서 뒤로 가는 입력으로 처리할 현재 화면입니다.
        /// [EN] Current page used to handle Escape as formation back navigation.
        /// </summary>
        private Define.FrontendPage _currentPage;

        /// <summary>
        /// シーンの EntryPoint が入力を一度だけ接続したかを記録します。
        /// [KO] 씬 EntryPoint가 입력을 한 번 연결했는지 기록합니다.
        /// [EN] Records whether the scene entry point has connected input once.
        /// </summary>
        private bool _isInitialized;

        /// <summary>
        /// 画面切替の要求。
        /// [KO] 화면 전환 요청입니다.
        /// [EN] Page navigation request.
        /// </summary>
        public event Action<Define.FrontendPage> PageRequested;

        /// <summary>
        /// 英雄カードの選択。
        /// [KO] 영웅 카드 선택입니다.
        /// [EN] Hero-card selection.
        /// </summary>
        public event Action<Define.UnitRole> HeroSelected;

        /// <summary>
        /// 配置先の選択。
        /// [KO] 배치할 위치 선택입니다.
        /// [EN] Destination-slot selection.
        /// </summary>
        public event Action<Define.BattleSlot> SlotSelected;

        /// <summary>
        /// 難易度の変更。
        /// [KO] 난이도 변경입니다.
        /// [EN] Difficulty change.
        /// </summary>
        public event Action<Define.Difficulty> DifficultySelected;

        /// <summary>
        /// 出発要求。
        /// [KO] 출발 요청입니다.
        /// [EN] Departure request.
        /// </summary>
        public event Action DepartureRequested;

        /// <summary>
        /// 失敗した読み込みの再試行。
        /// [KO] 실패한 로딩 재시도입니다.
        /// [EN] Retry of a failed load.
        /// </summary>
        public event Action RetryRequested;

        /// <summary>
        /// 音量の変更要求。
        /// [KO] 음량 변경 요청입니다.
        /// [EN] Volume-change request.
        /// </summary>
        public event Action<float> VolumeRequested;

        /// <summary>
        /// EntryPoint が必須参照を確認してから、同じ prefab 内の入力を一度だけ接続します。
        /// [KO] EntryPoint가 필수 참조를 확인한 뒤 같은 prefab 안의 입력을 한 번만 연결합니다.
        /// [EN] Called by the entry point to validate required references and connect prefab input once.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;

            Validate();
            _titleStart.onClick.AddListener(() => PageRequested?.Invoke(Define.FrontendPage.Formation));
            _formationBack.onClick.AddListener(() => PageRequested?.Invoke(Define.FrontendPage.Title));
            _reform.onClick.AddListener(() => PageRequested?.Invoke(Define.FrontendPage.Formation));
            _resultTitle.onClick.AddListener(() => PageRequested?.Invoke(Define.FrontendPage.Title));
            _retry.onClick.AddListener(() => RetryRequested?.Invoke());
            _errorTitle.onClick.AddListener(() => PageRequested?.Invoke(Define.FrontendPage.Title));
            for (var index = 0; index < Define.SlotsPerTeam; index++)
            {
                var selection = index;
                // 0/1/2 は騎士/弓使い/魔法使い、および前列/後列上/後列下の順。
                _heroButtons[index].onClick.AddListener(() => HeroSelected?.Invoke((Define.UnitRole)selection));
                _slotButtons[index].onClick.AddListener(() => SlotSelected?.Invoke((Define.BattleSlot)selection));
            }

            _difficultyButtons[0].onClick.AddListener(() => DifficultySelected?.Invoke(Define.Difficulty.Standard));
            _difficultyButtons[1].onClick.AddListener(() => DifficultySelected?.Invoke(Define.Difficulty.Easy));
            _depart.onClick.AddListener(() => DepartureRequested?.Invoke());
            _volume.onValueChanged.AddListener(value => VolumeRequested?.Invoke(value));
            _isInitialized = true;
        }

        /// <summary>
        /// 入力を接続する前に、各画面の必須参照と固定配列の長さ・要素を確認します。
        /// [KO] 입력 연결 전에 각 화면의 필수 참조와 고정 배열의 길이·원소를 확인합니다.
        /// [EN] Validates required page references and fixed-array lengths and entries before connecting input.
        /// </summary>
        private void Validate()
        {
            RequireReference(_titlePage, nameof(_titlePage));
            RequireReference(_formationPage, nameof(_formationPage));
            RequireReference(_resultsPage, nameof(_resultsPage));
            RequireReference(_errorPage, nameof(_errorPage));
            RequireReference(_titleStart, nameof(_titleStart));
            RequireReference(_formationBack, nameof(_formationBack));
            RequireReference(_reform, nameof(_reform));
            RequireReference(_resultTitle, nameof(_resultTitle));
            RequireReference(_retry, nameof(_retry));
            RequireReference(_errorTitle, nameof(_errorTitle));
            RequireReference(_depart, nameof(_depart));
            RequireReference(_inputGroup, nameof(_inputGroup));
            RequireReference(_victory, nameof(_victory));
            RequireReference(_defeat, nameof(_defeat));
            RequireReference(_defeatReason, nameof(_defeatReason));
            RequireReference(_resultDetails, nameof(_resultDetails));
            RequireReference(_errorText, nameof(_errorText));
            RequireReference(_volume, nameof(_volume));
            RequireReference(_volumeText, nameof(_volumeText));
            ValidateReferences(_heroButtons, Define.SlotsPerTeam, nameof(_heroButtons));
            ValidateReferences(_heroTexts, Define.SlotsPerTeam, nameof(_heroTexts));
            ValidateReferences(_slotButtons, Define.SlotsPerTeam, nameof(_slotButtons));
            ValidateReferences(_slotTexts, Define.SlotsPerTeam, nameof(_slotTexts));
            ValidateReferences(_difficultyButtons, 2, nameof(_difficultyButtons));
            ValidateReferences(_difficultyTexts, 2, nameof(_difficultyTexts));
            if (_paletteBindings == null || _paletteBindings.Length == 0)
                throw new InvalidOperationException("Frontend requires palette bindings.");

            for (var index = 0; index < _paletteBindings.Length; index++)
                if (_paletteBindings[index] == null)
                    throw new InvalidOperationException($"Frontend palette binding {index} is missing.");
        }

        /// <summary>
        /// 未設定または破棄済みの Unity 参照を、Inspector のフィールド名と共に報告します。
        /// [KO] 미설정 또는 파괴된 Unity 참조를 Inspector 필드명과 함께 보고합니다.
        /// [EN] Reports missing or destroyed Unity references with their Inspector field names.
        /// </summary>
        private static void RequireReference(UnityEngine.Object reference, string fieldName)
        {
            if (reference == null)
                throw new InvalidOperationException($"Frontend requires {fieldName}.");
        }

        /// <summary>
        /// 英雄・配置枠・難易度の配列が必要数を持ち、各要素が利用可能かを確認します。
        /// [KO] 영웅·배치 슬롯·난이도 배열이 필요한 개수이며 각 원소를 사용할 수 있는지 확인합니다.
        /// [EN] Checks required counts and usable entries in hero, formation-slot and difficulty arrays.
        /// </summary>
        private static void ValidateReferences(IReadOnlyList<UnityEngine.Object> references, int count, string fieldName)
        {
            if (references == null || references.Count != count)
                throw new InvalidOperationException($"Frontend {fieldName} requires exactly {count} entries.");

            for (var index = 0; index < references.Count; index++)
                RequireReference(references[index], $"{fieldName}[{index}]");
        }

        /// <summary>
        /// 編成中だけ Esc を画面の戻るボタンと同じ要求へ変換します。
        /// [KO] 편성 중에만 Esc를 화면 뒤로 버튼과 같은 요청으로 변환합니다.
        /// [EN] Converts Escape into the same request as the formation back button.
        /// </summary>
        private void Update()
        {
            if (_isInitialized && _currentPage == Define.FrontendPage.Formation && _inputGroup.interactable &&
                UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                PageRequested?.Invoke(Define.FrontendPage.Title);
            }
        }

        /// <summary>
        /// 指定画面だけを表示し、結果は独立した値から描画します。
        /// [KO] 지정한 화면만 표시하고 결과는 독립된 값에서 그립니다.
        /// [EN] Shows only the requested page and renders results from independent values.
        /// </summary>
        public void ShowPage(Define.FrontendPage page, RunResultDisplay result, string error)
        {
            _currentPage = page;
            if (page == Define.FrontendPage.Results && result == null)
                throw new InvalidOperationException("Results page requires a result snapshot.");

            if (result != null)
            {
                var isVictory = result.IsVictory;
                _victory.SetActive(isVictory);
                _defeat.SetActive(!isVictory);
                _defeatReason.text = result.Reason;
                _resultDetails.text = result.Details;
            }

            _errorText.text = error ?? GameText.StartupFailed;
            _titlePage.SetActive(page == Define.FrontendPage.Title);
            _formationPage.SetActive(page == Define.FrontendPage.Formation);
            _resultsPage.SetActive(page == Define.FrontendPage.Results);
            _errorPage.SetActive(page == Define.FrontendPage.Error);
        }

        /// <summary>
        /// 実データのカード文字と、交換後の三つの配置を表示します。
        /// [KO] 실제 데이터 카드 글자와 교환 후 세 배치를 표시합니다.
        /// [EN] Displays data-backed hero cards and the three slots after swapping.
        /// </summary>
        public void ShowFormation(FormationDisplay display)
        {
            for (var index = 0; index < _heroTexts.Length; index++)
                _heroTexts[index].text = display.Heroes[index];
            for (var index = 0; index < _slotTexts.Length; index++)
                _slotTexts[index].text = display.Slots[index];
            for (var index = 0; index < _difficultyTexts.Length; index++)
                _difficultyTexts[index].text = display.Difficulties[index];
        }

        /// <summary>
        /// 調整側が処理中の間、全てのページ入力を止めます。
        /// [KO] 조정자가 처리 중인 동안 모든 페이지 입력을 멈춥니다.
        /// [EN] Disables all page input while the coordinator is busy.
        /// </summary>
        public void SetInteractable(bool value)
            => _inputGroup.interactable = value;

        /// <summary>
        /// 開始失敗にだけ再試行ボタンを表示します。
        /// [KO] 시작 실패에만 재시도 버튼을 표시합니다.
        /// [EN] Shows the retry button only for startup failures.
        /// </summary>
        public void SetErrorRetry(bool value)
            => _retry.gameObject.SetActive(value);

        /// <summary>
        /// 画面開始時に共通パレットを自作表示へ適用します。
        /// [KO] 화면 시작 시 공통 팔레트를 자체 표시에 적용합니다.
        /// [EN] Applies shared palette colors to custom graphics at screen startup.
        /// </summary>
        public void ApplyPalette(GameColorPalette colors)
        {
            foreach (var binding in _paletteBindings)
                binding.Apply(colors);
        }

        /// <summary>
        /// 通知を再発行せず音量表示を合わせます。
        /// [KO] 알림을 다시 발행하지 않고 음량 표시를 맞춥니다.
        /// [EN] Synchronizes volume display without re-emitting input.
        /// </summary>
        public void SetVolume(float value)
        {
            _volume.SetValueWithoutNotify(value);
            _volumeText.text = $"{Mathf.RoundToInt(value * Define.PercentScale)}%";
        }

    }
}
