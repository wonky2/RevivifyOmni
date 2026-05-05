using BepInEx;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using UnityEngine;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace RevivifyOmni;

[BepInPlugin(MOD_ID, MOD_NAME, MOD_VERSION)]
sealed class Plugin : BaseUnityPlugin
{
    public const string MOD_ID = "Wonky.RevivifyOmni";
    public const string MOD_NAME = "RevivifyOmni";
    public const string MOD_VERSION = "1.2.4";

    public static bool meadowEnabled; // becomes true if Rain Meadow is enabled, always include in if statements with Meadow-specific checks

    public static void Log(string msg, bool warn = false)
    {
        if (RainWorld.ShowLogs) Debug.Log(msg);

        if (warn) BepInEx.Logging.Logger.CreateLogSource(MOD_NAME).LogWarning(msg);
        else BepInEx.Logging.Logger.CreateLogSource(MOD_NAME).LogInfo(msg);
    }

    static readonly ConditionalWeakTable<Player, PlayerData> cwt = new();
    public static PlayerData Data(Player p) => cwt.GetValue(p, _ => new());

    static PlayerGraphics G(Player p) => p.graphicsModule as PlayerGraphics;

    private static Vector2 HeartPos(Player player) =>
        Vector2.Lerp(player.firstChunk.pos, player.bodyChunks[1].pos, 0.38f) + new Vector2(0, 0.7f * player.firstChunk.rad);

    private static bool IsRevivingDisabled(Player self) =>
        Options.Mode.Value == "N" || (Options.DisableInArena.Value
        && ((meadowEnabled && Meadow.Meadow.IsOnlineArenaSession())
        || self.room.game.IsArenaSession));

    const float DEBUG_INTERVAL = 0.5f;
    private static bool CanRevive(Player medic, Player patient)
    {
        if (Data(patient) == null) Data(patient).debugTime = Time.time;

        // still needs some work
        if (Options.DebugMode.Value && Time.time > Data(patient).debugTime)
        {
            if (medic == patient)
            {
                Log($"{medic} can't revive {patient}: medic == patient");
            }
            if (IsRevivingDisabled(medic))
            {
                Log($"{medic} can't revive {patient}: IsRevivingDisabled(medic)");
            }
            if (patient.playerState.permaDead)
            {
                Log($"{medic} can't revive {patient}: patient.playerState.permaDead");
            }
            if (!patient.dead)
            {
                Log($"{medic} can't revive {patient}: !patient.dead");
            }
            if (patient.grabbedBy.Count > 1)
            {
                Log($"{medic} can't revive {patient}: patient.grabbedBy.Count > 1");
            }
            if (patient.Submersion > 0.8f)
            {
                Log($"{medic} can't revive {patient}: patient.Submersion > 0.8f");
            }
            if (Data(patient).Expired)
            {
                Log($"{medic} can't revive {patient}: Data(patient).Expired");
            }
            if (Data(patient).deaths >= Options.DeathsUntilExpire.Value && !Options.DisableExpiry.Value)
            {
                Log($"{medic} can't revive {patient}: Data(patient).deaths >= Options.DeathsUntilExpire.Value && !Options.DisableExpiry.Value");
            }
            if (!medic.Consious)
            {
                Log($"{medic} can't revive {patient}: !medic.Consious");
            }
            if (medic.grabbedBy.Count > 0 && medic.grabbedBy.All(x => x.grabber is not Player))
            {
                Log($"{medic} can't revive {patient}: medic.grabbedBy.Count > 0 && medic.grabbedBy.All(x => x.grabber is not Player)");
            }
            // non-proximity only
            if (medic.Submersion > 0.8f && Options.Mode.Value != "P")
            {
                Log($"{medic} can't revive {patient}: medic.Submersion > 0.8f");
            }
            if (medic.exhausted && Options.Mode.Value != "P")
            {
                Log($"{medic} can't revive {patient}: medic.exhausted");
            }
            if (medic.lungsExhausted && Options.Mode.Value != "P")
            {
                Log($"{medic} can't revive {patient}: medic.lungsExhausted");
            }
            if (medic.gourmandExhausted && Options.Mode.Value != "P")
            {
                Log($"{medic} can't revive {patient}: medic.gourmandExhausted");
            }
            if (patient.onBack != null && Options.Mode.Value != "P")
            {
                Log($"{medic} can't revive {patient}: patient.onBack != null");
            }
            // logging cooldown
            Data(patient).debugTime = Time.time + DEBUG_INTERVAL;
        }

        if (medic == patient
            || IsRevivingDisabled(medic)
            || patient.playerState.permaDead
            || !patient.dead
            || patient.grabbedBy.Count > 1
            || patient.Submersion > 0.8f
            || Data(patient).Expired
            || (Data(patient).deaths >= Options.DeathsUntilExpire.Value && !Options.DisableExpiry.Value)
            || !medic.Consious
            || (medic.grabbedBy.Count > 0 && medic.grabbedBy.All(x => x.grabber is not Player)))
            return false;

        if (Options.Mode.Value == "P")
            return !medic.dead && Vector2.Distance(medic.firstChunk.pos, patient.firstChunk.pos) <= Options.ProximityDistance.Value;

        if (medic.Submersion > 0.8f
            || medic.exhausted
            || medic.lungsExhausted
            || medic.gourmandExhausted
            || patient.onBack != null)
            return false;

        bool corpseStill = patient.IsTileSolid(0, 0, -1) && patient.IsTileSolid(1, 0, -1) && patient.bodyChunks[0].vel.magnitude < 6;
        bool selfStill = medic.input.Take(10).All(i => i.x == 0 && i.y == 0 && !i.thrw && !i.jmp) && medic.bodyChunks[1].ContactPoint.y < 0;

        return corpseStill && selfStill && medic.bodyMode == Player.BodyModeIndex.Stand;
    }

    private static void Revive(Player player)
    {
        Log($"Revive! {player}");

        Data(player).deathTime = 0;

        player.stun = 20;
        player.airInLungs = 0.1f;
        player.exhausted = true;
        player.aerobicLevel = 1;
        if (ModManager.Watcher)
            player.injectedPoison = 0f;

        if (!Options.DisableExhaustion.Value)
            player.playerState.permanentDamageTracking = Mathf.Clamp01((float)Data(player).deaths / Options.DeathsUntilExhaustion.Value) * 0.6;

        player.playerState.alive = true;
        player.playerState.permaDead = false;
        player.dead = false;
        player.killTag = null;
        player.killTagCounter = 0;
        player.abstractCreature.abstractAI?.SetDestination(player.abstractCreature.pos);
    }

    private void CheckMeadowEnabled(On.RainWorld.orig_PostModsInit orig, RainWorld self)
    {
        orig(self);
        meadowEnabled = ModManager.ActiveMods.Any(x => x.id == "henpemaz_rainmeadow");
    }

    public void OnEnable()
    {
        On.RainWorld.Update += ErrorCatch;
        On.RainWorld.OnModsInit += RainWorld_OnModsInit;
        On.RainWorld.PostModsInit += CheckMeadowEnabled;

        new Hook(typeof(Player).GetMethod("get_Malnourished"), getMalnourished);
        On.Player.CanIPutDeadSlugOnBack += Player_CanIPutDeadSlugOnBack;
        On.Player.ctor += Player_ctor;
        On.Player.Die += Player_Die;
        On.HUD.FoodMeter.GameUpdate += FixFoodMeter;
        On.Player.Update += UpdatePlr;
        On.Creature.Violence += ReduceLife;
        On.Player.CanEatMeat += DontEatPlayers;
        On.Player.GraphicsModuleUpdated += DontMoveWhileReviving;
        IL.Player.GrabUpdate += Player_GrabUpdate;

        // Fixes corpse being dropped when pressing Grab
        On.Player.GrabUpdate += FixHeavyCarry;
        On.Player.HeavyCarry += FixHeavyCarry;

        On.PlayerGraphics.Update += PlayerGraphics_Update;
        //IL.PlayerGraphics.DrawSprites += ChangeHeadSprite; // causes invisible slugcats when Rain Meadow is also enabled, yeet
        On.PlayerGraphics.DrawSprites += PlayerGraphics_DrawSprites;
        On.SlugcatHand.Update += SlugcatHand_Update;

        On.Menu.PauseMenu.ctor += ShowCurrentSettings;

        // prevents slugpups from picking other items up while holding a dead slugcat, cause otherwise they then drop the slugcat
        On.Player.CanIPickThisUp += NoPickupWhileCprAsSlup;
    }

    private void ErrorCatch(On.RainWorld.orig_Update orig, RainWorld self)
    {
        try
        {
            orig(self);
        }
        catch (Exception e)
        {
            Logger.LogError(e);
            throw;
        }
    }

    private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);

        MachineConnector.SetRegisteredOI("Wonky.RevivifyOmni", new Options());
    }

    private readonly Func<Func<Player, bool>, Player, bool> getMalnourished = (orig, self) =>
    {
        return orig(self) ||
            (Data(self).deaths >= Options.DeathsUntilExhaustion.Value && !Options.DisableExhaustion.Value);
    };

    private bool Player_CanIPutDeadSlugOnBack(On.Player.orig_CanIPutDeadSlugOnBack orig, Player self, Player pickUpCandidate)
    {
        return orig(self, pickUpCandidate) ||
            (pickUpCandidate != null && self.slugOnBack != null && !Data(pickUpCandidate).Expired);
    }

    private void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
    {
        orig(self, abstractCreature, world);

        if (self.dead)
        {
            Data(self).expireTime = int.MaxValue;
        }
        Data(self).proximityExposure = 0;
    }

    private void Player_Die(On.Player.orig_Die orig, Player self)
    {
        if (!self.dead)
        {
            if (self.drown > 0.25f || self.rainDeath > 0.25f)
            {
                Data(self).waterInLungs = 1;
            }
            Data(self).deaths++;
        }
        Data(self).proximityExposure = 0;

        orig(self);
    }

    private void FixFoodMeter(On.HUD.FoodMeter.orig_GameUpdate orig, HUD.FoodMeter self)
    {
        orig(self);

        if (self.IsPupFoodMeter)
        {
            self.survivalLimit = self.pup.slugcatStats.foodToHibernate;
        }
    }

    private void UpdatePlr(On.Player.orig_Update orig, Player self, bool eu)
    {
        orig(self, eu);

        const int ONE_SECOND = 40;
        const int TICKS_TO_DIE = 30 * ONE_SECOND;
        const int TICKS_TO_REVIVE = 10 * ONE_SECOND;

        if (Options.Mode.Value == "C")
        {
            // meadow/online
            // will attempt to ReviveRPC the patient, but if they don't have Omni, they'll have to deal with it client-side
            if (meadowEnabled && Meadow.Meadow.IsOnlineSession() && !Options.DisableRPC.Value)
            {
                if (self.grasps.FirstOrDefault()?.grabbed is not Player patient)
                    return;

                if (patient.isSlugpup && patient.AI != null && Data(patient).deaths >= Options.DeathsUntilComa.Value && !Options.DisableExhaustion.Value)
                    patient.stun = 100;
            
                if (!self.dead)
                {
                    ref float death = ref Data(patient).deathTime;
            
                    if (death > 0.1f)
                    {
                        Data(patient).expireTime++;
                    }
                    else
                    {
                        Data(patient).expireTime = 0;
                    }
            
                    if (death > -0.1f)
                    {
                        death += 1f / TICKS_TO_DIE;
                    }
                    if (death < -0.5f && self.dangerGrasp == null)
                    {
                        death -= 1f / TICKS_TO_REVIVE;
            
                        if (patient.room?.shelterDoor != null && patient.room.shelterDoor.IsClosing)
                        {
                            death = -1.1f;
                        }
                    }
                    if (death < -1f)
                    {
                        if (Options.DebugMode.Value) Log($"Invoking ReviveRPC on {patient}");
                        Meadow.Meadow.InvokeReviveRPC(patient);
            
                        if (patient.grabbedBy.FirstOrDefault()?.grabber is Player medic)
                        {
                            medic.ThrowObject(patient.grabbedBy[0].graspUsed, eu);
                        }

                        death = 0f;
                    }
            
                    death = Mathf.Clamp(death, -1, 1);
                }
                else if (Data(patient).waterInLungs > 0 && UnityEngine.Random.value < 1 / 40f && patient.Consious)
                {
                    Data(patient).waterInLungs -= UnityEngine.Random.value / 4f;
            
                    G(patient).breath = Mathf.PI;
            
                    patient.Stun(20);
                    patient.Blink(10);
                    patient.airInLungs = 0;
                    patient.firstChunk.pos += patient.firstChunk.Rotation * 3;
            
                    int amount = UnityEngine.Random.Range(3, 6);
                    for (int i = 0; i < amount; i++)
                    {
                        Vector2 dir = Custom.RotateAroundOrigo(patient.firstChunk.Rotation, -40f + 80f * UnityEngine.Random.value);
            
                        patient.room.AddObject(new WaterDrip(patient.firstChunk.pos + dir * 30, dir * (3 + 6 * UnityEngine.Random.value), true));
                    }
                }
                else
                {
                    Data(patient).deathTime = 0;
                }
            
                if (Data(patient).deaths >= Options.DeathsUntilExhaustion.Value && !Options.DisableExhaustion.Value)
                {
                    if (patient.isSlugpup && patient.AI != null)
                    {
                        patient.slugcatStats.foodToHibernate = patient.slugcatStats.maxFood;
                    }
                    if (patient.aerobicLevel >= 1f)
                    {
                        Data(patient).exhausted = true;
                    }
                    else if (patient.aerobicLevel < 0.3f)
                    {
                        Data(patient).exhausted = false;
                    }
                    if (Data(patient).exhausted)
                    {
                        patient.slowMovementStun = Math.Max(patient.slowMovementStun, (int)Custom.LerpMap(patient.aerobicLevel, 0.7f, 0.4f, 6f, 0f));
                        if (patient.aerobicLevel > 0.9f && UnityEngine.Random.value < 0.04f)
                        {
                            patient.Stun(10);
                        }
                        if (patient.aerobicLevel > 0.9f && UnityEngine.Random.value < 0.1f)
                        {
                            patient.standing = false;
                        }
                        if (!(patient.lungsExhausted && patient.animation != Player.AnimationIndex.SurfaceSwim))
                        {
                            patient.swimCycle += 0.05f;
                        }
                    }
                }
            }
            // local/offline
            // dealing with being revived client-side as the patient
            if (self.dead && self.grabbedBy.Any(x => x.grabber is Player))
            {
                if (self.isSlugpup && self.AI != null && Data(self).deaths >= Options.DeathsUntilComa.Value && !Options.DisableExhaustion.Value)
                    self.stun = 100;

                if (self.dead)
                {
                    ref float death = ref Data(self).deathTime;

                    if (death > 0.1f)
                    {
                        Data(self).expireTime++;
                    }
                    else
                    {
                        Data(self).expireTime = 0;
                    }

                    if (death > -0.1f)
                    {
                        death += 1f / TICKS_TO_DIE;
                    }
                    if (death < -0.5f && self.dangerGrasp == null)
                    {
                        death -= 1f / TICKS_TO_REVIVE;

                        if (self.room?.shelterDoor != null && self.room.shelterDoor.IsClosing)
                        {
                            death = -1.1f;
                        }
                    }
                    if (death < -1f)
                    {
                        Revive(self);

                        if (self.grabbedBy.FirstOrDefault()?.grabber is Player medic)
                        {
                            medic.ThrowObject(self.grabbedBy[0].graspUsed, eu);
                        }
                    }

                    death = Mathf.Clamp(death, -1, 1);
                }
                else if (Data(self).waterInLungs > 0 && UnityEngine.Random.value < 1 / 40f && self.Consious)
                {
                    Data(self).waterInLungs -= UnityEngine.Random.value / 4f;

                    G(self).breath = Mathf.PI;

                    self.Stun(20);
                    self.Blink(10);
                    self.airInLungs = 0;
                    self.firstChunk.pos += self.firstChunk.Rotation * 3;

                    int amount = UnityEngine.Random.Range(3, 6);
                    for (int i = 0; i < amount; i++)
                    {
                        Vector2 dir = Custom.RotateAroundOrigo(self.firstChunk.Rotation, -40f + 80f * UnityEngine.Random.value);

                        self.room.AddObject(new WaterDrip(self.firstChunk.pos + dir * 30, dir * (3 + 6 * UnityEngine.Random.value), true));
                    }
                }
                else
                {
                    Data(self).deathTime = 0;
                }

                if (Data(self).deaths >= Options.DeathsUntilExhaustion.Value && !Options.DisableExhaustion.Value)
                {
                    if (self.isSlugpup && self.AI != null)
                    {
                        self.slugcatStats.foodToHibernate = self.slugcatStats.maxFood;
                    }
                    if (self.aerobicLevel >= 1f)
                    {
                        Data(self).exhausted = true;
                    }
                    else if (self.aerobicLevel < 0.3f)
                    {
                        Data(self).exhausted = false;
                    }
                    if (Data(self).exhausted)
                    {
                        self.slowMovementStun = Math.Max(self.slowMovementStun, (int)Custom.LerpMap(self.aerobicLevel, 0.7f, 0.4f, 6f, 0f));
                        if (self.aerobicLevel > 0.9f && UnityEngine.Random.value < 0.04f)
                        {
                            self.Stun(10);
                        }
                        if (self.aerobicLevel > 0.9f && UnityEngine.Random.value < 0.1f)
                        {
                            self.standing = false;
                        }
                        if (!(self.lungsExhausted && self.animation != Player.AnimationIndex.SurfaceSwim))
                        {
                            self.swimCycle += 0.05f;
                        }
                    }
                }
            }
        }
        else if (Options.Mode.Value == "P")
        {
            // meadow/online
            // will attempt to ReviveRPC the patient, but if they don't have Omni, they'll have to deal with it client-side
            if (meadowEnabled && Meadow.Meadow.IsOnlineSession() && !self.dead && !Options.DisableRPC.Value)
            {
                foreach (var entity in self.room?.abstractRoom.entities)
                {
                    if (entity is AbstractCreature { realizedCreature: Player patient } && Meadow.Meadow.IsRemote(patient))
                    {
                        if (self == patient)
                            continue;
            
                        if (!CanRevive(self, patient))
                        {
                            Data(patient).proximityExposureRpc = 0;
                            continue;
                        }
            
                        if (Data(patient).proximityExposureRpc >= ONE_SECOND * Options.ProximityTime.Value)
                        {
                            if (Options.DebugMode.Value) Log($"Invoking ReviveRPC on {patient}");
                            Meadow.Meadow.InvokeReviveRPC(patient);
                            Data(patient).proximityExposureRpc = 0;
                        }
                        else
                        {
                            Data(patient).proximityExposureRpc++;
                        }
                    }
                }
            }
            // local/offline
            // fallback to reviving oneself client-side if near alive players
            if (self.dead)
            {
                Data(self).exposureNeedsIncrementing = true; // make sure only one medic progresses the reviving process

                HashSet<Player> nearbyMedics = new();
                foreach (var entity in self.room?.abstractRoom.entities)
                {
                    if (entity is AbstractCreature { realizedCreature: Player medic } && medic != self)
                    {
                        nearbyMedics.Add(medic);
                    }
                }

                foreach (var medic in nearbyMedics)
                {
                    if (nearbyMedics.All(medic => !CanRevive(medic, self)))
                    {
                        Data(self).proximityExposure = 0;
                        break;
                    }

                    if (Data(self).proximityExposure >= ONE_SECOND * Options.ProximityTime.Value)
                    {
                        Revive(self);
                        Data(self).proximityExposure = 0;
                    }
                    else
                    {
                        if (Data(self).exposureNeedsIncrementing) Data(self).proximityExposure++;
                        Data(self).exposureNeedsIncrementing = false;
                    }
                }
            }
        }
    }

    private void ReduceLife(On.Creature.orig_Violence orig, Creature self, BodyChunk source, Vector2? directionAndMomentum, BodyChunk hitChunk, PhysicalObject.Appendage.Pos hitAppendage, Creature.DamageType type, float damage, float stunBonus)
    {
        bool wasDead = self.dead;

        orig(self, source, directionAndMomentum, hitChunk, hitAppendage, type, damage, stunBonus);

        if (self is Player p && wasDead && p.dead && damage > 0)
        {
            PlayerData data = Data(p);
            if (data.deathTime < 0)
            {
                data.deathTime = 0;
            }
            data.deathTime += damage * 0.34f;
        }
    }

    private bool DontEatPlayers(On.Player.orig_CanEatMeat orig, Player self, Creature crit)
    {
        return crit is not Player && orig(self, crit);
    }

    private void DontMoveWhileReviving(On.Player.orig_GraphicsModuleUpdated orig, Player self, bool actuallyViewed, bool eu)
    {
        if (Options.Mode.Value == "P" || Options.Mode.Value == "N")
        {
            orig(self, actuallyViewed, eu);
            return;
        }

        Vector2 pos1 = default, pos2 = default, vel1 = default, vel2 = default;
        Vector2 posH = default, posB = default, velH = default, velB = default;

        foreach (var grasp in self.grasps)
        {
            if (grasp?.grabbed is Player p && CanRevive(self, p))
            {
                posH = self.bodyChunks[0].pos;
                posB = self.bodyChunks[1].pos;
                velH = self.bodyChunks[0].vel;
                velB = self.bodyChunks[1].vel;

                pos1 = p.bodyChunks[0].pos;
                pos2 = p.bodyChunks[1].pos;
                vel1 = p.bodyChunks[0].vel;
                vel2 = p.bodyChunks[1].vel;
                break;
            }
        }

        orig(self, actuallyViewed, eu);

        if (pos1 != default)
        {
            foreach (var grasp in self.grasps)
            {
                if (grasp?.grabbed is Player p && CanRevive(self, p))
                {
                    self.bodyChunks[0].pos = posH;
                    self.bodyChunks[1].pos = posB;
                    self.bodyChunks[0].vel = velH;
                    self.bodyChunks[1].vel = velB;

                    p.bodyChunks[0].pos = pos1;
                    p.bodyChunks[1].pos = pos2;
                    p.bodyChunks[0].vel = vel1;
                    p.bodyChunks[1].vel = vel2;
                    break;
                }
            }
        }
    }

    private void Player_GrabUpdate(ILContext il)
    {
        try
        {
            ILCursor cursor = new(il);

            // Move after num11 check and ModManager.MSC
            cursor.GotoNext(MoveType.After, i => i.MatchStloc(8));
            cursor.Index++;
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldloc_S, il.Body.Variables[8]);
            cursor.EmitDelegate(UpdateRevive);
            cursor.Emit(OpCodes.Brfalse, cursor.Next);
            cursor.Emit(OpCodes.Pop); // pop "ModManager.MSC" off stack
            cursor.Emit(OpCodes.Ret);
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }
    }

    private bool UpdateRevive(Player self, int grasp)
    {
        if (Options.Mode.Value == "P" || Options.Mode.Value == "N")
            return false;

        PlayerData data = Data(self);

        if (self.grasps[grasp]?.grabbed is not Player patient || !CanRevive(self, patient))
        {
            data.Unprepared();
            return false;
        }

        Vector2 heartPos = HeartPos(patient);
        Vector2 targetHeadPos = heartPos + new Vector2(0, Mathf.Sign(self.room.gravity)) * 25;
        Vector2 targetButtPos = heartPos - new Vector2(0, patient.bodyChunks[0].rad);
        float headDist = (targetHeadPos - self.bodyChunks[0].pos).magnitude;
        float buttDist = (targetButtPos - self.bodyChunks[1].pos).magnitude;

        if (data.animTime < 0 && (headDist > 22 || buttDist > 22))
        {
            return false;
        }

        self.bodyChunks[0].vel += Mathf.Min(headDist, 0.4f) * (targetHeadPos - self.bodyChunks[0].pos).normalized;
        self.bodyChunks[1].vel += Mathf.Min(buttDist, 0.4f) * (targetButtPos - self.bodyChunks[1].pos).normalized;

        PlayerData patientData = Data(patient);
        int difference = self.room.game.clock - patientData.lastCompression;

        if (data.animTime < 0)
        {
            data.PreparedToGiveCpr();
        }
        else if ((self.input[0].pckp && !self.input[1].pckp && difference > 4) || (self.isSlugpup && (self.input[0].pckp || self.input[1].pckp) && difference > 4))
        {
            Compression(self, grasp, data, patient, patientData, difference);
        }

        AnimationStage stage = data.Stage();

        if (stage is AnimationStage.Prepared or AnimationStage.CompressionRest)
        {
            self.bodyChunkConnections[0].distance = 14;
        }
        if (stage is AnimationStage.CompressionDown)
        {
            self.bodyChunkConnections[0].distance = 13 - data.compressionDepth;
        }
        if (stage is AnimationStage.CompressionUp)
        {
            self.bodyChunkConnections[0].distance = Mathf.Lerp(13 - data.compressionDepth, 15, (data.animTime - 3) / 2f);
        }

        if (data.animTime > 0)
        {
            data.animTime++;
        }
        if (Data(patient).compressionsUntilBreath > 0)
        {
            if (data.animTime >= 20)
                data.PreparedToGiveCpr();
        }
        else if (data.animTime >= 80)
        {
            data.PreparedToGiveCpr();
        }

        return false;
    }

    private static bool disableHeavyCarry = false;
    private void FixHeavyCarry(On.Player.orig_GrabUpdate orig, Player self, bool eu)
    {
        try
        {
            disableHeavyCarry = true;
            orig(self, eu);
        }
        finally
        {
            disableHeavyCarry = false;
        }
    }
    private bool FixHeavyCarry(On.Player.orig_HeavyCarry orig, Player self, PhysicalObject obj)
    {
        return !(disableHeavyCarry && obj is Player p && CanRevive(self, p)) && orig(self, obj);
    }

    private static void Compression(Player self, int grasp, PlayerData data, Player patient, PlayerData patientData, int difference)
    {
        if (self.slugOnBack != null)
        {
            self.slugOnBack.interactionLocked = true;
            self.slugOnBack.counter = 0;
        }

        if (self.grasps[grasp].chunkGrabbed == 1)
        {
            self.grasps[grasp].chunkGrabbed = 0;
        }

        if (patient.AI != null)
        {
            patient.State.socialMemory.GetOrInitiateRelationship(self.abstractCreature.ID).InfluenceLike(10f);
            patient.State.socialMemory.GetOrInitiateRelationship(self.abstractCreature.ID).InfluenceTempLike(10f);
            patient.State.socialMemory.GetOrInitiateRelationship(self.abstractCreature.ID).InfluenceKnow(0.5f);
        }

        for (int i = patient.abstractCreature.stuckObjects.Count - 1; i >= 0; i--)
        {
            if (patient.abstractCreature.stuckObjects[i] is AbstractPhysicalObject.AbstractSpearStick stick && stick.A.realizedObject is Spear s)
            {
                s.ChangeMode(Weapon.Mode.Free);
            }
        }

        data.StartCompression();
        self.AerobicIncrease(0.5f);

        patientData.compressionsUntilBreath--;
        if (patientData.compressionsUntilBreath < 0)
        {
            patientData.compressionsUntilBreath = 1000;//(int)(8 + UnityEngine.Random.value * 5);
        }

        bool breathing = patientData.compressionsUntilBreath == 0;
        float healing = difference switch
        {
            < 75 when breathing => -1 / 10f,
            < 100 when breathing => 1 / 5f,
            < 8 => -1 / 30f,
            < 19 => 1 / 40f,
            < 22 => 1 / 15f,
            < 30 => 1 / 20f,
            _ => 1 / 40f,
        };
        data.compressionDepth = difference switch
        {
            < 8 => 0.2f,
            < 19 => 1f,
            < 22 => 4.5f,
            < 30 => 3.5f,
            _ => 1f
        };
        if (data.compressionDepth > 4) self.Blink(6);
        patientData.deathTime -= healing * Options.ReviveSpeed.Value;
        patientData.lastCompression = self.room.game.clock;

        if (patientData.waterInLungs > 0)
        {
            patientData.waterInLungs -= healing * 0.34f;

            float amount = data.compressionDepth * 0.5f + UnityEngine.Random.value - 0.5f;
            for (int i = 0; i < amount; i++)
            {
                Vector2 dir = Custom.RotateAroundOrigo(new Vector2(0, 1), -30f + 60f * UnityEngine.Random.value);

                patient.room.AddObject(new WaterDrip(patient.firstChunk.pos + dir * 10, dir * (2 + 4 * UnityEngine.Random.value), true));
            }
        }
    }

    private void PlayerGraphics_Update(On.PlayerGraphics.orig_Update orig, PlayerGraphics self)
    {
        orig(self);

        if (Options.Mode.Value == "P" || Options.Mode.Value == "N")
            return;

        PlayerData data = Data(self.player);

        float visualDecay = Options.DisableExhaustion.Value ? 0f : Mathf.Max(Mathf.Clamp01(data.deathTime), Mathf.Clamp01((float)data.deaths / Options.DeathsUntilExhaustion.Value) * 0.6f);
        if (self.malnourished < visualDecay)
        {
            self.malnourished = visualDecay;
        }

        if (self.player.grasps.FirstOrDefault(g => g?.grabbed is Player)?.grabbed is not Player patient)
        {
            return;
        }

        AnimationStage stage = data.Stage();

        if (stage == AnimationStage.None) return;

        Vector2 starePos = stage is AnimationStage.Prepared or AnimationStage.CompressionDown or AnimationStage.CompressionUp or AnimationStage.CompressionRest
            ? HeartPos(patient)
            : patient.firstChunk.pos + patient.firstChunk.Rotation * 5;

        self.LookAtPoint(starePos, 10000f);

        if (stage is AnimationStage.CompressionDown)
        {
            // Push patient's head and butt upwards
            PlayerGraphics graf = G(patient);
            graf.head.vel.y += data.compressionDepth * 0.5f;
            graf.NudgeDrawPosition(0, new(0, data.compressionDepth * 0.5f));

            if (graf.tail.Length > 1)
            {
                graf.tail[0].pos.y += 1;
                graf.tail[0].vel.y += data.compressionDepth * 0.8f;
                graf.tail[1].vel.y += data.compressionDepth * 0.2f;
            }
        }
    }

    // this causes invisible slugcats when Rain Meadow is also enabled, yeet
    //private void ChangeHeadSprite(ILContext il)
    //{
    //    try
    //    {
    //        ILCursor cursor = new(il);
    //
    //        // Move after num11 check and ModManager.MSC
    //        cursor.GotoNext(MoveType.Before, i => i.MatchCall<PlayerGraphics>("get_RenderAsPup"));
    //        cursor.Emit(OpCodes.Ldarg_0);
    //        cursor.Emit(OpCodes.Ldloca, il.Body.Variables[9]);
    //        cursor.EmitDelegate(ChangeHead);
    //    }
    //    catch (Exception e)
    //    {
    //        Logger.LogError(e);
    //    }
    //}
    //
    //private void ChangeHead(PlayerGraphics self, ref int headNum)
    //{
    //    if (self.player.grabbedBy.Count == 1 && self.player.grabbedBy[0].grabber is Player medic && CanRevive(medic, self.player) && Data(medic).animTime >= 0)
    //        headNum = 7;
    //}

    private void PlayerGraphics_DrawSprites(On.PlayerGraphics.orig_DrawSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        orig(self, sLeaser, rCam, timeStacker, camPos);

        if (self.player.grabbedBy.Count == 1 && self.player.grabbedBy[0].grabber is Player medic && CanRevive(medic, self.player) && Data(medic).animTime >= 0)
        {
            sLeaser.sprites[9].y += 6;
            sLeaser.sprites[3].rotation -= 50 * Mathf.Sign(sLeaser.sprites[3].rotation);
            sLeaser.sprites[3].scaleX *= -1;
        }

        if (sLeaser.sprites[9].element.name == "FaceDead" && Data(self.player).deathTime < -0.6f)
            sLeaser.sprites[9].element = Futile.atlasManager.GetElementWithName("FaceStunned");
    }

    private void SlugcatHand_Update(On.SlugcatHand.orig_Update orig, SlugcatHand self)
    {
        orig(self);

        if (Options.Mode.Value == "P" || Options.Mode.Value == "N")
            return;

        Player player = ((PlayerGraphics)self.owner).player;
        PlayerData data = Data(player);

        if (player.grasps.FirstOrDefault(g => g?.grabbed is Player)?.grabbed is not Player patient)
        {
            return;
        }

        AnimationStage stage = data.Stage();

        if (stage == AnimationStage.None) return;

        Vector2 heart = HeartPos(patient);
        Vector2 heartDown = HeartPos(patient) - new Vector2(0, data.compressionDepth);

        if (stage is AnimationStage.Prepared or AnimationStage.CompressionRest)
        {
            self.pos = heart;
        }
        else if (stage == AnimationStage.CompressionDown)
        {
            self.pos = heartDown;
        }
        else if (stage == AnimationStage.CompressionUp)
        {
            self.pos = Vector2.Lerp(heartDown, heart, (data.animTime - 3) / 2f);
        }
    }

    private void ShowCurrentSettings(On.Menu.PauseMenu.orig_ctor orig, Menu.PauseMenu self, ProcessManager manager, RainWorldGame game)
    {
        orig(self, manager, game);

        if (!Options.ShowSettingsInPauseMenu.Value)
            return;

        Vector2 size = Vector2.zero;
        Vector2 pos = new(((1366f - manager.rainWorld.screenSize.x) / 2f) + 10f, game.rainWorld.screenSize.y);

        Menu.MenuLabel mode;

        const int LINE_HEIGHT = 20;

        string modeText = Options.Mode.Value switch
        {
            "C" => "CPR",
            "P" => "Proximity",
            "N" => "Disabled",
            _ => "Error"
        };

        if (Options.DisableRPC.Value) modeText += " (client-side only)";

        if (Options.Mode.Value == "N")
        {
            mode = new(self, self.pages[0], $"Revive mode: {modeText}", new Vector2(pos.x, pos.y -= LINE_HEIGHT), size, false);
            mode.label.alignment = FLabelAlignment.Left;
            self.pages[0].subObjects.Add(mode);
            return;
        }
        if (Options.DisableInArena.Value && ((meadowEnabled && Meadow.Meadow.IsOnlineArenaSession()) || self.game.IsArenaSession))
        {
            mode = new(self, self.pages[0], $"Revive mode: Disabled In Arena", new Vector2(pos.x, pos.y -= LINE_HEIGHT), size, false);
            mode.label.alignment = FLabelAlignment.Left;
            self.pages[0].subObjects.Add(mode);
            return;
        }

        mode = new(self, self.pages[0], $"Revive mode: {modeText}", new Vector2(pos.x, pos.y -= LINE_HEIGHT), size, false);
        mode.label.alignment = FLabelAlignment.Left;
        self.pages[0].subObjects.Add(mode);

        Menu.MenuLabel disableExhaustion;
        Menu.MenuLabel deathsUntilExhaustion;
        Menu.MenuLabel deathsUntilComa;

        Menu.MenuLabel disableExpiry;
        Menu.MenuLabel deathsUntilExpire;

        Menu.MenuLabel reviveSpeed;

        Menu.MenuLabel proximityDistance;
        Menu.MenuLabel proximityTime;

        if (Options.DisableExhaustion.Value)
        {
            disableExhaustion = new(self, self.pages[0], $"Exhaustion and slugpup coma are disabled", new Vector2(pos.x, pos.y -= LINE_HEIGHT), size, false);
            disableExhaustion.label.alignment = FLabelAlignment.Left;
            self.pages[0].subObjects.Add(disableExhaustion);
        }
        else
        {
            deathsUntilExhaustion = new(self, self.pages[0], $"Exhaustion after {Options.DeathsUntilExhaustion.Value} deaths", new Vector2(pos.x, pos.y -= LINE_HEIGHT), size, false);
            deathsUntilComa = new(self, self.pages[0], $"Slugpup coma after {Options.DeathsUntilComa.Value} deaths", new Vector2(pos.x, pos.y -= LINE_HEIGHT), size, false);
            deathsUntilExhaustion.label.alignment = FLabelAlignment.Left;
            deathsUntilComa.label.alignment = FLabelAlignment.Left;
            self.pages[0].subObjects.Add(deathsUntilExhaustion);
            self.pages[0].subObjects.Add(deathsUntilComa);
        }

        if (Options.DisableExpiry.Value)
        {
            disableExpiry = new(self, self.pages[0], $"Corpses never expire", new Vector2(pos.x, pos.y -= LINE_HEIGHT), size, false);
            disableExpiry.label.alignment = FLabelAlignment.Left;
            self.pages[0].subObjects.Add(disableExpiry);
        }
        else
        {
            deathsUntilExpire = new(self, self.pages[0], $"Corpses expire after {Options.DeathsUntilExpire.Value} deaths or {Math.Round(Options.CorpseExpiryTime.Value, 1)} minutes", new Vector2(pos.x, pos.y -= LINE_HEIGHT), size, false);
            deathsUntilExpire.label.alignment = FLabelAlignment.Left;
            self.pages[0].subObjects.Add(deathsUntilExpire);
        }

        if (Options.Mode.Value == "C")
        {
            reviveSpeed = new(self, self.pages[0], $"Revive speed: {Math.Round(Options.ReviveSpeed.Value, 1)}x", new Vector2(pos.x, pos.y -= LINE_HEIGHT), size, false);
            reviveSpeed.label.alignment = FLabelAlignment.Left;
            self.pages[0].subObjects.Add(reviveSpeed);
        }
        else
        {
            proximityDistance = new(self, self.pages[0], $"Revive distance: {Math.Round(Options.ProximityDistance.Value, 0)}", new Vector2(pos.x, pos.y -= LINE_HEIGHT), size, false);
            proximityTime = new(self, self.pages[0], $"Revive time: {Options.ProximityTime.Value} seconds", new Vector2(pos.x, pos.y -= LINE_HEIGHT), size, false);
            proximityDistance.label.alignment = FLabelAlignment.Left;
            proximityTime.label.alignment = FLabelAlignment.Left;
            self.pages[0].subObjects.Add(proximityDistance);
            self.pages[0].subObjects.Add(proximityTime);
        }
    }

    private bool NoPickupWhileCprAsSlup(On.Player.orig_CanIPickThisUp orig, Player self, PhysicalObject obj)
    {
        if (Options.Mode.Value != "C" && !Options.DisableSlugpupSwapCorpseWithItem.Value)
            return orig(self, obj);

        // wouldn't it be annoying if you tried to perform chest compressions on someone to save their life but a nearby spear said no
        if (self.SlugCatClass == MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName.Slugpup
            && self.grasps.FirstOrDefault()?.grabbedChunk.owner is Player patient
            && patient.dead
            && obj is not Player)
            return false;
    
        return orig(self, obj);
    }
}
