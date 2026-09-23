# SellAllFloorFix (How to Fish)

Aperte F9 (host) para vender todos os itens vendáveis largados no chão da ilha atual. O dinheiro vai para quem apertou. Feito para destravar lag quando dezenas de peixes/loot bugam e acumulam.

- Loader: BepInEx 5.4.23.5 (via r2modman BepInExPack)
- Uso: seja o host, aperte F9, veja `+$X (N itens)` no log/HUD
- Cliente sem autoridade: recebe aviso e nada faz (v1)
- Config: `BepInEx/config/Guigumi.SellAllFloorFix.cfg` (tecla, SoVendaveis, IncluirSemValor, CooldownSeg, LimitePorFrame)
- Linux/Proton: Launch Options `WINEDLLOVERRIDES="winhttp=n,b" %command%`
