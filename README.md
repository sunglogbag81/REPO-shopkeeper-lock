# REPOShopkeeperLock

BepInEx 5 + Harmony 기반 R.E.P.O. 모드입니다.

목표: 상점 주인(`ShopKeeper`)과 관련 감지 컴포넌트가 비활성화되거나 bool 상태가 꺼지는 경우 주기적으로 다시 켜서, 상점 주인을 계속 활성 상태로 유지합니다.

## 0.2.0 변경점

- 제공된 `Assembly-CSharp.dll` 정적 분석 결과를 반영했습니다.
- 실제 확인된 타입을 우선 대상으로 사용합니다.
  - `ShopKeeper`
  - `shopkeeperDetectionSphere`
- 기존처럼 `shop`이 들어간 모든 타입을 건드리던 넓은 스캔은 기본 비활성화했습니다.
  - `ShopManager`, `ShopCostUI`, `ShopKeycard` 같은 관련 없는 상점 시스템을 잘못 수정할 가능성을 줄였습니다.
- BepInEx config를 추가했습니다.
  - 스캔 주기 조절
  - 넓은 shop fallback 스캔 on/off
  - 변경 로그 on/off
- 기본 스캔 주기를 `1.0초`에서 `0.5초`로 낮춰 더 빨리 되살립니다.

## 현재 방식

런타임에서 다음 타입의 `MonoBehaviour`를 찾습니다.

1. 정확히 확인된 타입명:
   - `ShopKeeper`
   - `shopkeeperDetectionSphere`
2. fallback 타입명:
   - `shopkeeper`
   - `shop keeper`
   - `shopowner`
   - `shop owner`
3. 선택적 broad fallback:
   - `shop`
   - 기본값은 꺼져 있습니다.

대상 컴포넌트에 대해:

- 관련 GameObject가 꺼져 있으면 `SetActive(true)`
- Behaviour가 꺼져 있으면 `enabled = true`
- bool 필드/프로퍼티 중 `enabled`, `active`, `spawned`, `present` 류는 `true`
- `disabled`, `despawn`, `hide`, `hidden`, `off` 류는 `false`

로 강제합니다.

## 설정

처음 실행하면 BepInEx config 파일이 생성됩니다.

```text
BepInEx/config/gampa.repo.shopkeeperlock.cfg
```

주요 옵션:

```ini
[General]

## How often to re-force shopkeeper state. Lower is more aggressive; higher is cheaper.
# Setting type: Single
# Default value: 0.5
ScanIntervalSeconds = 0.5

## Also scan every MonoBehaviour with 'shop' in its type name. Disabled by default to avoid touching unrelated shop systems.
# Setting type: Boolean
# Default value: false
IncludeBroadShopFallback = false

## Log only when the mod actually changes an object/member state.
# Setting type: Boolean
# Default value: true
LogChanges = true
```

상점 주인이 여전히 꺼진다면 `IncludeBroadShopFallback = true`를 시험해볼 수 있습니다. 다만 이 옵션은 다른 상점 관련 시스템까지 건드릴 수 있으니 문제 생기면 다시 끄세요.

## 빌드

이 저장소는 .NET SDK가 있는 환경에서 빌드합니다.

BepInEx 패키지는 `nuget.org`가 아니라 BepInEx NuGet feed에 있으므로, 저장소의 `NuGet.config`를 같이 사용합니다.

```bash
dotnet restore --configfile NuGet.config
dotnet build -c Release --no-restore
```

결과 DLL:

```text
bin/Release/netstandard2.1/REPOShopkeeperLock.dll
```

이 파일을:

```text
R.E.P.O./BepInEx/plugins/REPOShopkeeperLock.dll
```

에 넣으면 됩니다.

## 다음 개선 후보

정확한 `ShopKeeper` 메서드 동작까지 디컴파일할 수 있으면, 주기적 reflection 강제 대신 Harmony patch로 특정 despawn/disable 루틴을 직접 막는 방식이 더 안정적입니다.
