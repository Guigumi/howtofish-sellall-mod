# Guia de Instalação e Uso — SellAllFloorFix

Este guia explica como instalar e usar o mod **SellAllFloorFix** no jogo *How to Fish*.

---

## 📋 Requisitos Prévios

- Ter o jogo **How to Fish** instalado na Steam.
- Modloader **BepInEx 5** (disponível automaticamente via **r2modman**).

---

## 🎮 Método 1: Instalação via r2modman (Recomendado)

O [r2modman](https://github.com/ebkr/r2modmanPlus/releases) é o gerenciador de mods mais estável para jogos Unity no Linux e Windows.

### Passo 1: Instalar o BepInEx
1. Baixe e abra o **r2modman**.
2. Na lista de jogos, procure por **How to Fish** e selecione o perfil **Default**.
3. Na barra lateral esquerda, clique em **Online**.
4. Procure por `BepInExPack` e clique em **Download with dependencies**.
5. Clique em **Start modded** uma vez para gerar os arquivos iniciais e feche o jogo ao chegar ao menu principal.

### Passo 2: Instalar o SellAllFloorFix
1. Baixe o arquivo `Guigumi-SellAllFloorFix-1.0.0.zip` da aba [Releases](https://github.com/guigumi/howtofish-sellall-mod/releases).
2. No r2modman, clique em **Settings** na barra lateral.
3. Procure por **Import local mod** e selecione o arquivo `.zip` baixado.
4. O mod agora aparecerá na sua lista de mods instalados!

---

## 🕹️ Como Jogar

1. Abra o jogo clicando em **Start modded** no r2modman.
2. Inicie uma partida multiplayer sendo o **Host** (ou jogue singleplayer).
3. Quando houver peixes e itens acumulados no chão causando lag, pressione a tecla **`F9`**.
4. Todos os itens no chão serão vendidos na hora, o dinheiro subirá no seu saldo e o chão ficará limpo, restaurando os FPS!

---

## ⚡ Dica: Como Jogar Direto pela Steam (Sem abrir o r2modman)

Se você não quer ter que abrir o r2modman toda vez que for jogar:

### No Linux (Steam Deck / Bazzite / Fedora / Ubuntu):
1. Na Steam, clique com o botão direito em **How to Fish** → **Propriedades...**
2. Em **Opções de Inicialização (Launch Options)**, adicione:
   ```bash
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```
3. Abra o arquivo `doorstop_config.ini` localizado na pasta de instalação do jogo:
   `~/.steam/steam/steamapps/common/How to Fish/How to Fish/doorstop_config.ini`
4. Altere a linha `target_assembly` para apontar diretamente para o Preloader do seu perfil r2modman:
   ```ini
   target_assembly=/home/SEU_USUARIO/.config/r2modmanPlus-local/HowToFish/profiles/Default/BepInEx/core/BepInEx.Preloader.dll
   ```
5. Pronto! Agora você pode abrir o jogo normalmente pelo botão **Jogar** na Steam com os mods ativos.

---

## 📁 Método 2: Instalação Manual (Sem r2modman)

Se você prefere não usar nenhum gerenciador de mods:

1. Baixe o **BepInEx 5 (x64)** no [repositório oficial do BepInEx](https://github.com/BepInEx/BepInEx/releases).
2. Extraia o conteúdo na pasta raiz do jogo (onde fica o `How to Fish.exe`).
3. Baixe o `Guigumi-SellAllFloorFix-1.0.0.zip` das Releases e extraia a DLL em:
   `How to Fish/BepInEx/plugins/Guigumi-SellAllFloorFix/Guigumi.SellAllFloorFix.dll`
4. Se estiver no Linux/Proton, coloque nas Opções de Inicialização da Steam:
   ```bash
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```

---

## ❓ Perguntas Frequentes (FAQ)

### 1. Meus amigos precisam ter o mod instalado?
**Não necessariamente.** Se você for o **Host** da sessão, somente você precisa do mod instalado. Quando você apertar `F9`, o servidor limpa o chão e todo mundo no lobby se beneficia da redução de lag. Seus amigos jogando na versão original (vanilla) verão os peixes sumirem normalmente.

*Observação:* Se outra pessoa for o host da partida, ela é quem precisará ter o mod ativo para poder usar a tecla de limpeza.

### 2. O que acontece se um cliente apertar F9?
O mod detecta que ele não é o host da sessão e exibe apenas um aviso no console/log: *"SellAll: apenas o host pode vender. Peça ao host para apertar F9."* Nenhuma ação é tomada do lado do cliente para garantir que a sincronização da sala não seja corrompida.

### 3. Como trocar a tecla de atalho?
Abra o arquivo de configuração gerado após a primeira execução:
`BepInEx/config/Guigumi.SellAllFloorFix.cfg`
Localize a linha:
```ini
Tecla = F9
```
E troque por qualquer tecla válida (ex: `F8`, `Delete`, `K`, etc).
