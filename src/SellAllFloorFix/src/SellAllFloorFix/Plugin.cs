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
