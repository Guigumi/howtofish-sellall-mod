# Inspection notes — 2026-09-23
GameDir: /home/guigumi/.steam/steam/steamapps/common/How to Fish/How to Fish
Assembly-CSharp.dll: presente (870400 bytes, 2026-09-22)
FishNet.Runtime.dll: presente (801792 bytes)
Toolchain: .NET SDK 10.0.401 ($HOME/.dotnet), ilspycmd 11.0.0.9375
Nota: `ilspycmd -l` do brief exige tipo de entidade; comando correto é `ilspycmd -l c <dll>`.
Nota: `ilspycmd` só roda com `DOTNET_ROOT=$HOME/.dotnet` exportado.

## Item (Assembly-CSharp, sem namespace: `class Item`)
- valor: campo privado `_worth` (int); exposto via `DefaultWorth => _worth` e
  `TotalWorth => (int)(_worth * RandomizedWeight * CooknessWorthCurve * betting * killScore)` (int).
  Fallbacks do plano (price/value/sellPrice) NÃO existem — usar `TotalWorth` (valor real de venda)
  com fallback para `DefaultWorth`. Testar via reflexão nessa ordem.
- flags de posse/inventário (todos confirmados no dump):
  - `HasPlayerHolder` (bool, getter: `_holder ? true : (bool)_syncedHolder.Value`)
  - `Holder` (Player `_holder`), `SyncedHolder` (Player), `LastHolder` (Player, usado por `SellItem` p/ som)
  - `IsInInventory` (bool, `{ get; private set; }`)
  - `HasBeenHeld` (bool, `{ get; private set; }`)
  - Definição de "no chão": `!HasPlayerHolder && !IsInInventory && isActiveAndEnabled`
- venda nativa: método `Sell()` na classe `Item` AUSENTE (grep `Sell` em `Item` só acha
  `AchievementManager.CheckSellWorthAchievement`). Rota nativa é `MoneyManager.SellItem(Item)`
  (server-side, já soma `TotalWorth` + toca som). Verificado corpo abaixo.

## MoneyManager / PlayerUI (Assembly-CSharp, sem namespace)
- `MoneyManager : NetworkBehaviour`, singleton `public static MoneyManager Instance`
- dinheiro: `public readonly SyncVar<int> _money`, espelho local `public static int Money { get; private set; }`
- crédito (assinaturas exatas):
  - `public static void SellItem(Item item)` — soma `item.TotalWorth` em `_money.Value`,
    só executa `if ((bool)Instance && Instance.IsServerInitialized)`; chama
    `ObserverMoneySound(true, item.LastHolder)` + `MoneySound(true, item.LastHolder)`.
    → ROTA NATIVA PREFERIDA para Tasks 4–5 (passar o próprio `Item`, sem somar manualmente).
  - `public static void AddMoney(int amount, Player player)` — soma `Mathf.Abs(amount)`, sons p/ `player`
  - `public static void RemoveMoney(int amount, Player player)` — clampa `[0, int.MaxValue]`
- `MoneySound(bool increase, Player player)` existe: sim (private instance; toca `Sell_0` via AudioManager)
- `ObserverMoneySound(bool increase, Player player)` existe: sim (private, Rpc Observers → chama MoneySound)
- `PlayerUI.SetMoney(int to, int diff, bool gainedMoney)` existe: sim (public static; delega p/
  `_instance._moneyUI.SetMoney`). Chamado automaticamente por `MoneyManager.OnChangeMoney(prev, next, asServer)`,
  então crédito via `SellItem`/`AddMoney` já atualiza o HUD sem chamada manual.

## FishNet (FishNet.Runtime.dll)
- `FishNet.Object.NetworkObject.Despawn()` existe: sim — overloads:
  - `public void Despawn(GameObject go, DespawnType? despawnType = null)` (no NetworkManager/ServerManager)
  - `public void Despawn(NetworkObject nob, DespawnType? despawnType = null)`
  - `public void Despawn(DespawnType? despawnType = null)` (instance no próprio NetworkObject — preferida: `nob.Despawn()`)
  - `Spawn(...)` análogos existem.
- check server (ambos existem; usar nessa ordem de fallback):
  1. `FishNet.Object.NetworkBehaviour.IsServer` / `IsServerInitialized` (prop bool; `Item`/`MoneyManager` herdam)
  2. `FishNet.Managing.NetworkManager.IsServer` / `IsServerStarted` (`IsServer => IsServerStarted => ServerManager.Started`)
  3. `FishNet.InstanceFinder.NetworkManager` / `.ServerManager` (static accessors; `IsServer` está no NetworkManager, não no InstanceFinder direto)
- `MoneyManager.SellItem` já faz gate `Instance.IsServerInitialized` — Tasks 4–5 devem checar
  `IsServer` antes de chamar e avisar cliente ("apenas host").

## Comandos de verificação usados
- `ilspycmd -l c Assembly-CSharp.dll | grep -iE 'Item|Creature|MoneyManager|PlayerUI|Shop|Server'`
- `ilspycmd -t Item Assembly-CSharp.dll | grep -iE 'worth|price|value|sell|...'` (+ holder/inventory/sell)
- `ilspycmd -t MoneyManager Assembly-CSharp.dll | grep -iE 'money|add|set|sell|sound|SyncVar|_money'`
- `ilspycmd -t FishNet.Object.NetworkObject FishNet.Runtime.dll | grep -iE 'despawn|spawn'`
- `ilspycmd -t FishNet.InstanceFinder ...`, `ilspycmd -t FishNet.Managing.NetworkManager ...`,
  `ilspycmd -t FishNet.Object.NetworkBehaviour ...`, `ilspycmd -t PlayerUI ...`
