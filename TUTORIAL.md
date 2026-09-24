# Tutorial de Instalação — SellAllFloorFix

## Método 1: r2modman (Recomendado)

1. No **r2modman**, selecione o jogo **How to Fish** (perfil `Default`).
2. Na aba **Online**, baixe o pacote **`BepInExPack`**.
3. Baixe o `.zip` do mod na aba **[Releases](https://github.com/Guigumi/howtofish-sellall-mod/releases)**.
4. No r2modman, vá em **Settings** → **Import local mod** e selecione o `.zip`.
5. Clique em **Start modded** para jogar. Como host, aperte **F9** para limpar o chão.

---

## Método 2: Instalação Manual

1. Instale o **BepInEx 5 (x64)** na pasta do jogo (`How to Fish.exe`).
2. Extraia o `.zip` da release em `BepInEx/plugins/Guigumi-SellAllFloorFix/`.
3. Se estiver no Linux / Steam Deck, adicione nas Opções de Inicialização da Steam:
   ```bash
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```

---

## Jogar direto pela Steam (sem abrir o r2modman)

Se você instalou pelo r2modman no Linux e quer abrir direto pelo botão "Jogar" da Steam:

1. Nas propriedades do jogo na Steam (Opções de Inicialização), coloque:
   ```bash
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```
2. No arquivo `doorstop_config.ini` dentro da pasta do jogo, aponte `target_assembly` para o perfil:
   ```ini
   target_assembly=/home/SEU_USUARIO/.config/r2modmanPlus-local/HowToFish/profiles/Default/BepInEx/core/BepInEx.Preloader.dll
   ```

---

## Dúvidas Rápidas

- **Meus amigos precisam do mod?** Não. Apenas o host precisa. Quando o host aperta F9, o chão limpa para todos os jogadores da sala.
- **E se eu apertar F9 como cliente?** Nada acontece. O mod apenas ignora e avisa no log que só o host tem autoridade.
- **Como trocar a tecla?** Altere a opção `Tecla = F9` em `BepInEx/config/Guigumi.SellAllFloorFix.cfg`.
