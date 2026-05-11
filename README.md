# REPOShopkeeperLock

BepInEx 5 + Harmony 기반 R.E.P.O. 모드 템플릿.

목표: 새로 추가된 상점 주인을 항상 켜진 상태로 유지하고, 꺼지는 값/오브젝트/Behaviour를 주기적으로 다시 켭니다.

## 현재 방식

게임 내부 클래스명이 아직 확정되지 않았다는 전제로, 런타임에서 이름에 `shop`, `shopkeeper`, `shopowner` 등이 들어간 `MonoBehaviour`를 찾고:

- 관련 GameObject가 꺼져 있으면 `SetActive(true)`
- Behaviour가 꺼져 있으면 `enabled = true`
- bool 필드/프로퍼티 중 `enabled`, `active`, `spawned`, `present` 류는 `true`
- `disabled`, `despawn`, `hide`, `off` 류는 `false`

로 강제합니다.

## 빌드

이 환경에는 `dotnet`이 없어서 여기서 빌드는 못 했습니다.
로컬 PC에서 .NET SDK 설치 후:

```bash
dotnet restore
dotnet build -c Release
```

결과 DLL:

```text
REPOShopkeeperLock/bin/Release/netstandard2.1/REPOShopkeeperLock.dll
```

이 파일을:

```text
R.E.P.O./BepInEx/plugins/REPOShopkeeperLock.dll
```

에 넣으면 됩니다.

## 다음 개선

정확한 상점 주인 클래스/설정 이름을 알면 reflection 루프 대신 Harmony patch로 바꾸는 게 더 안정적입니다.
`R.E.P.O._Data/Managed/Assembly-CSharp.dll`에서 `Shopkeeper`, `ShopOwner`, `Shop` 관련 클래스명을 확인해서 알려주면 바로 고정 패치로 바꿀 수 있습니다.
