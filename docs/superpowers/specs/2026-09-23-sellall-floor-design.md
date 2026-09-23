# SellAll Floor Fix — Design Spec (How to Fish)

**Data:** 2026-09-23
**Jogo:** How to Fish (Dazed Games, Steam App, Unity Mono + FishNet P2P)
**Autor:** guigumi
**Status:** Aprovado (abordagem A — BepInEx SellAll com tecla)

## 1. Problema

Em sessões multiplayer de How to Fish, itens dropados no chão (peixes, loot pós-morte, itens roubados por gaivotas e largados) acumulam e bugam — passam a existir como `GameObject` + `NetworkObject` sincronizados que nunca despawnam. Com dezenas/centenas de objetos com física + sync FishNet, o host tem spike de CPU/rede e o lobby laga.

## 2. Objetivo

Um mod BepInEx que, ao apertar uma tecla (default `F9`), vende todos os itens vendáveis largados no chão da ilha atual e entrega o dinheiro para quem apertou, despawnando os objetos no server para destravar o lag imediatamente.

Sucesso = apertar F9 com 50+ itens no chão → dinheiro creditado correto + objetos somem + FPS/rede normalizam + sem erro no `LogOutput.log`.

## 3. Restrições (valores exatos)

- Jogo: build Windows via Proton em Linux. Path confirmado: `/home/guigumi/.steam/steam/steamapps/common/How to Fish/How to Fish/` com `How to Fish.exe`, `How to Fish_Data/Managed/Assembly-CSharp.dll`, `FishNet.Runtime.dll`, `UnityPlayer.dll`.
- Loader: `BepInEx 5.4.23.5` (`BepInExPack` do Thunderstore c/ categoria How to Fish). Não BepInEx 6, não MelonLoader.
- SDK: `.NET SDK 10 ou newer` (exigência do template `htfmod`).
- Template: `ModdingCommunity.HowToFish-BepInExTemplate` (`dotnet new htfmod`).
- Rede: FishNet server-authoritative. Apenas o server pode dar `Despawn` válido e alterar `SyncVar _money` sem desync.
- Compatibilidade: todos do lobby precisam da mesma versão do `.dll` (padrão FishNet/Thunderstore). v1 é host-only funcional; cliente sem autoridade recebe aviso.
- Linux/Proton: BepInEx Windows via Proton exige `WINEDLLOVERRIDES="winhttp=n,b" %command%` nas Launch Options, idealmente via `r2modman` (AppImage/flatpak).

## 4. Arquitetura

Um único plugin BepInEx 5 (`Guigumi.SellAllFloorFix.dll`) em `BepInEx/plugins/`, sem assets Unity, sem servidor dedicado.

Componentes (um arquivo C# cada, responsabilidade única):

1. `Plugin.cs` — `BaseUnityPlugin`, registra config, escuta tecla no `Update()`, orquestra `TrySellAll()` com `try/catch` + log.
2. `SellAllConfig.cs` — wrappers tipados sobre `ConfigEntry`: `Tecla` (`KeyboardShortcut F9`), `SoVendaveis` (`true`), `IncluirSemValor` (`false` = só limpa visual se `true`), `CooldownSeg` (`0.5`), `LimitePorFrame` (`100`).
3. `ItemCollector.cs` — `FindObjectsOfType<MonoBehaviour>()` + reflexão para achar tipo `Item`/`Creature` sem quebrar com rename de patch: procura classe chamada `Item` em `Assembly-CSharp`, filtra `isActiveAndEnabled && !held && !inInventory`, lê `Worth`/`price`/`value`/`sellPrice` via reflexão, retorna lista `(GameObject go, int worth, object component)`.
4. `Seller.cs` — tenta rota nativa primeiro (`Sell()`/`SellItem()`/`CmdSell` via reflexão se existir); senão soma `worth` e credita via `MoneyManager` + `PlayerUI.SetMoney` + `MoneySound` (rota provada pelo trainer `how-to-fish-trainer` F6).
5. `Despawner.cs` — só executa se `isServer`: chama `NetworkObject.Despawn()` / `ServerManager.Despawn(go)` via reflexão sobre `FishNet.Runtime.dll`; despawna em batches de `LimitePorFrame` usando coroutine para não congelar.
6. Feedback — texto HUD `+ $X (N itens)` via `PlayerUI.SetMoney` quando disponível + `Logger.LogInfo` sempre.

## 5. Data flow

```
[F9] -> Plugin.Update detecta KeyboardShortcut.IsDown
  -> Cooldown check (0.5s)
  -> IsServer? (checa InstanceFinder.IsServer / NetworkManager.IsServer via reflexão)
    -> NÃO: Chat/Log "Apenas o host pode vender (v1). Peça ao host para apertar F9." + return
    -> SIM:
      -> ItemCollector.Collect() -> List<(go, worth)>
      -> Filtra worth>0 (se SoVendaveis)
      -> Seller.Credit(total) para jogador local que apertou
      -> Despawner.DespawnBatch(lista) no server
      -> Feedback HUD + Log "SellAll: N itens, $X"
```

Dinheiro vai integralmente para quem apertou a tecla quando ele é o host (requisito aprovado: "Tudo vendável, pra mim"). Sem divisão.

## 6. Casos de borda e erros

- 0 itens: log `SellAll: nada para vender` + return, sem tocar em dinheiro.
- Item sem `NetworkObject` ou já despawnado: pula + `LogDebug`, não conta no total.
- Falha em `Despawn` de um item: não credita aquele `worth` (evita duplicar dinheiro), continua os demais, loga warning com nome do objeto.
- Exceção inesperada em `TrySellAll`: `catch` global, `LogError` com stack, sem crash do jogo.
- Spam de tecla: cooldown bloqueia reentrância; flag `isRunning` impede overlap.
- Cliente aperta: sem efeito colateral, só aviso. Nenhum `Despawn` client-side (evita objeto fantasma que volta no resync).

## 7. Teste

- T1 boot: jogo abre com BepInEx console sem erro vermelho, `LogOutput.log` contém `SellAllFloorFix v1.0.0 loaded`.
- T2 funcional: dropa 50 peixes no chão (morre 2-3x / pesca e larga), aperta F9 como host → dinheiro sobe pelo somatório exato, chão limpa, sem erro no log.
- T3 cliente: cliente aperta F9 → aviso, nada vende; host aperta → vende.
- T4 stress: 150+ itens, batch 100/frame, sem freeze >200ms.
- T5 compatibilidade: host + 1 cliente com mesma versão, sem desync após venda.

## 8. Fora de escopo (YAGNI)

- Divisão de dinheiro, botão na loja, comando `/sellall` no chat, venda automática por threshold, suporte a cliente vender sem host, suporte a itens de mods terceiros com `Worth` custom fora do padrão (serão vendidos se expuserem campo padrão, senão pulados).
- v2 futuro (não nesta spec): broadcast FishNet cliente→host pedindo execução.

## 9. Riscos

- Patch do jogo renomeia `Item.Worth`/`MoneyManager`: mitigado com reflexão + fallback de nomes (`Worth|price|value|sellPrice`) e log explícito quando nada achado.
- `Assembly-CSharp.dll` precisa ser inspecionado com dnSpy/ILSpy antes de fechar nomes — Task 1 do plano cobre isso.
- Sem `dotnet` instalado no Linux atual (confirmado `dotnet: comando não encontrado` em 2026-09-23): Task 1 instala SDK 10.
