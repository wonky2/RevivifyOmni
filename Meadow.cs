using RainMeadow;
using System.Linq;
using UnityEngine;

#pragma warning disable IDE0130 // annoying >:(
namespace Meadow
{
    internal static class Meadow
    {
        public static void Log(string msg, bool warn = false)
        {
            if (RainWorld.ShowLogs) Debug.Log(msg);

            if (warn) BepInEx.Logging.Logger.CreateLogSource(RevivifyOmni.Plugin.MOD_NAME).LogWarning(msg);
            else BepInEx.Logging.Logger.CreateLogSource(RevivifyOmni.Plugin.MOD_NAME).LogInfo(msg);
        }

        public static bool IsOnlineSession() => OnlineManager.lobby != null;
        public static bool IsOnlineArenaSession() => OnlineManager.lobby?.gameMode is ArenaOnlineGameMode;
        public static bool IsRemote(Player player) => player.room.game.Players.All(p => p.realizedCreature != player);
        
        public static void InvokeReviveRPC(Player player)
        {
            Log($"ReviveRPC! {player}");
            var critter = player.abstractCreature.GetOnlineCreature();
            critter.owner.InvokeOnceRPC(ReviveRPC, critter);
        }
        [SoftRPCMethod]
        public static void ReviveRPC(RPCEvent rpc, OnlinePhysicalObject onlinePlayer)
        {
            bool debug = RevivifyOmni.Options.DebugMode.Value;

            if (RevivifyOmni.Options.DisableRPC.Value)
            {
                if (debug) Log($"{rpc.from} tried to ReviveRPC you, but you've disabled RPC");
                return;
            }

            if (RevivifyOmni.Options.Mode.Value == "N" || (RevivifyOmni.Options.DisableInArena.Value && IsOnlineArenaSession()))
            {
                if (debug) Log($"{rpc.from} tried to ReviveRPC you, but you've disabled reviving");
                return;
            }

            if (debug) Log($"You've been ReviveRPC'd by {rpc.from}");
        
            Player player = (onlinePlayer.apo.realizedObject as Player);
        
            RevivifyOmni.Plugin.Data(player).deathTime = 0;
        
            player.stun = 20;
            player.airInLungs = 0.1f;
            player.exhausted = true;
            player.aerobicLevel = 1;
            if (ModManager.Watcher)
                player.injectedPoison = 0f;

            if (!RevivifyOmni.Options.DisableExhaustion.Value)
                player.playerState.permanentDamageTracking = Mathf.Clamp01((float)RevivifyOmni.Plugin.Data(player).deaths / RevivifyOmni.Options.DeathsUntilExhaustion.Value) * 0.6;

            player.playerState.alive = true;
            player.playerState.permaDead = false;
            player.dead = false;
            player.killTag = null;
            player.killTagCounter = 0;
            player.abstractCreature.abstractAI?.SetDestination(player.abstractCreature.pos);
        }
    }
}
