# SellAllFloorFix 🐟💰 (How to Fish)

Mod para o jogo **[How to Fish](https://store.steampowered.com/app/4001890/How_to_Fish/)** que resolve o problema de queda brutal de FPS e lag de rede causado pelo acúmulo de peixes e itens bugados no chão durante sessões multiplayer.

Ao apertar uma tecla (**padrão: `F9`**), o mod varre todos os itens vendáveis caídos na ilha atual, credita o valor para quem apertou a tecla e despawna os objetos diretamente no servidor.

---

## 🚀 Funcionalidades

- **Limpeza Anti-Lag instantânea:** Remove dezenas ou centenas de objetos sincronizados via física e rede (FishNet) que travam o host e os jogadores conectados.
- **Venda Automática:** O dinheiro dos itens no chão não é perdido — é somado e creditado na hora com som de moedas e animação no HUD.
- **Segurança de Servidor (Host-Authoritative):** Somente o host tem autoridade para despawnar objetos e creditar o dinheiro sem risco de desync ou objetos fantasmas. Clientes que apertarem a tecla recebem apenas um aviso informativo.
- **Despawn em Lotes (Anti-Freeze):** Despawna os itens em lotes (padrão: 100 por frame) com coroutine para evitar micro-travamentos na hora da limpeza.
- **Totalmente Configurável:** Altere a tecla de atalho, intervalo de cooldown, filtros de itens e limites por frame via arquivo de configuração.

---

## 📥 Instalação Rápida

👉 **Consulte o guia completo no [TUTORIAL.md](TUTORIAL.md) para o passo a passo detalhado de instalação no Windows e Linux (Steam Deck / Bazzite).**

### Resumo:
1. Instale o **BepInEx 5.4.x** (recomendado via **r2modman** selecionando o pacote `BepInExPack`).
2. Baixe o `.zip` mais recente na aba **[Releases](https://github.com/guigumi/howtofish-sellall-mod/releases)**.
3. No r2modman: vá em `Settings` → `Import local mod` e selecione o `.zip`.
   *(Ou manualmente: extraia a DLL em `BepInEx/plugins/Guigumi-SellAllFloorFix/`)*.
4. Inicie o jogo, seja o host da partida e aperte **`F9`** quando o chão estiver cheio de itens.

---

## ⚙️ Configurações

Após iniciar o jogo com o mod pela primeira vez, o arquivo de configuração será gerado em:
`BepInEx/config/Guigumi.SellAllFloorFix.cfg`

Opções disponíveis:
| Opção | Padrão | Descrição |
|---|---|---|
| `Tecla` | `F9` | Tecla de atalho para acionar a limpeza/venda. |
| `SoVendaveis` | `true` | Vende apenas itens com valor > 0. |
| `IncluirSemValor` | `false` | Se `true`, também despawna itens sem valor (limpeza total). |
| `CooldownSeg` | `0.5` | Intervalo mínimo (em segundos) entre ativações. |
| `LimitePorFrame` | `100` | Quantidade máxima de objetos despawnados por frame. |

---

## 🛠️ Como Compilar do Código Fonte

Requisitos: [.NET SDK 10+](https://dotnet.microsoft.com/download)

```bash
# Clone o repositório
git clone https://github.com/guigumi/howtofish-sellall-mod.git
cd howtofish-sellall-mod

# Compile em Release apontando para a pasta do jogo instalado
dotnet build src/SellAllFloorFix -c Release -p:HowToFishGameRootDir="CAMINHO_PARA/How to Fish/How to Fish"
```
A DLL compilada estará em:
`src/SellAllFloorFix/artifacts/bin/SellAllFloorFix/release/Guigumi.SellAllFloorFix.dll`

---

## 📜 Licença & Créditos

Desenvolvido por **Guigumi**. Distribuído sob a licença MIT.
Criado para a comunidade do jogo *How to Fish* da Dazed Games.
