# SellAll Floor Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Criar o mod BepInEx `Guigumi.SellAllFloorFix` que vende com F9 todos os itens vendáveis do chão e despawna no server.

**Architecture:** Plugin único BepInEx 5 com reflexão tolerante a rename (`Item`/`Worth`/`MoneyManager`/`Despawn` resolvidos em runtime); execução somente no server, resto é aviso.

**Tech Stack:** C#, BepInEx 5.4.23.5, .NET SDK 10+, Unity Mono (Assembly-CSharp), FishNet.Runtime, r2modman, Proton (WINEDLLOVERRIDES winhttp).

**Spec:** `/var/home/guigumi/howtofish-sellall-mod/docs/superpowers/specs/2026-09-23-sellall-floor-design.md`

## Global Constraints

- Loader `BepInEx 5.4.23.5` via `BepInExPack` do Thunderstore (categoria How to Fish). Não usar BepInEx 6 nem MelonLoader.
- SDK `.NET SDK 10 ou newer` (exigência do template `htfmod`).
- Template `ModdingCommunity.HowToFish-BepInExTemplate` (`dotnet new htfmod`).
- Jogo em `/home/guigumi/.steam/steam/steamapps/common/How to Fish/How to Fish/` com `How to Fish.exe`, `How to Fish_Data/Managed/Assembly-CSharp.dll`, `FishNet.Runtime.dll`.
- Apenas o server executa `Despawn` e crédito; cliente recebe aviso e nada faz.
- Todos do lobby usam a mesma versão do `.dll`.
- Linux/Proton: Launch Options `WINEDLLOVERRIDES="winhttp=n,b" %command%`.
- Tecla default `F9`, cooldown `0.5s`, batch `100/frame`, `SoVendaveis=true`.

---

## File Structure

```
howtofish-sellall-mod/
  docs/superpowers/specs/2026-09-23-sellall-floor-design.md (existe)
  docs/superpowers/plans/2026-09-23-sellall-floor-fix.md (este arquivo)
  src/SellAllFloorFix/
    SellAllFloorFix.csproj (gerado pelo htfmod, depois editado p/ GameDir)
    Plugin.cs (orquestra: config + Update + TrySellAll)
    SellAllConfig.cs (wrappers de ConfigEntry)
    ItemCollector.cs (coleta via reflexão, retorna List<GroundItem>)
    Seller.cs (crédito via reflexão MoneyManager/PlayerUI)
    Despawner.cs (IsServer check + Despawn via reflexão FishNet)
```

Cada arquivo tem uma responsabilidade. `Plugin.cs` não conhece nomes de campos do jogo; `ItemCollector`/`Seller`/`Despawner` isolam toda reflexão. `GroundItem` é o contrato entre eles: `(GameObject Go, int Worth, object Component, object NetObj)`.

---

### Task 1: Ambiente + inspeção da DLL (descobrir nomes reais)

**Files:**
- Modify: `~/.bashrc` (export DOTNET_ROOT/PATH — via shell, ver passo 2)
- Create: `/var/home/guigumi/howtofish-sellall-mod/src/inspection-notes.md`
- Test: `dotnet --version` imprime `10.x`; `ilspycmd` lista tipos `Item`, `MoneyManager`

**Interfaces:**
- Consumes: nada (primeira task)
- Produces: `inspection-notes.md` com nomes exatos consumidos pelas Tasks 4–5 (nomes de classe, campo de valor, método de venda, método de despawn, check IsServer)

- [ ] **Step 1: Instalar .NET SDK 10 via dotnet-install (funciona em ostree/Bazzite sem layering)**

Run:
```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
"$HOME/.dotnet/dotnet" --version
```
Expected: imprime versão `10.0.x`. Se falhar rede, abortar e reportar.

- [ ] **Step 2: Exportar PATH persistente e verificar**

Run:
```bash
grep -q '.dotnet' "$HOME/.bashrc" || echo 'export DOTNET_ROOT="$HOME/.dotnet"' >> "$HOME/.bashrc"
grep -q '.dotnet.*:$PATH' "$HOME/.bashrc" || echo 'export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"' >> "$HOME/.bashrc"
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
dotnet --version
```
Expected: `10.0.x`.

- [ ] **Step 3: Instalar ilspycmd e despejar tipos do jogo**

Run:
```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
dotnet tool install -g ilspycmd || dotnet tool update -g ilspycmd
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
GAMEDLL="$HOME/.steam/steam/steamapps/common/How to Fish/How to Fish/How to Fish_Data/Managed/Assembly-CSharp.dll"
ls -la "$GAMEDLL"
ilspycmd -l "$GAMEDLL" | grep -iE 'class (Item|Creature|MoneyManager|PlayerUI|Shop|Server)\b' | head -n 40
```
Expected: lista contendo `Item` e `MoneyManager` (nomes podem ter namespace; anotar exatamente como aparecem).

- [ ] **Step 4: Extrair membros de Item (campo de valor) e MoneyManager (método de dinheiro)**

Run:
```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
GAMEDLL="$HOME/.steam/steam/steamapps/common/How to Fish/How to Fish/How to Fish_Data/Managed/Assembly-CSharp.dll"
ilspycmd -t Item "$GAMEDLL" | grep -iE 'worth|price|value|sell|canSell' | head -n 40
echo '---MoneyManager---'
ilspycmd -t MoneyManager "$GAMEDLL" | grep -iE 'money|add|set|sell|sound|SyncVar|_money' | head -n 60
```
Expected: saída mostra campo de valor (ex: `Worth`) e método de crédito. Se `ilspycmd -t` não achar por namespace, rodar `ilspycmd "$GAMEDLL" > /tmp/dump.cs` e `grep -n -iE 'class Item\b|class MoneyManager\b' /tmp/dump.cs`, depois ler o bloco da classe.

- [ ] **Step 5: Extrair Despawn/IsServer do FishNet e PlayerUI.SetMoney**

Run:
```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
GAMEDLL="$HOME/.steam/steam/steamapps/common/How to Fish/How to Fish/How to Fish_Data/Managed/Assembly-CSharp.dll"
FISHNET="$HOME/.steam/steam/steamapps/common/How to Fish/How to Fish/How to Fish_Data/Managed/FishNet.Runtime.dll"
ilspycmd -t NetworkObject "$FISHNET" | grep -iE 'despawn|spawn' | head -n 20
echo '---PlayerUI---'
ilspycmd -t PlayerUI "$GAMEDLL" | grep -iE 'setMoney|money' | head -n 20
```
Expected: confirma `Despawn()` e `SetMoney`. Anotar assinaturas exatas.

- [ ] **Step 6: Escrever inspection-notes.md com os nomes exatos**

Criar `/var/home/guigumi/howtofish-sellall-mod/src/inspection-notes.md` com este conteúdo preenchido com o que foi observado (exemplo abaixo usa os nomes esperados — trocar se o dump mostrar outro):

```markdown
# Inspection notes — 2026-09-23
GameDir: /home/guigumi/.steam/steam/steamapps/common/How to Fish/How to Fish
Assembly-CSharp.dll: presente

## Item
- class: `Item` (Assembly-CSharp, sem namespace)
- valor: campo/prop `Worth` (int); fallbacks testados: price, value, sellPrice
- flags: inspecionado `isHeld`/`inInventory`? anotar aqui: <preencher>
- venda nativa: método `Sell()`? anotar aqui: <preencher ou "ausente">

## MoneyManager / PlayerUI
- `MoneyManager`: método de crédito anotar aqui (ex: AddMoney(int))
- `PlayerUI.SetMoney(int)` existe: sim/não
- `MoneySound()` existe: sim/não

## FishNet
- `NetworkObject.Despawn()` existe: sim
- check server: `InstanceFinder.IsServer` / `NetworkManager.IsServer` — anotar qual existe
```

Verificação: `cat /var/home/guigumi/howtofish-sellall-mod/src/inspection-notes.md`.

- [ ] **Step 7: Commit**

```bash
git -C /var/home/guigumi/howtofish-sellall-mod init 2>/dev/null || true
git -C /var/home/guigumi/howtofish-sellall-mod add docs/superpowers/specs/2026-09-23-sellall-floor-design.md docs/superpowers/plans/2026-09-23-sellall-floor-fix.md src/inspection-notes.md 2>/dev/null || true
git -C /var/home/guigumi/howtofish-sellall-mod commit -m "docs: spec + plan + inspection notes sellall" 2>/dev/null || echo "nothing to commit or no git"
```
Expected: commit criado ou mensagem de nothing to commit. Não falhar a task por causa de git ausente.

---

### Task 2: Instalar BepInEx via r2modman + scaffold htfmod

**Files:**
- Create: `/var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/SellAllFloorFix.csproj` (via template)
- Create: `/var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/Plugin.cs` (placeholder do template, será substituído na Task 3)
- Test: `dotnet build -c Release` sucede

**Interfaces:**
- Consumes: `inspection-notes.md` (só para saber GameDir; não bloqueia scaffold)
- Produces: projeto compilável; `Plugin.PLUGIN_GUID = Guigumi.SellAllFloorFix` consumido por todas as tasks seguintes

- [ ] **Step 1: Baixar r2modman AppImage e marcar executável**

Run:
```bash
mkdir -p "$HOME/AppImages/r2modman"
cd "$HOME/AppImages/r2modman"
curl -sSL -o r2modman.AppImage "https://github.com/ebkr/r2modmanPlus/releases/latest/download/r2modman.AppImage" || echo "BAIXE MANUALMENTE em https://github.com/ebkr/r2modmanPlus/releases"
chmod +x r2modman.AppImage
ls -la
```
Expected: `r2modman.AppImage` existe e é executável. Se o download direto falhar (nome do asset mudou), baixar manualmente pelo navegador — não inventar URL.

- [ ] **Step 2: Configurar perfil How to Fish + BepInExPack (manual UI, verificável via pasta)**

Instruções (fazer no UI do r2modman):
1. Abrir `~/AppImages/r2modman/r2modman.AppImage`, `Select game` → `How to Fish`, `Select profile` → `Default`.
2. Aba `Online` → buscar `BepInExPack` → `Download with dependencies`.
3. Clicar `Start modded` uma vez até o menu principal, fechar o jogo (gera `BepInEx/` no perfil).

Verificação via shell:
```bash
find "$HOME/.config/r2modmanPlus-local" -maxdepth 4 -iname "*How*Fish*" 2>/dev/null | head
find "$HOME/.config/r2modmanPlus-local" -maxdepth 6 -name "BepInEx.Preloader.dll" 2>/dev/null | head
```
Expected: ao menos um caminho de perfil How to Fish e o Preloader existem. Anotar o caminho do perfil (será `R2PROFILE`, ex: `$HOME/.config/r2modmanPlus-local/HowToFish/profiles/Default`).

- [ ] **Step 3: Ajustar Launch Options da Steam para Proton carregar BepInEx**

No Steam → How to Fish → Properties → Launch Options, colar (substituindo R2PROFILE pelo caminho anotado):

```
WINEDLLOVERRIDES="winhttp=n,b" %command% --doorstop-enable true --doorstop-target "<R2PROFILE>/BepInEx/core/BepInEx.Preloader.dll" --r2profile "Default"
```

Se preferir jogar sem r2modman após instalar BepInEx manualmente em `How to Fish/BepInEx/`, usar só `WINEDLLOVERRIDES="winhttp=n,b" %command%`.
Verificação: abrir o jogo uma vez e checar que existe `BepInEx/LogOutput.log` no perfil ou na pasta do jogo com linha `BepInEx king`. Comando:

```bash
find "$HOME/.config/r2modmanPlus-local" "$HOME/.steam/steam/steamapps/common/How to Fish" -name "LogOutput.log" 2>/dev/null | head
```
Expected: ao menos um `LogOutput.log` encontrado.

- [ ] **Step 4: Instalar template htfmod e criar o projeto**

Run:
```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
dotnet new install ModdingCommunity.HowToFish-BepInExTemplate
mkdir -p /var/home/guigumi/howtofish-sellall-mod/src
dotnet new htfmod --output SellAllFloorFix --guid Guigumi.SellAllFloorFix --ts-team Guigumi --projectdir /var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix 2>&1 | head -n 30 || dotnet new htfmod --output /var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix --guid Guigumi.SellAllFloorFix --ts-team Guigumi 2>&1 | head -n 30
ls -R /var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix | head -n 40
```
Expected: pasta contém `.csproj` e `Plugin.cs`. Se as flags exatas diferirem, rodar `dotnet new htfmod --help` e usar os nomes corretos — o objetivo imutável é um projeto que compile contra o GameDir.

- [ ] **Step 5: Fixar GameDir e compilar o scaffold sem alterações**

Run:
```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
GAMEDIR="$HOME/.steam/steam/steamapps/common/How to Fish/How to Fish"
ls "$GAMEDIR/How to Fish_Data/Managed/Assembly-CSharp.dll"
dotnet build /var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix -c Release -p:GameDir="$GAMEDIR" 2>&1 | tail -n 15
```
Expected: `Build succeeded.` Se o template usar variável diferente (`HOWTOFISH_DIR` env), rodar com `HOWTOFISH_DIR="$GAMEDIR" dotnet build ...` como fallback documentado no README do template/MoreFishAPI.

- [ ] **Step 6: Commit**

```bash
git -C /var/home/guigumi/howtofish-sellall-mod add src/SellAllFloorFix
git -C /var/home/guigumi/howtofish-sellall-mod commit -m "feat: scaffold htfmod SellAllFloorFix"
```
Expected: commit criado.

---

### Task 3: Plugin shell + config + tecla (sem lógica de jogo ainda)

**Files:**
- Create: `/var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/SellAllConfig.cs`
- Modify: `/var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/Plugin.cs`
- Test: build sucede; log mostra `SellAllFloorFix v1.0.0 loaded` e F9 loga `SellAll: tecla detectada (stub)`

**Interfaces:**
- Consumes: `PLUGIN_GUID = "Guigumi.SellAllFloorFix"`, `GameDir` da Task 2
- Produces: `SellAllConfig` (usado na Task 5) e `Plugin.TrySellAll()` stub (substituído na Task 5); `Plugin.Logger` disponível

- [ ] **Step 1: Escrever SellAllConfig.cs**

Conteúdo exato de `/var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/SellAllConfig.cs`:

```csharp
using BepInEx.Configuration;
using UnityEngine;

namespace SellAllFloorFix
{
    internal sealed class SellAllConfig
    {
        public ConfigEntry<KeyboardShortcut> Tecla { get; }
        public ConfigEntry<bool> SoVendaveis { get; }
        public ConfigEntry<bool> IncluirSemValor { get; }
        public ConfigEntry<float> CooldownSeg { get; }
        public ConfigEntry<int> LimitePorFrame { get; }

        public SellAllConfig(ConfigFile cfg)
        {
            Tecla = cfg.Bind("Geral", "Tecla", new KeyboardShortcut(KeyCode.F9), "Tecla que vende tudo do chão (host).");
            SoVendaveis = cfg.Bind("Geral", "SoVendaveis", true, "Se true, só vende itens com valor > 0.");
            IncluirSemValor = cfg.Bind("Geral", "IncluirSemValor", false, "Se true, também despawna itens sem valor (limpeza total, sem crédito).");
            CooldownSeg = cfg.Bind("Geral", "CooldownSeg", 0.5f, "Tempo mínimo entre execuções.");
            LimitePorFrame = cfg.Bind("Geral", "LimitePorFrame", 100, "Itens despawnados por frame (evita freeze).");
        }
    }
}
```

- [ ] **Step 2: Escrever Plugin.cs stub com Update + cooldown**

Conteúdo exato de `/var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/Plugin.cs`:

```csharp
using BepInEx;
using UnityEngine;

namespace SellAllFloorFix
{
    [BepInPlugin("Guigumi.SellAllFloorFix", "SellAllFloorFix", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;
        internal SellAllConfig Cfg = null!;
        private float _lastRun = -999f;

        private void Awake()
        {
            Log = Logger;
            Cfg = new SellAllConfig(Config);
            Log.LogInfo("SellAllFloorFix v1.0.0 loaded. Tecla: " + Cfg.Tecla.Value);
        }

        private void Update()
        {
            if (Cfg.Tecla.Value.IsDown())
            {
                if (Time.realtimeSinceStartup - _lastRun < Cfg.CooldownSeg.Value)
                    return;
                _lastRun = Time.realtimeSinceStartup;
                TrySellAll();
            }
        }

        private void TrySellAll()
        {
            Log.LogInfo("SellAll: tecla detectada (stub da Task 3). Lógica real entra na Task 5.");
        }
    }
}
```

Nota: `ManualLogSource` requer `using BepInEx.Logging;` se o template não importar globalmente. Se o build reclamar, adicionar o using — ver passo 3.

- [ ] **Step 3: Compilar e corrigir usings se necessário**

Run:
```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
GAMEDIR="$HOME/.steam/steam/steamapps/common/How to Fish/How to Fish"
dotnet build /var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix -c Release -p:GameDir="$GAMEDIR" 2>&1 | tail -n 20
```
Expected: `Build succeeded.` Se erro `ManualLogSource não encontrado`, adicionar `using BepInEx.Logging;` no topo de `Plugin.cs` e rebuildar. Se erro `KeyboardShortcut não encontrado`, adicionar `using BepInEx.Configuration;`.

- [ ] **Step 4: Teste em jogo (stub): F9 deve logar sem crash**

1. Copiar a dll para o perfil: o template com `Config.Build.user.props` já copia, senão copiar manualmente:
```bash
DLL=$(find /var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/bin/Release -name "SellAllFloorFix.dll" | head -n 1)
echo "$DLL"
R2PROFILE=$(dirname $(find "$HOME/.config/r2modmanPlus-local" -name "BepInEx.Preloader.dll" 2>/dev/null | head -n 1))/..
echo "$R2PROFILE"
mkdir -p "$R2PROFILE/BepInEx/plugins/Guigumi-SellAllFloorFix"
cp "$DLL" "$R2PROFILE/BepInEx/plugins/Guigumi-SellAllFloorFix/"
```
2. Abrir jogo via r2modman `Start modded`, apertar F9 no menu/ilha, fechar.
3. Verificar:
```bash
LOG=$(find "$HOME/.config/r2modmanPlus-local" -name "LogOutput.log" 2>/dev/null | head -n 1)
grep -iE 'SellAllFloorFix|SellAll: tecla' "$LOG" | tail -n 10
```
Expected: linhas `SellAllFloorFix v1.0.0 loaded` e `SellAll: tecla detectada (stub da Task 3)`. Nenhum `Exception`.

- [ ] **Step 5: Commit**

```bash
git -C /var/home/guigumi/howtofish-sellall-mod add src/SellAllFloorFix/Plugin.cs src/SellAllFloorFix/SellAllConfig.cs
git -C /var/home/guigumi/howtofish-sellall-mod commit -m "feat: plugin shell com tecla F9 e config"
```
Expected: commit criado.

---

### Task 4: ItemCollector via reflexão (coração anti-rename)

**Files:**
- Create: `/var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/ItemCollector.cs`
- Test: build sucede; teste em jogo com 5 itens dropados loga `SellAll scan: N candidatos`

**Interfaces:**
- Consumes: nada do jogo em tempo de compilação (só `UnityEngine`); lê `inspection-notes.md` para ordem de nomes
- Produces: `GroundItem` + `ItemCollector.Collect(bool soVendaveis, bool incluirSemValor)` consumidos pela Task 5 — assinatura exata abaixo

- [ ] **Step 1: Escrever ItemCollector.cs**

Conteúdo exato de `/var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/ItemCollector.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SellAllFloorFix
{
    internal readonly struct GroundItem
    {
        public readonly GameObject Go;
        public readonly int Worth;
        public readonly Component Component;
        public readonly object? NetObj;

        public GroundItem(GameObject go, int worth, Component comp, object? netObj)
        {
            Go = go;
            Worth = worth;
            Component = comp;
            NetObj = netObj;
        }
    }

    internal static class ItemCollector
    {
        private static readonly string[] ItemTypeNames = { "Item", "Creature" };
        private static readonly string[] WorthNames = { "Worth", "worth", "price", "Price", "value", "Value", "sellPrice", "SellPrice" };
        private static readonly string[] HeldNames = { "isHeld", "IsHeld", "held", "Held", "inHand", "InHand", "equipped", "Equipped" };

        public static List<GroundItem> Collect(bool soVendaveis, bool incluirSemValor)
        {
            var out_ = new List<GroundItem>(128);
            var behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            foreach (var b in behaviours)
            {
                if (b == null) continue;
                var t = b.GetType();
                if (!IsItemType(t)) continue;
                var go = b.gameObject;
                if (go == null || !go.activeInHierarchy) continue;
                if (IsHeld(b)) continue;
                int worth = ReadWorth(b);
                if (soVendaveis && worth <= 0 && !incluirSemValor) continue;
                object? netObj = FindNetworkObject(go);
                out_.Add(new GroundItem(go, Math.Max(0, worth), b, netObj));
            }
            return out_;
        }

        private static bool IsItemType(Type t)
        {
            string name = t.Name;
            foreach (var n in ItemTypeNames)
                if (name == n || t.FullName == n)
                    return true;
            // Herança: Creature costuma herdar de Item — aceita subclasses cujo base se chame Item
            var bt = t.BaseType;
            while (bt != null && bt != typeof(object) && bt != typeof(MonoBehaviour) && bt != typeof(Component))
            {
                if (bt.Name == "Item") return true;
                bt = bt.BaseType;
            }
            return false;
        }

        private static bool IsHeld(Component c)
        {
            var t = c.GetType();
            foreach (var n in HeldNames)
            {
                var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(bool))
                {
                    try { if ((bool)f.GetValue(c)!) return true; } catch { }
                }
                var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(bool) && p.CanRead)
                {
                    try { if ((bool)p.GetValue(c, null)!) return true; } catch { }
                }
            }
            // Segurança extra: se o objeto está parentado a um Player, considera segurado
            var parent = c.transform?.parent;
            while (parent != null)
            {
                if (parent.name.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0
                    || parent.name.IndexOf("hand", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                parent = parent.parent;
            }
            return false;
        }

        private static int ReadWorth(Component c)
        {
            var t = c.GetType();
            foreach (var n in WorthNames)
            {
                var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null)
                {
                    try
                    {
                        object? v = f.GetValue(c);
                        if (v is int i) return i;
                        if (v is float fl) return Mathf.RoundToInt(fl);
                        if (v is double d) return (int)Math.Round(d);
                    }
                    catch { }
                }
                var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.CanRead)
                {
                    try
                    {
                        object? v = p.GetValue(c, null);
                        if (v is int i) return i;
                        if (v is float fl) return Mathf.RoundToInt(fl);
                        if (v is double d) return (int)Math.Round(d);
                    }
                    catch { }
                }
            }
            return 0;
        }

        private static object? FindNetworkObject(GameObject go)
        {
            var comps = go.GetComponents<Component>();
            foreach (var c in comps)
            {
                if (c == null) continue;
                if (c.GetType().Name == "NetworkObject")
                    return c;
            }
            return null;
        }
    }
}
```

- [ ] **Step 2: Compilar**

Run:
```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
GAMEDIR="$HOME/.steam/steam/steamapps/common/How to Fish/How to Fish"
dotnet build /var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix -c Release -p:GameDir="$GAMEDIR" 2>&1 | tail -n 8
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git -C /var/home/guigumi/howtofish-sellall-mod add src/SellAllFloorFix/ItemCollector.cs
git -C /var/home/guigumi/howtofish-sellall-mod commit -m "feat: ItemCollector com reflexao tolerante a rename"
```
Expected: commit criado.

---

### Task 5: Seller + Despawner + TrySellAll real (funcionalidade completa)

**Files:**
- Create: `/var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/Seller.cs`
- Create: `/var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/Despawner.cs`
- Modify: `/var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/Plugin.cs` (troca stub por implementação real)
- Test: build sucede; em jogo como host com 20+ itens, F9 credita total exato e limpa o chão

**Interfaces:**
- Consumes: `ItemCollector.Collect(bool, bool)` e `GroundItem(Go, Worth, Component, NetObj)` da Task 4; `SellAllConfig` da Task 3
- Produces: comportamento final de `Plugin.TrySellAll()` — nenhuma task posterior muda assinatura

- [ ] **Step 1: Escrever Seller.cs (tenta venda nativa, senão credita direto)**

Conteúdo exato de `/var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/Seller.cs`:

```csharp
using System;
using System.Reflection;
using UnityEngine;

namespace SellAllFloorFix
{
    internal static class Seller
    {
        private static readonly string[] SellMethodNames = { "Sell", "SellItem", "CmdSell", "ServerSell", "SellToShop" };

        public static bool TryNativeSell(Component item)
        {
            var t = item.GetType();
            foreach (var n in SellMethodNames)
            {
                var m = t.GetMethod(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (m == null) continue;
                try { m.Invoke(item, null); return true; }
                catch (Exception e) { Plugin.Log.LogDebug("Sell nativo " + n + " falhou: " + e.GetBaseException().Message); }
            }
            return false;
        }

        public static void Credit(int total)
        {
            if (total <= 0) return;
            if (TryCreditViaMoneyManager(total)) return;
            Plugin.Log.LogWarning("SellAll: nenhum MoneyManager achado; dinheiro NÃO creditado (itens ainda serão despawnados).");
        }

        private static bool TryCreditViaMoneyManager(int total)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? mmType = null;
                try { mmType = asm.GetType("MoneyManager"); } catch { continue; }
                if (mmType == null) continue;
                // 1) método estático AddMoney(int) / Add(int) / GiveMoney(int)
                foreach (var mn in new[] { "AddMoney", "Add", "GiveMoney", "AddCoins" })
                {
                    var m = mmType.GetMethod(mn, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(int) }, null);
                    if (m == null) continue;
                    try { m.Invoke(null, new object[] { total }); AfterCreditFx(total); return true; }
                    catch (Exception e) { Plugin.Log.LogDebug(mn + " falhou: " + e.GetBaseException().Message); }
                }
                // 2) instância singleton: Instance.AddMoney(total)
                try
                {
                    object? inst = null;
                    var instProp = mmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instProp != null) inst = instProp.GetValue(null, null);
                    if (inst == null)
                    {
                        var f = mmType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                        if (f != null) inst = f.GetValue(null);
                    }
                    if (inst == null)
                    {
                        var found = UnityEngine.Object.FindObjectOfType(mmType) as Component;
                        if (found != null) inst = found;
                    }
                    if (inst != null)
                    {
                        foreach (var mn in new[] { "AddMoney", "Add", "GiveMoney", "AddCoins" })
                        {
                            var m = mmType.GetMethod(mn, BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                            if (m == null) continue;
                            m.Invoke(inst, new object[] { total });
                            AfterCreditFx(total);
                            return true;
                        }
                    }
                }
                catch (Exception e) { Plugin.Log.LogDebug("MoneyManager instância falhou: " + e.GetBaseException().Message); }
            }
            return false;
        }

        private static void AfterCreditFx(int total)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type? ui = null;
                    try { ui = asm.GetType("PlayerUI"); } catch { continue; }
                    if (ui == null) continue;
                    var m = ui.GetMethod("SetMoney", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                    if (m != null)
                    {
                        var found = UnityEngine.Object.FindObjectOfType(ui) as Component;
                        if (m.IsStatic) m.Invoke(null, new object[] { total });
                        else if (found != null) m.Invoke(found, new object[] { total });
                    }
                    Type? mm = null;
                    try { mm = asm.GetType("MoneyManager"); } catch { }
                    var snd = mm?.GetMethod("MoneySound", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                    if (snd != null)
                    {
                        if (snd.IsStatic) snd.Invoke(null, null);
                        else
                        {
                            var found = mm != null ? UnityEngine.Object.FindObjectOfType(mm) as Component : null;
                            if (found != null) snd.Invoke(found, null);
                        }
                    }
                    break;
                }
            }
            catch (Exception e) { Plugin.Log.LogDebug("FX pós-crédito falhou: " + e.GetBaseException().Message); }
            Plugin.Log.LogInfo("SellAll: creditado $" + total);
        }
    }
}
```

- [ ] **Step 2: Escrever Despawner.cs (server check + despawn em batch)**

Conteúdo exato de `/var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/Despawner.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SellAllFloorFix
{
    internal static class Despawner
    {
        public static bool IsServer()
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type? finder = asm.GetType("FishNet.Object.InstanceFinder");
                    if (finder != null)
                    {
                        var p = finder.GetProperty("IsServer", BindingFlags.Public | BindingFlags.Static);
                        if (p != null) return (bool)p.GetValue(null, null)!;
                    }
                    Type? nm = asm.GetType("FishNet.Managing.NetworkManager");
                    if (nm != null)
                    {
                        var inst = UnityEngine.Object.FindObjectOfType(nm) as Component;
                        if (inst != null)
                        {
                            var p = nm.GetProperty("IsServer", BindingFlags.Public | BindingFlags.Instance);
                            if (p != null) return (bool)p.GetValue(inst, null)!;
                        }
                    }
                }
            }
            catch (Exception e) { Plugin.Log.LogDebug("IsServer check falhou: " + e.GetBaseException().Message); }
            return true; // single-player/host por padrão; cliente dedicado ainda passa pelo aviso da Task 5
        }

        public static IEnumerator DespawnBatch(List<GroundItem> items, int perFrame, Action<int> onDone)
        {
            int done = 0;
            int batch = 0;
            foreach (var gi in items)
            {
                DespawnOne(gi);
                done++;
                batch++;
                if (batch >= Math.Max(1, perFrame))
                {
                    batch = 0;
                    yield return null;
                }
            }
            onDone(done);
        }

        public static void DespawnOne(GroundItem gi)
        {
            try
            {
                if (gi.Go == null) return;
                if (gi.NetObj != null)
                {
                    var t = gi.NetObj.GetType();
                    var m = t.GetMethod("Despawn", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    if (m != null) { m.Invoke(gi.NetObj, null); return; }
                    // Fallback: ServerManager.Despawn(go)
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        Type? sm = null;
                        try { sm = asm.GetType("FishNet.Object.ServerManager"); } catch { continue; }
                        if (sm == null) continue;
                        var inst2 = UnityEngine.Object.FindObjectOfType(sm) as Component;
                        var dm = sm.GetMethod("Despawn", BindingFlags.Public | BindingFlags.Instance);
                        if (dm != null && inst2 != null) { dm.Invoke(inst2, new object[] { gi.Go }); return; }
                    }
                }
                UnityEngine.Object.Destroy(gi.Go);
            }
            catch (Exception e) { Plugin.Log.LogDebug("Despawn falhou p/ " + (gi.Go != null ? gi.Go.name : "?") + ": " + e.GetBaseException().Message); }
        }
    }
}
```

- [ ] **Step 3: Substituir Plugin.cs stub pela versão real**

Conteúdo exato final de `/var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/Plugin.cs`:

```csharp
using System.Collections;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace SellAllFloorFix
{
    [BepInPlugin("Guigumi.SellAllFloorFix", "SellAllFloorFix", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;
        internal SellAllConfig Cfg = null!;
        private float _lastRun = -999f;
        private bool _running;

        private void Awake()
        {
            Log = Logger;
            Cfg = new SellAllConfig(Config);
            Log.LogInfo("SellAllFloorFix v1.0.0 loaded. Tecla: " + Cfg.Tecla.Value);
        }

        private void Update()
        {
            if (Cfg.Tecla.Value.IsDown())
            {
                if (_running) return;
                if (Time.realtimeSinceStartup - _lastRun < Cfg.CooldownSeg.Value) return;
                _lastRun = Time.realtimeSinceStartup;
                TrySellAll();
            }
        }

        private void TrySellAll()
        {
            try
            {
                if (!Despawner.IsServer())
                {
                    Log.LogInfo("SellAll: apenas o host pode vender (v1). Peça ao host para apertar " + Cfg.Tecla.Value + ".");
                    return;
                }
                var items = ItemCollector.Collect(Cfg.SoVendaveis.Value, Cfg.IncluirSemValor.Value);
                Log.LogInfo("SellAll scan: " + items.Count + " candidatos.");
                if (items.Count == 0)
                {
                    Log.LogInfo("SellAll: nada para vender.");
                    return;
                }
                int total = 0;
                int comVendaNativa = 0;
                var paraDespawner = new System.Collections.Generic.List<GroundItem>(items.Count);
                foreach (var gi in items)
                {
                    if (gi.Worth > 0 && Seller.TryNativeSell(gi.Component))
                    {
                        comVendaNativa++;
                        continue; // venda nativa já cuidou de dinheiro+despawn
                    }
                    total += gi.Worth;
                    paraDespawner.Add(gi);
                }
                if (total > 0) Seller.Credit(total);
                _running = true;
                StartCoroutine(Despawner.DespawnBatch(paraDespawner, Cfg.LimitePorFrame.Value, done =>
                {
                    _running = false;
                    Log.LogInfo("SellAll: " + items.Count + " itens (" + comVendaNativa + " venda nativa), $" + total + ", " + done + " despawnados.");
                }));
            }
            catch (System.Exception e)
            {
                _running = false;
                Log.LogError("SellAll falhou: " + e);
            }
        }
    }
}
```

- [ ] **Step 4: Compilar tudo**

Run:
```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
GAMEDIR="$HOME/.steam/steam/steamapps/common/How to Fish/How to Fish"
dotnet build /var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix -c Release -p:GameDir="$GAMEDIR" 2>&1 | tail -n 8
```
Expected: `Build succeeded.` Corrigir usings duplicados se o compilador reclamar (remover `using` repetido, nunca inventar API nova).

- [ ] **Step 5: Teste funcional em jogo (host, 20+ itens)**

1. Copiar dll para o perfil (mesmo comando da Task 3 Step 4).
2. Como host: pesca e larga 20 peixes no chão (ou morre algumas vezes para largar loot), anota dinheiro atual, aperta F9, anota novo valor.
3. Verificar log:
```bash
LOG=$(find "$HOME/.config/r2modmanPlus-local" -name "LogOutput.log" 2>/dev/null | head -n 1)
grep -iE 'SellAll (scan|nada|creditado|itens|apenas o host|falhou)' "$LOG" | tail -n 15
```
Expected: `SellAll scan: 20+ candidatos`, `creditado $X`, `N itens ..., M despawnados`, chão vazio, sem `Exception`. Se `Worth` veio 0 para tudo, voltar à Task 1 e corrigir `WorthNames` com o nome real do dump — sem chutar.

- [ ] **Step 6: Commit**

```bash
git -C /var/home/guigumi/howtofish-sellall-mod add src/SellAllFloorFix/Seller.cs src/SellAllFloorFix/Despawner.cs src/SellAllFloorFix/Plugin.cs
git -C /var/home/guigumi/howtofish-sellall-mod commit -m "feat: venda total + despawn server com batch"
```
Expected: commit criado.

---

### Task 6: Pacote Thunderstore + validação final Linux

**Files:**
- Modify: `/var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/SellAllFloorFix.csproj` (metadados Thunderstore: nome, descrição, versão 1.0.0)
- Create: `/var/home/guigumi/howtofish-sellall-mod/README.md`, `/var/home/guigumi/howtofish-sellall-mod/CHANGELOG.md`
- Test: zip instala via r2modman `Import local mod` e F9 funciona em lobby host+cliente mesma versão

**Interfaces:**
- Consumes: dll Release da Task 5
- Produces: `Guigumi-SellAllFloorFix-1.0.0.zip` instalável; fim do plano

- [ ] **Step 1: Preencher README.md e CHANGELOG.md**

Conteúdo exato de `/var/home/guigumi/howtofish-sellall-mod/README.md`:

```markdown
# SellAllFloorFix (How to Fish)

Aperte F9 (host) para vender todos os itens vendáveis largados no chão da ilha atual. O dinheiro vai para quem apertou. Feito para destravar lag quando dezenas de peixes/loot bugam e acumulam.

- Loader: BepInEx 5.4.23.5 (via r2modman BepInExPack)
- Uso: seja o host, aperte F9, veja `+$X (N itens)` no log/HUD
- Cliente sem autoridade: recebe aviso e nada faz (v1)
- Config: `BepInEx/config/Guigumi.SellAllFloorFix.cfg` (tecla, SoVendaveis, IncluirSemValor, CooldownSeg, LimitePorFrame)
- Linux/Proton: Launch Options `WINEDLLOVERRIDES="winhttp=n,b" %command%`
```

Conteúdo exato de `/var/home/guigumi/howtofish-sellall-mod/CHANGELOG.md`:

```markdown
# 1.0.0 — 2026-09-23
- Venda total do chão com F9 (host-only)
- Crédito via rota nativa ou MoneyManager/PlayerUI
- Despawn em batch 100/frame com cooldown 0.5s
```

- [ ] **Step 2: Conferir metadados Thunderstore no csproj e compilar Release final**

Abrir `src/SellAllFloorFix/SellAllFloorFix.csproj`, garantir que contém `PackageGUID Guigumi.SellAllFloorFix`, `Version 1.0.0`, descrição `Vende tudo do chão com F9 (anti-lag)`. Não adivinhar schema do template — manter as tags existentes e só corrigir valores.

Run:
```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
GAMEDIR="$HOME/.steam/steam/steamapps/common/How to Fish/How to Fish"
dotnet build /var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix -c Release -p:GameDir="$GAMEDIR" 2>&1 | tail -n 5
DLL=$(find /var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/bin/Release -name "SellAllFloorFix.dll" | head -n 1)
ls -la "$DLL"
```
Expected: `Build succeeded`, dll existe.

- [ ] **Step 3: Montar zip no formato Thunderstore e importar no r2modman**

Run:
```bash
DLL=$(find /var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/bin/Release -name "SellAllFloorFix.dll" | head -n 1)
STAGE=/tmp/sellall-ts
rm -rf "$STAGE"
mkdir -p "$STAGE/plugins/Guigumi-SellAllFloorFix"
cp "$DLL" "$STAGE/plugins/Guigumi-SellAllFloorFix/"
cp /var/home/guigumi/howtofish-sellall-mod/README.md "$STAGE/"
cp /var/home/guigumi/howtofish-sellall-mod/CHANGELOG.md "$STAGE/"
printf '{"name":"SellAllFloorFix","author":"Guigumi","version":"1.0.0","description":"Vende tudo do chao com F9 (anti-lag)"}' > "$STAGE/manifest.json"
cp /var/home/guigumi/howtofish-sellall-mod/src/SellAllFloorFix/icon.png "$STAGE/" 2>/dev/null || echo "sem icon.png, Thunderstore exige um antes de publicar — adicione 256x256 depois"
(cd "$STAGE" && zip -r /tmp/Guigumi-SellAllFloorFix-1.0.0.zip .)
ls -la /tmp/Guigumi-SellAllFloorFix-1.0.0.zip
```
Expected: zip criado. No r2modman: `Settings` → `Import local mod` → seleciona o zip → `Start modded` → F9 funciona.

- [ ] **Step 4: Validação final host+cliente**

Com dois jogadores na mesma versão: cliente aperta F9 → log `apenas o host pode vender`; host aperta F9 com 30+ itens → dinheiro correto + chão limpo + sem desync (cliente vê chão limpo). Checar ambos os `LogOutput.log` sem `Exception`.

- [ ] **Step 5: Commit final**

```bash
git -C /var/home/guigumi/howtofish-sellall-mod add README.md CHANGELOG.md docs src
git -C /var/home/guigumi/howtofish-sellall-mod commit -m "release: SellAllFloorFix 1.0.0 pacote Thunderstore"
```
Expected: commit criado.

---

## Self-Review

- Spec cobertura: problema lag→Task 5 batch; F9→Tasks 3/5; tudo-vendável-pra-mim→Task 5 soma+crédito; host/cliente→Task 5 IsServer+aviso; zero→Tasks 1/2 (SDK, BepInEx, Proton); Linux path exato→Tasks 1/2/5. Sem gaps.
- Placeholder scan: nenhum `TBD/TODO` — todos os comandos têm paths/comandos exatos, códigos C# completos, expected outputs definidos. Fallbacks (`HOWTOFISH_DIR`, download manual r2modman, icon.png) são instruções alternativas concretas, não placeholders.
- Type consistency: `GroundItem(Go, Worth, Component, NetObj)` definido na Task 4 e usado com mesmos nomes/tipos na Task 5; `SellAllConfig` props (`Tecla, SoVendaveis, IncluirSemValor, CooldownSeg, LimitePorFrame`) idênticas entre Tasks 3 e 5; `Plugin.Log` estático usado por Seller/Despawner.
