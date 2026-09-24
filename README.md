# How to Fish: SellAllFloorFix

Mod para **How to Fish** que acaba com o lag de peixes e itens bugados acumulando no chão em partidas multiplayer.

Aperte **F9** como host: ele vende tudo que estiver largado na ilha, credita o dinheiro para você e limpa o mapa na hora.

## Como funciona

- **Tecla F9:** varre o chão, vende os itens e remove os objetos do servidor sem travar a tela.
- **Só o host precisa do mod:** seus amigos podem jogar no vanilla normalmente. Quando você limpar, o chão limpa para todos e o lag do lobby acaba.
- **Seguro:** se você não for o host, apertar F9 não quebra nada — o mod apenas avisa no log que a ação requer o host.

## Instalação Rápida

1. Baixe o `.zip` na aba **[Releases](https://github.com/Guigumi/howtofish-sellall-mod/releases)**.
2. No **r2modman**: vá em `Settings` → `Import local mod` e selecione o `.zip`.
3. Inicie o jogo pelo r2modman (**Start modded**), puxe a sala como host e aperte **F9**.

> Guia detalhado (jogar direto pela Steam, Linux/Deck ou instalação manual): veja **[TUTORIAL.md](TUTORIAL.md)**.

## Configuração

O arquivo fica em `BepInEx/config/Guigumi.SellAllFloorFix.cfg`:
- `Tecla = F9`: tecla de atalho.
- `SoVendaveis = true`: vende só itens com valor em dinheiro.
- `LimitePorFrame = 100`: quantos itens despawna por frame (evita engasgos).
- `CooldownSeg = 0.5`: intervalo mínimo entre apertos.
