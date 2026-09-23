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
