# How to Fish: SellAllFloorFix

Sabe quando você tá jogando *How to Fish* com os amigos e o chão começa a ficar entupido de peixe, carcaça e loot bugado até o jogo começar a engasgar de tanto lag?

Esse mod resolve isso com um botão: aperta **F9** como host e ele vende todos os itens caídos no chão da ilha, entrega o dinheiro pra você e limpa o mapa na hora.

---

## Como funciona na prática

- **Aperta F9 e pronto:** o mod cata tudo que tiver valor no chão, soma a grana, toca o som de moedas na hora e limpa os objetos direto do servidor.
- **Só o host precisa instalar:** seus amigos podem continuar jogando vanilla tranquilamente. Como a limpeza roda pelo servidor, o chão esvazia pra todo mundo e o lag do lobby desce pra todos.
- **Sem congelar o jogo:** em vez de apagar 200 itens num frame só e travar a tela, ele despawna os objetos em lotes rápidos em segundo plano.
- **Se você não for o host:** apertar a tecla não vai desincronizar nem bugar a sala — o mod só te avisa que apenas quem criou a partida pode rodar a limpeza.

---

## Como instalar

O passo a passo detalhado (com r2modman, instalação manual e como rodar no Linux / Steam Deck) está no **[TUTORIAL.md](TUTORIAL.md)**.

Resumo rápido:
1. Baixe o arquivo `.zip` na aba **[Releases](https://github.com/Guigumi/howtofish-sellall-mod/releases)**.
2. Se usa **r2modman**: vá em `Settings` → `Import local mod` e escolha o `.zip`.
3. Se instala **manual**: extraia a DLL na pasta `BepInEx/plugins/Guigumi-SellAllFloorFix/`.
4. Abra o jogo, crie a sala como host e aperte **F9** quando começar a acumular tralha no chão.

---

## Configurações

Depois de abrir o jogo com o mod uma vez, vai aparecer o arquivo de configuração em:
`BepInEx/config/Guigumi.SellAllFloorFix.cfg`

Por lá dá pra personalizar:
- **`Tecla`** (padrão `F9`): se quiser usar outra tecla (ex: `F8`, `Delete`).
- **`SoVendaveis`** (padrão `true`): vende apenas o que tem valor em dinheiro.
- **`IncluirSemValor`** (padrão `false`): se quiser que ele delete também itens inúteis/sem valor.
- **`CooldownSeg`** (padrão `0.5`): intervalo mínimo em segundos pra evitar spam de tecla.
- **`LimitePorFrame`** (padrão `100`): quantos itens ele apaga por frame (ajuste se seu PC for mais fraco).

---

## Compilando do código

Caso queira alterar o código ou compilar por conta própria, você só precisa do [.NET SDK 10+](https://dotnet.microsoft.com/download):

```bash
git clone https://github.com/Guigumi/howtofish-sellall-mod.git
cd howtofish-sellall-mod

dotnet build src/SellAllFloorFix -c Release -p:HowToFishGameRootDir="CAMINHO_DA_SUA_STEAM/How to Fish/How to Fish"
```

A DLL gerada fica em `src/SellAllFloorFix/artifacts/bin/SellAllFloorFix/release/Guigumi.SellAllFloorFix.dll`.
