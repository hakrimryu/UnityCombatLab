# Unity Combat Lab

**3명의 영웅을 배치하고, 웨이브 사이의 강화를 선택해 5번의 자동전투를 돌파하는 Unity 클라이언트 포트폴리오입니다.**

기사·궁수·마법사의 전열/후열 배치와 강화 조합이 전투 결과를 바꿉니다. 타이틀 → 편성 → 전투 → 강화 선택 → 결과 → 재도전까지 한 판의 흐름을 구현했습니다. 게임 UI는 일본어이며, 이 README는 코드와 설계 의도를 설명하는 한국어 문서입니다.

> **현재 개발 중인 포트폴리오이며, 최종 완성본은 아닙니다.** 주요 게임 흐름을 구현한 상태로, 이후 밸런스 도구 연동·사운드 추가·로직 개선을 진행할 예정입니다.

![5웨이브 전투와 2배속, HP·방어막·피해 숫자 표시](Docs/Images/battle-wave5.png)

## 이 프로젝트에서 다룬 문제

작은 자동전투 게임에서도 **전투가 확정되는 시점과 화면에 보이는 시점**, **화면이 끝나는 시점과 로딩이 끝나는 시점**은 다릅니다. 또한 같은 전투를 재현할 수 있어도, 규칙을 잘못 바꾼 사실까지 자동으로 발견되는 것은 아닙니다. 이 프로젝트는 다음 세 문제를 중심으로 구현과 테스트를 구성했습니다.

| 문제 | 해결 방식 | 주요 코드 |
|---|---|---|
| 투사체가 닿기 전에 HP가 줄고, 피해·회복의 표시 순서가 어긋남 | 확정된 전투 상태와 표시 상태를 분리하고, 회복 전후의 적용 순서를 보존 | [UnitPresentationReadModel](Assets/UnityCombatLab/Scripts/Presentation/ReadModels/UnitPresentationReadModel.cs), [BattlePresenter](Assets/UnityCombatLab/Scripts/Presentation/Units/BattlePresenter.cs) |
| 취소하거나 화면을 닫은 뒤 로딩이 완료되어 자산의 해제 책임이 사라짐 | 성공한 로드의 소유권을 Lease로 넘기고, 종료된 Scope에 도착한 결과는 즉시 해제 | [AddressableAssetScope](Assets/UnityCombatLab/Scripts/Infrastructure/Assets/AddressableAssetScope.cs), [RunFlowCoordinator](Assets/UnityCombatLab/Scripts/Application/Run/RunFlowCoordinator.cs) |
| 잘못 변경한 전투 규칙도 현재 코드로 재실행하면 같은 결과가 나옴 | 20Hz 결정론 전투의 재현 검사와 독립된 고정 기대값의 회귀 검사를 함께 구성 | [BattleRunner](Assets/UnityCombatLab/Scripts/Application/Battle/BattleRunner.cs), [RunReplayTests](Assets/UnityCombatLab/Tests/EditMode/RunReplayTests.cs) |

게임 규모에 비해 계층별 어셈블리, DI, 표시 전용 읽기 모델, 자산 Scope 등 설계 요소를 폭넓게 적용했습니다. 포트폴리오에서 **무엇이 바뀔 때 어느 코드를 수정하고, 어떤 테스트로 확인할지** 드러내기 위한 선택입니다. 전투 슬롯과 상태 전이처럼 요구가 작은 부분은 고정 배열과 enum으로 구현했습니다.

## 게임 구성

- **편성:** 기사·궁수·마법사를 전열, 위쪽 후열, 아래쪽 후열에 배치합니다. 카드와 슬롯을 선택해 위치를 교환합니다.
- **전투:** 아군 3명과 최대 3명의 적이 자동으로 싸웁니다. 기사는 피해 감소, 궁수는 주기적인 강한 단일 공격, 마법사는 주기적인 광역 공격을 사용합니다.
- **강화:** 중간 웨이브 승리 시 최대 3개 후보 중 하나를 선택합니다. 공격력·최대 HP·공격 속도·방어막·처치 회복·초반 공격력의 6종이며, 각 종류는 최대 3회 중첩됩니다.
- **진행:** 다음 웨이브에서 아군은 부활하고 강화된 최대 HP로 회복합니다. 강화는 한 판 동안 유지되며 새 도전에서는 초기화됩니다.
- **조작:** 1배/2배속, 일시정지·재개, 종료 확인을 제공합니다. 포커스를 잃으면 자동 일시정지하고 직접 재개합니다.

![증가량과 누적 효과, 중첩 수를 보여주는 강화 선택 화면](Docs/Images/reward-selection.png)

## 구현과 설계 판단

아래 코드 블록은 실제 구현의 핵심 부분을 발췌했습니다. 각 사례에 전체 소스와 관련 테스트를 연결했습니다. **Run**은 편성 후 출발부터 클리어·패배·중도 종료까지의 한 판을 뜻합니다.

### 1. 전투 결과는 확정됐지만, 화살은 아직 도착하지 않았다

**문제.** 전투 tick에서 피해를 계산하자마자 HUD를 갱신하면 화살이 닿기 전에 HP가 줄어듭니다. 반대로 피해 계산을 애니메이션이나 Tween 완료에 맡기면 연출 시간과 배속 변경이 전투 규칙에 영향을 줍니다.

**선택.** 피해·사망·승패는 순수 C# 전투 코어에서 확정하고, 화면은 그 결과를 명중 시점에 표시합니다. Application의 `BattleReadModel`은 확정된 상태를, Presentation의 `BattlePresentationReadModel`은 화면에 보일 상태를 보관합니다. [BattlePresentationTimeline](Assets/UnityCombatLab/Scripts/Presentation/Units/BattlePresentationTimeline.cs)이 공격 동작과 명중 예약을 관리하고, [BattlePresenter](Assets/UnityCombatLab/Scripts/Presentation/Units/BattlePresenter.cs)가 유닛과 HUD를 연결합니다.

단순히 모든 이벤트를 받은 순서대로 표시하면 늦게 도착하는 마법탄 때문에 먼저 닿은 근접 공격까지 기다리게 됩니다. 따라서 **피해끼리는 도착 순서대로 표시하고, 회복 전후에는 확정된 순서를 지킵니다.** [UnitPresentationReadModel.PresentChange](Assets/UnityCombatLab/Scripts/Presentation/ReadModels/UnitPresentationReadModel.cs)가 이 순서를 관리합니다. HP를 먼저 적용한 뒤 `ChangePresented`로 같은 피해의 숫자·피격 연출을 알립니다.

예를 들어 테스트의 HP `100 → 피해 16 → 회복 8 → 피해 16`은, 마지막 피해의 연출이 먼저 도착해도 `100 → 84 → 92 → 76` 순서로 표시합니다. 회복을 건너뛰어 피해를 먼저 적용하거나, HP 갱신 전에 피해 숫자만 나오는 상황을 검사합니다.

**R3는 이 표시 상태가 바뀔 때 HUD에 전달하는 데 사용합니다.** [BattleHudPresenter](Assets/UnityCombatLab/Scripts/Presentation/UI/BattleHudPresenter.cs)의 구독은 다음과 같습니다.

```csharp
unit.Health.Subscribe(value =>
{
    if (!_isDisposed && status != null)
    {
        status.SetHealth(value, unit.Input.MaxHealth);
    }
}).AddTo(_subscriptions);
```

Presenter가 `_subscriptions`를 소유하고 `Dispose()`에서 해제합니다. 확정 상태와 표시 상태를 따로 유지하는 비용은 생기지만, 연출 타이밍을 바꿔도 전투 계산과 그 테스트는 유지할 수 있습니다. 마지막 명중·사망 표시가 끝난 뒤 다음 웨이브 또는 결과 화면으로 넘어갑니다.

**DOTween은 이 과정의 시각적 표현을 담당합니다.** [UnitView](Assets/UnityCombatLab/Scripts/Presentation/Units/UnitView.cs)의 접근·복귀와 피격, [DamageNumberView](Assets/UnityCombatLab/Scripts/Presentation/UI/DamageNumberView.cs)의 피해 숫자, UI 팝업에 사용합니다. 일시정지 중에도 메뉴는 반응해야 하므로 공통 UI에는 독립 시간을 적용합니다. [UiTweenUtility.PopIn](Assets/UnityCombatLab/Scripts/Presentation/UI/UiTweenUtility.cs)의 반환 부분입니다.

```csharp
return DOTween.Sequence()
    .Append(content.DOScale(Vector3.one, seconds).SetEase(Ease.OutBack, 1.7f))
    .Join(DOTween.To(() => group.alpha, alpha => group.alpha = alpha, 1f,
        Mathf.Min(seconds, 0.18f)).SetEase(Ease.OutQuad))
    .SetUpdate(true)
    .SetRecyclable(false);
```

전투 소속 Animator·Tween·Particle·피해 숫자에는 별도로 0/1/2배 재생 속도를 전달합니다. VFX는 [UnitEffectsView](Assets/UnityCombatLab/Scripts/Presentation/Units/UnitEffectsView.cs)의 대상 슬롯별 인스턴스를 재사용합니다. 피해 숫자는 초기 3개 슬롯이 모두 사용 중이면 추가해 표시 중인 숫자를 보존하고, 반환 시 Tween 종료와 세대 번호 변경으로 이전 콜백이 재사용된 슬롯을 건드리지 않게 합니다.

**확인하는 테스트:** [UnitPresentationReadModelTests](Assets/UnityCombatLab/Tests/EditMode/UnitPresentationReadModelTests.cs)는 피해 도착 순서, 회복 전후 순서, HP 적용 후 알림을 검사합니다. [BattlePresentationTests](Assets/UnityCombatLab/Tests/PlayMode/BattlePresentationTests.cs)는 Unity 표시 객체와 구독·연출 수명을 다룹니다.

### 2. 화면은 종료됐는데, 자산 로딩은 나중에 끝난다

**문제.** 로딩을 기다리던 Run이 취소되거나 화면이 파괴돼도, 이미 시작한 내부 로드는 나중에 완료될 수 있습니다. 기다리는 코드만 빠져나오면 결과를 누가 해제할지 불분명해집니다. 반대로 표시 객체가 남아 있는데 자산부터 해제하는 것도 피해야 합니다.

**선택.** 로드 작업의 완료와 호출자의 취소를 구분하고, 성공한 로드마다 해제 책임을 갖는 [AddressableAssetLease](Assets/UnityCombatLab/Scripts/Infrastructure/Assets/AddressableAssetLease.cs)를 반환합니다. [AddressableAssetManager.LoadAsync](Assets/UnityCombatLab/Scripts/Infrastructure/Assets/AddressableAssetManager.cs)는 UniTask로 내부 완료를 기다린 뒤 취소를 확인합니다.

```csharp
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
    if (handle.IsValid())
        Addressables.Release(handle);

    throw;
}
```

성공하면 Lease가 handle을 소유하고, 실패·취소면 로더가 해제합니다. 화면 또는 Run이 사용하는 Lease들은 [AddressableAssetScope](Assets/UnityCombatLab/Scripts/Infrastructure/Assets/AddressableAssetScope.cs)가 모아서 관리합니다. Scope가 먼저 끝났다면 뒤늦게 도착한 결과를 보관하지 않고 바로 해제합니다.

```csharp
var lease = await _assets.LoadAsync(reference, cancellationToken);
if (_isDisposed)
{
    lease.Dispose();
    throw new ObjectDisposedException(nameof(AddressableAssetScope));
}
```

이 방식은 취소에 즉시 응답하는 것보다 **진행 중인 로드의 해제 책임을 끝까지 유지하는 것**을 우선한 절충입니다. Addressables는 로컬 유닛 정의·prefab·UI·아이콘에 사용하며, [RunAssetScope](Assets/UnityCombatLab/Scripts/Infrastructure/Assets/RunAssetScope.cs)가 준비한 자산은 5웨이브 동안 유지합니다. VFX도 유닛 prefab의 의존성으로 보유하므로 타격마다 다시 로드하지 않습니다.

Run 종료에서도 순서를 명시했습니다. [RunSession](Assets/UnityCombatLab/Scripts/Application/Run/RunSession.cs)이 취소 토큰을 소유하고, [RunFlowCoordinator.EndRunAsync](Assets/UnityCombatLab/Scripts/Application/Run/RunFlowCoordinator.cs)가 입력 잠금 이후 **취소 요청 → 구독·연출·View 정리 → 다음 프레임 → 자산 해제**를 진행합니다.

```csharp
session.Cancel();
ShowTransitionSafely(Define.RunTransitionPhase.CleaningUp);
session.Battle?.Dispose();
await _nextFrame();
if (_isDisposed)
    return;

session.Resources.Dispose();
```

다음 프레임 대기는 Unity의 `Destroy` 처리가 끝난 뒤 자산을 해제하기 위한 것입니다. 이미 취소한 Run 토큰으로 필수 정리 과정까지 취소하지 않습니다. 이후 Frontend를 준비하고 Run 상태를 정리합니다.

| 소유 기간 | 유지하는 대상과 종료 기준 |
|---|---|
| AppRoot | 공통 씬 전환·로딩 UI·EventSystem 등. 앱이 유지되는 동안 사용 |
| Frontend 씬 | 타이틀·편성·결과·오류 UI, Presenter와 화면 자산. 씬 종료 시 정리 |
| Run | 취소 토큰, 유닛·UI 자산, 진행 데이터와 강화 이력. 한 판 종료 시 정리 |
| Dungeon / 웨이브 | 전투 Canvas·조작 UI는 씬 소유. Runner·유닛·HUD·구독·연출은 웨이브마다 교체 |

**확인하는 테스트:** [AddressableAssetTests](Assets/UnityCombatLab/Tests/PlayMode/AddressableAssetTests.cs)는 공유 자산의 마지막 소유자 해제, 취소 후 로드 완료, 종료된 Scope의 늦은 완료, 로드 실패 후 재시도를 검사합니다. [RunFlowCoordinatorTests](Assets/UnityCombatLab/Tests/EditMode/RunFlowCoordinatorTests.cs)는 정리 중 프레임 대기와 앱 Dispose가 겹치는 두 완료 순서, 해제 예외, 늦은 UI 접근을 다룹니다.

### 3. 같은 결과가 재현돼도, 규칙이 올바르게 유지됐다는 뜻은 아니다

**문제.** 프레임 간격이나 1배/2배속에 따라 공격 횟수와 승패가 달라지면 버그를 재현하기 어렵습니다. 이를 해결해도, 현재 코드로 기록과 replay를 모두 계산하면 잘못 바꾼 규칙이 양쪽에 똑같이 반영되어 검사를 통과할 수 있습니다.

**첫 번째 선택은 전투 시간과 입력을 고정하는 것입니다.** [BattleSimulation.Step](Assets/UnityCombatLab/Scripts/Domain/Battle/BattleSimulation.cs) 한 번은 50ms, 즉 20Hz입니다. HP·방어막·피해는 정수, 비율은 basis point(`10000 = 100%`)로 계산합니다. [BattleRunner](Assets/UnityCombatLab/Scripts/Application/Battle/BattleRunner.cs)가 프레임 시간을 tick 수로 환산하고, 배속은 Runner에 전달하는 시간에 적용합니다.

한 tick의 이벤트를 읽기 모델과 소비자에게 전달한 뒤, 코어에 소비 완료를 알립니다.

```csharp
_simulation.Step();
for (var index = 0; index < _simulation.EventCount; index++)
{
    var battleEvent = _simulation.GetEvent(index);
    ReadModel.Apply(battleEvent);
    EventOccurred?.Invoke(battleEvent);
    if (_isDisposed)
    {
        return;
    }
}

_simulation.AcknowledgeEvents();
```

코어는 고정 이벤트 배열을 재사용합니다. 미소비 검사와 Runner의 재진입 방어로 소비 중인 버퍼가 다음 tick에 덮어써지지 않게 합니다. 비동기 연출이 보관하는 `BattleEvent`는 값으로 전달합니다.

한 프레임에 최대 **8tick**을 처리하고 잔여 시간은 보존합니다. 부하가 크면 실제 시간보다 전투가 늦어질 수 있지만 논리 tick은 건너뛰지 않습니다. 행동 순서는 **아군 전열 → 적 전열 → 아군 위쪽 후열 → 적 위쪽 후열 → 아군 아래쪽 후열 → 적 아래쪽 후열**로 고정했습니다. 같은 tick에서도 앞선 공격에 죽으면 행동하지 않으므로 아군 우선 편향이 있습니다. 이 게임에서는 단순하고 재현 가능한 순차 판정을 선택했습니다.

완료한 Run의 설정·시드·강화 선택·이벤트·결과는 [RunReplayRecord](Assets/UnityCombatLab/Scripts/Domain/Run/RunReplayRecord.cs)에 보관합니다. [RunReplayVerifier](Assets/UnityCombatLab/Scripts/Domain/Run/RunReplayVerifier.cs)는 Unity 화면 없이 전투를 다시 계산해 최초로 달라진 이벤트 또는 결과를 보고합니다. 이벤트 비교용 해시는 고정 필드 순서·little-endian 직렬화의 FNV-1a 64-bit이며, 실행마다 달라지는 Run ID는 제외합니다.

**두 번째 선택은 현재 실행 결과와 독립된 회귀 기준을 두는 것입니다.** [RunReplayTests](Assets/UnityCombatLab/Tests/EditMode/RunReplayTests.cs)는 테스트 전용 입력·편성·시드·강화 선택을 만들고 승패, 웨이브별 tick, 이벤트 수, 선택 이력과 해시를 고정합니다. 주요 기대값은 다음과 같습니다.

```csharp
private const ulong GoldenEventHash = 0x135806989FA96900UL;
private const int GoldenTotalTicks = 386;
private const int GoldenEventCount = 152;
```

| 검사 | 발견하려는 변화 |
|---|---|
| 같은 입력을 다른 프레임 간격·1배/2배속으로 실행 | 실행 타이밍에 따라 전투 이벤트나 결과가 달라짐 |
| 기록한 Run을 화면 없이 다시 계산 | 기록과 재현 결과가 다르거나 이벤트·선택 이력이 변조됨 |
| 고정 입력을 독립된 기대값과 비교 | 기록과 replay가 함께 바뀌어도, 기존에 정한 규칙의 결과가 달라짐 |

고정 기준은 규칙 변경 시 함께 관리해야 하는 비용이 있습니다. 의도한 밸런스·규칙 변경일 때만 근거와 함께 기대값을 갱신하도록 했습니다.

**확인하는 테스트:** [BattleSimulationTests](Assets/UnityCombatLab/Tests/EditMode/BattleSimulationTests.cs)는 타깃·피해·사망·종료 순서를, [BattleRunnerTests](Assets/UnityCombatLab/Tests/EditMode/BattleRunnerTests.cs)는 시간 누적·이벤트 소비·재진입을 검사합니다. [RunReplayTests](Assets/UnityCombatLab/Tests/EditMode/RunReplayTests.cs)는 세 가지 프레임/배속 조건의 고정 결과와 이벤트 누락·강화 증가량 변조를 검사합니다.

### 4. 이전 화면의 입력과 이전 Run의 상태를 다음 진행에 섞지 않는다

**문제.** 강화 버튼을 연속으로 누르거나 이전 화면의 콜백이 늦게 도착하면 같은 강화를 두 번 적용할 수 있습니다. 실행 중 상태를 ScriptableObject에 남기면 재도전이 이전 판의 HP·강화를 이어받을 수도 있습니다.

**선택.** [RunController](Assets/UnityCombatLab/Scripts/Application/Run/RunController.cs)가 현재 Run ID와 상태를 검사하고, [RunProgress](Assets/UnityCombatLab/Scripts/Domain/Run/RunProgress.cs)가 웨이브·제시 후보·선택 가능 여부를 확인합니다. 선택이 성공하면 상태와 웨이브가 바뀌므로 다음 입력은 같은 선택을 반복 적용할 수 없습니다.

```csharp
public bool TrySelectReward(Guid runId, int wave, Define.RewardKind? kind)
{
    if (!Matches(runId, Define.RunPhase.RewardSelection) || !Progress.TryChoose(wave, kind))
        return false;

    SetPhase(Define.RunPhase.WaveIntro);
    return true;
}
```

강화 후보는 명시적 seed의 [xorshift32 PRNG와 부분 Fisher–Yates 추출](Assets/UnityCombatLab/Scripts/Domain/Run/RewardRandom.cs)로 결정합니다. 선택 당시 증가량은 [RewardChoice](Assets/UnityCombatLab/Scripts/Domain/Run/RewardChoice.cs)에 저장해, 다음 웨이브의 보상 배율이 과거 효과를 다시 계산하지 않게 합니다.

편집 데이터와 실행 상태도 구분했습니다. [RunDefinition](Assets/UnityCombatLab/Scripts/Infrastructure/Balance/RunDefinition.cs)의 ScriptableObject 설정을 검증된 C# 데이터인 [RunGameData](Assets/UnityCombatLab/Scripts/Domain/Run/RunGameData.cs)로 변환하고, 실제 HP·강화 횟수는 Run과 전투 객체가 소유합니다. [RunBattlePresenter](Assets/UnityCombatLab/Scripts/Presentation/UI/RunBattlePresenter.cs)는 강화 카드에 필요한 표시값을 [RewardCardDisplay](Assets/UnityCombatLab/Scripts/Presentation/ReadModels/RewardCardDisplay.cs)로 만들어 View에 전달합니다.

**확인하는 테스트:** [RunProgressTests](Assets/UnityCombatLab/Tests/EditMode/RunProgressTests.cs)는 후보·중복 선택을, [RunControllerTests](Assets/UnityCombatLab/Tests/EditMode/RunControllerTests.cs)는 상태 전이와 재시작 초기화를 검사합니다. [RunBattleCoordinatorTests](Assets/UnityCombatLab/Tests/EditMode/RunBattleCoordinatorTests.cs)는 화면 없이 5웨이브 진행, 포커스 복귀, 연출 완료 대기를 검사합니다.

## 이 문제들을 나눈 코드 구조

전투 규칙을 바꾸는 작업, 화면의 명중 타이밍을 바꾸는 작업, 자산 로딩 방식을 바꾸는 작업의 수정 범위를 구분했습니다. 이 책임 분리를 **Clean Architecture Lite + MVP/Passive View**로 구성하고, 자주 바뀌는 HUD 값에 R3를 적용했습니다. 클릭·화면 전환은 C# 이벤트와 명시적 메서드 호출로 처리합니다.

아래 화살표는 주요 코드 의존 방향입니다. Common의 enum·고정 규약 참조는 생략했습니다.

```mermaid
flowchart TD
    B[Bootstrap / VContainer] --> P[Presentation / View · Presenter]
    B --> A[Application / Run · BattleRunner · Ports]
    B --> I[Infrastructure / Assets · Scenes · Data]
    P --> A
    P --> D[Domain / Combat · Rewards · Replay]
    I --> A
    I --> D
    A --> D
```

[Domain asmdef](Assets/UnityCombatLab/Scripts/Domain/UnityCombatLab.Domain.asmdef)는 `noEngineReferences: true`이며 Common만 참조합니다. 전투 규칙과 replay는 Unity 화면 없이 실행할 수 있습니다. [Application](Assets/UnityCombatLab/Scripts/Application/UnityCombatLab.Application.asmdef)도 UnityEngine을 참조하지 않지만 진행·비동기 흐름·읽기 모델에 UniTask와 R3를 사용합니다.

웨이브 진행 정책은 [RunBattleCoordinator](Assets/UnityCombatLab/Scripts/Application/Run/RunBattleCoordinator.cs)에 두고, [RunBattleEntryPoint](Assets/UnityCombatLab/Scripts/Bootstrap/RunBattleEntryPoint.cs)는 Unity 객체 생성과 프레임·포커스 입력을 연결합니다. 덕분에 다음 웨이브로 넘어가는 조건을 검사할 때 씬과 캐릭터를 준비할 필요가 없습니다.

**VContainer는 구체 구현을 조립하는 위치에 사용합니다.** [AppLifetimeScope.Configure](Assets/UnityCombatLab/Scripts/Bootstrap/AppLifetimeScope.cs)에서 흐름 조정 객체에 씬 전환, 자산 생성 함수, 프레임 대기와 오류 보고를 연결합니다.

```csharp
builder.Register(resolver => new RunFlowCoordinator
(resolver.Resolve<SceneTransitionManager>(),
    loading,
    () => new RunAssetScope(resolver.Resolve<AddressableAssetManager>(), _uiAssets, _unitAssets, _runDefinition),
    () => UniTask.NextFrame(),
    Debug.LogException,
    value => AudioListener.volume = value), Lifetime.Singleton);
builder.RegisterEntryPoint<AppEntryPoint>().AsSelf();
```

테스트에서는 이 연결을 대역으로 바꿔 로드 완료와 프레임 대기 시점을 직접 제어합니다. Domain은 컨테이너를 조회하지 않습니다. VContainer의 씬 Scope와 Run 자산의 수명은 별도로 관리하며, 앞의 수명 표처럼 한 판 동안 유지할 자산과 웨이브마다 교체할 전투 객체를 구분합니다.

자료구조와 상태 표현은 실제 크기에 맞췄습니다. [BattleSimulation.FindTarget](Assets/UnityCombatLab/Scripts/Domain/Battle/BattleSimulation.cs)은 최대 3개 적 슬롯을 정해진 순서로 검사하고, Unit ID로 View를 찾는 곳에는 Dictionary를 사용합니다. `RunController`의 상태는 enum과 허용 전이 검사로 표현합니다.

## 사용 환경과 라이브러리

버전은 이 프로젝트에서 확인한 설치 버전입니다. UPM 구성은 [manifest.json](Packages/manifest.json)과 [packages-lock.json](Packages/packages-lock.json), NuGet 구성은 [packages.config](Assets/packages.config)에 있습니다.

| 기술 | 버전 | 실제 사용처와 선택 이유 |
|---|---|---|
| Unity / C# | **6000.3.8f1** | 씬·프리팹·Animator와 게임 클라이언트 구현. [ProjectVersion](ProjectSettings/ProjectVersion.txt) |
| Universal Render Pipeline | **17.3.0** | 3D 전투 무대와 캐릭터 렌더링. [렌더링 설정](Assets/UnityCombatLab/Settings) |
| uGUI / TextMeshPro | **uGUI 2.0.0에 포함** | 명시적으로 연결하는 View, 일본어 UI와 숫자 표시. [UI 코드](Assets/UnityCombatLab/Scripts/Presentation/UI) |
| VContainer | **1.19.0** | 앱·씬의 객체 조립과 EntryPoint 연결. 컨테이너 접근을 [Bootstrap](Assets/UnityCombatLab/Scripts/Bootstrap)에 모음 |
| UniTask | **2.5.11** | 자산 준비, 씬 전환, 다음 프레임 대기, 취소 처리. [RunFlowCoordinator](Assets/UnityCombatLab/Scripts/Application/Run/RunFlowCoordinator.cs) |
| R3 | **1.3.1** — core + Unity 연동 | HP·방어막·예고 HP의 변경 구독과 해제. [BattleHudPresenter](Assets/UnityCombatLab/Scripts/Presentation/UI/BattleHudPresenter.cs) |
| DOTween Standard | **1.3.045** | UI 팝업, 버튼 반응, 이동·피격·피해 숫자 연출. [UiTweenUtility](Assets/UnityCombatLab/Scripts/Presentation/UI/UiTweenUtility.cs), [UnitView](Assets/UnityCombatLab/Scripts/Presentation/Units/UnitView.cs) |
| Addressables | **2.8.1** | 로컬 유닛 정의·프리팹·UI·아이콘 로딩. [RunAssetScope](Assets/UnityCombatLab/Scripts/Infrastructure/Assets/RunAssetScope.cs) |
| Input System | **1.18.0** | EventSystem의 UI 입력과 Esc 입력. [RunBattleView](Assets/UnityCombatLab/Scripts/Presentation/UI/RunBattleView.cs) |
| Unity Test Framework / NUnit | **1.6.0 패키지** | 전투·진행의 EditMode 테스트와 Unity 객체·자산 수명의 PlayMode 테스트. [Tests](Assets/UnityCombatLab/Tests) |
| NuGetForUnity | **4.5.0** | R3 core 및 관련 .NET 의존성 복원 |

개발 환경은 Rider **2026.2.3.1** / Unity Rider Editor **3.0.39**, Unity CLI **1.0.0-beta.14** / Pipeline **0.9.1-exp.1**입니다. CLI·MCP는 에디터 작업과 검증에 사용했습니다.

## 테스트와 재현 검증

**2026-10-26 Unity Editor 검증 결과**입니다.

| 검증 | 결과 |
|---|---|
| EditMode | **110 / 110 통과** |
| PlayMode | **44 / 44 통과** |
| 실제 Editor 플레이 | 표준 난이도 5/5웨이브 승리, 누적 **1090tick**, **455이벤트** |
| 해당 플레이의 replay | **MATCH**, 예상/실제 해시 `3F80450CE314E9DB` 일치 |
| 실행 환경 | Unity 6000.3.8f1, 1920×1080, Addressables Asset Database / Fast Mode |

실제 플레이의 1090tick/455이벤트와 테스트 전용 고정 입력의 386tick/152이벤트는 서로 다른 입력에 대한 결과입니다.

테스트 코드는 위 사례마다 연결했습니다. 값의 계산뿐 아니라 표시 순서, 늦은 비동기 완료, 중복 입력과 재시작을 함께 검사하도록 구성했습니다.

## 이후 개발 계획

- **밸런스 도구 연동:** 직접 개발한 **Fulcrum**의 데이터를 게임에 연동할 예정입니다. Fulcrum은 Unity Editor에서 **커브 기반 게임 밸런스 데이터를 작성하고 시뮬레이션·검증하는 도구**이며, 개인 비공개 저장소에서 관리하고 있습니다.
- **사운드 추가:** BGM과 전투·UI 효과음을 추가할 예정입니다. 현재 데모는 사운드 없이 동작합니다.
- **로직 개선:** 실제 사용 흐름과 테스트를 바탕으로 기존 로직을 다시 검토하고, 필요한 부분을 리팩토링·수정할 예정입니다.

## 프로젝트 실행과 에셋 복원

구매 에셋은 라이선스에 따라 저장소에서 제외했습니다. Unity에서 실행하려면 모델·텍스처·애니메이션·GUI·폰트와 프로젝트용 파생 재질/VFX를 복원해야 합니다.

로컬 개발 환경은 다음 순서로 구성합니다.

1. Unity **6000.3.8f1**로 이 저장소 루트를 엽니다. UPM 패키지는 manifest/lock의 버전을 기준으로 복원합니다.
2. `NuGet > Restore Packages`로 [packages.config](Assets/packages.config)의 R3 core와 의존성을 복원합니다. 생성되는 `Assets/Packages/`는 Git에서 제외합니다.
3. DOTween Standard **1.3.045**를 공식 배포본에서 복원하고 `Tools > Demigiant > DOTween Utility Panel`의 설정을 적용합니다. 배포본과 고지는 보존하며 `Assets/Plugins/Demigiant/`는 공개 저장소에 넣지 않습니다.
4. `Window > TextMeshPro > Import TMP Essential Resources`로 TMP 리소스를 복원합니다.
5. 아래 외부 에셋을 보유한 경우 원래 경로·GUID를 유지해 직접 import합니다. 구매 에셋을 다른 사람에게 복사하는 방식은 사용하지 않습니다.
6. 구매 팩 import 후에는 프로젝트용 URP 파생 재질과 VFX wrapper를 별도로 구성하고, 자체 유닛·UI prefab과 자산 카탈로그의 참조를 연결합니다.
7. 자산·참조 복원 후 [Boot.unity](Assets/UnityCombatLab/Scenes/Boot.unity)에서 시작합니다. Dungeon 직접 실행은 활성 Run을 요구합니다. 씬 순서는 [Define.SceneType](Assets/UnityCombatLab/Scripts/Common/Define.cs)의 `Boot=0 / Dungeon=1 / Frontend=2`와 일치해야 합니다.

검증 시에는 Unity Test Runner의 EditMode·PlayMode에서 `UnityCombatLab.Tests`를 실행합니다. 자산 참조를 사용하는 테스트는 위 복원이 필요합니다. 전투 종료 후 Play 모드를 유지한 상태에서 `UnityCombatLab > Development > Verify Last Run`을 실행하면 직전 완료 Run의 재현 결과를 Console에서 확인할 수 있습니다.

## 제작 범위와 외부 리소스

자체 구현 범위는 전투·강화·진행 규칙, UI/연출 연결, 비동기·자산 수명 관리, 테스트와 replay 검증, 프로젝트 전용 prefab·씬 구성입니다. 화면에 사용한 캐릭터·배경·GUI·VFX 원본은 다음 외부 리소스입니다.

| 리소스 | 공급자 / 용도 | 저장소에서의 취급 |
|---|---|---|
| KayKit Adventurers / Skeletons / Dungeon Remastered | Kay Lousberg / 캐릭터·장비·애니메이션·무대 | `Assets/KayKit/` 원본 제외 |
| GUI Pro - Minimal Game Light | LAYERLAB / UI 그래픽·아이콘, LTAvocado Bold·Afacad Flux ExtraBold 폰트 참조 | `Assets/Layer Lab/` 원본 제외 |
| [Epic Toon FX](https://assetstore.unity.com/packages/vfx/particles/epic-toon-fx-57772) | Archanor VFX / 전투 효과 | `Assets/Epic Toon FX/` 원본 제외 |
| 프로젝트용 파생 재질·VFX wrapper | 위 구매 자산을 사용하는 로컬 구성 | `Assets/UnityCombatLab/Art/Restricted/` 제외 |
| Zen Maru Gothic Bold | Zen Maru Gothic Project Authors / 일본어 UI | 원본 폰트와 생성 TMP atlas, [NOTICE](Assets/UnityCombatLab/Art/Fonts/ZenMaruGothic/NOTICE.txt)·[OFL](Assets/UnityCombatLab/Art/Fonts/ZenMaruGothic/OFL.txt) 포함 |

공개 제외 경로는 [.gitignore](.gitignore)에 명시했습니다. 자체 wrapper는 외부 리소스를 참조하므로 해당 원본과 프로젝트용 파생 자산을 함께 복원해야 합니다.

개발 과정에는 Codex와 Unity CLI/MCP를 활용한 구현 초안 작성·에디터 작업·검증 자동화가 포함됩니다. 설계와 범위 결정, 코드 리뷰·수정, 플레이 확인을 거쳐 반영했습니다.
