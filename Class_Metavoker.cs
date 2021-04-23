﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace ValheimLegends
{
    public class Class_Metavoker
    {
        private static int Warp_Layermask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece_nonsolid", "terrain", "vehicle", "piece", "viewblock", "Water", "character");
        private static int Light_Layermask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece_nonsolid", "terrain", "vehicle", "piece", "viewblock", "character");

        private static GameObject GO_CastFX;

        private static GameObject GO_Light;        
        private static Projectile P_Light;
        private static StatusEffect SE_Root;

        private static GameObject GO_RootDefender;
    
        private static float warpCount;
        private static float warpDistance;
        private static int warpGrowthTrigger;

        public static void Process_Input(Player player, ref float altitude, ref Rigidbody playerBody)
        {

            if(P_Light != null)
            {
                P_Light.transform.position = player.GetEyePoint() + player.transform.up * .4f + player.transform.right * -.8f;
            }

            if(ZInput.GetButton("Jump"))
            {
                
                if (!player.IsOnGround() && !player.IsDead() && !player.InAttack() && !player.IsEncumbered() && !player.InDodge() && !player.IsKnockedBack())
                {
                    //Rigidbody playerBody = Traverse.Create(root: player).Field(name: "m_body").GetValue<Rigidbody>();
                    Vector3 velocity = playerBody.velocity;
                    if (velocity.y < 0)
                    {
                        bool flag = true;
                        if (!player.HaveStamina(1f))
                        {
                            if (player.IsPlayer())
                            {
                                Hud.instance.StaminaBarNoStaminaFlash();
                            }
                            flag = false;
                        }
                        if (flag)
                        {
                            player.UseStamina(.6f);
                            //ZSyncAnimation zanim = Traverse.Create(root: player).Field(name: "m_zanim").GetValue<ZSyncAnimation>();
                            //Animator anim = Traverse.Create(root: player).Field(name: "m_animator").GetValue<Animator>();
                            float heightAboveGround = ZoneSystem.instance.GetSolidHeight(player.transform.position);
                            float heightDiff = altitude - heightAboveGround;
                            float _vy = Mathf.Clamp(-.15f * velocity.y, 0f, 1.5f);
                            float v_r = _vy / (-velocity.y);
                            float alt_r = heightDiff * (1f - v_r);
                            //ZLog.Log("adjusting " + velocity.y + " by " + _vy);
                            playerBody.velocity = velocity + new Vector3(0f, _vy, 0f);
                            //ZLog.Log("velocity y " + _vy + " adjusted to " + velocity.y);
                            //ZLog.Log("max height " + altitude + " adjusted to " + (altitude - alt_r));
                            altitude = (altitude - alt_r);
                            //player.StartEmote("sit");
                            //RuntimeAnimatorController PlayerRAC;
                            //PlayerRAC = player.gameObject.GetComponentInChildren<Animator>().runtimeAnimatorController;
                            //AnimationClipOverrides animationClipOverrides = new AnimationClipOverrides(PlayerRAC.animationClips.Length);
                            //AnimatorOverrideController PlayerFlyingAOC = new AnimatorOverrideController(PlayerRAC);
                            //PlayerFlyingAOC.GetOverrides(animationClipOverrides);
                            //AnimationClip[] animationClips = PlayerRAC.animationClips;
                            //foreach (AnimationClip animationClip in animationClips)
                            //{
                            //    if (animationClip.name == "jump")
                            //    {
                            //        animationClipOverrides[animationClip.name] = ValheimLegends.anim_player_float;
                            //    }
                            //}
                            //PlayerFlyingAOC.ApplyOverrides(animationClipOverrides);
                            //anim.SetTrigger("pre_landing");
                            UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("fx_VL_ReverseLightburst"), player.transform.position, Quaternion.LookRotation(new Vector3(0f, 1f, 0f)));
                        }
                    }
                }
            }

            if (VL_Utility.Ability3_Input_Down)
            {
                if (!player.GetSEMan().HaveStatusEffect("SE_VL_Ability3_CD"))
                {
                    ValheimLegends.shouldUseGuardianPower = false;
                    //player.Message(MessageHud.MessageType.Center, "root - starting");
                    if (player.GetStamina() >= VL_Utility.GetWarpCost && !ValheimLegends.isChanneling)
                    {
                        
                        //Ability Cooldown
                        StatusEffect se_cd = (SE_Ability3_CD)ScriptableObject.CreateInstance(typeof(SE_Ability3_CD));
                        se_cd.m_ttl = VL_Utility.GetWarpCooldownTime;
                        player.GetSEMan().AddStatusEffect(se_cd);

                        //Ability Cost
                        player.UseStamina(VL_Utility.GetWarpCost);

                        //Effects, animations, and sounds
                        //((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer)).SetTrigger("gpower");
                        //((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer)).SetSpeed(.3f);
                        VL_Utility.RotatePlayerToTarget(player);
                        player.StartEmote("point");
                        ValheimLegends.isChanneling = true;
                        
                        //Lingering effects

                        //Skill influence

                        //Apply effects
                        warpDistance = 15f;
                        warpGrowthTrigger = 10;

                        //Skill gain
                        player.RaiseSkill(ValheimLegends.EvocationSkill, VL_Utility.GetWarpSkillGain);
                    }
                    else
                    {
                        player.Message(MessageHud.MessageType.TopLeft, "Not enough stamina to initiate Warp: (" + player.GetStamina().ToString("#.#") + "/" + VL_Utility.GetWarpCost + ")");
                    }
                }
                else
                {
                    player.Message(MessageHud.MessageType.TopLeft, "Ability not ready");
                }
            }
            else if (VL_Utility.Ability3_Input_Pressed && player.GetStamina() > VL_Utility.GetWarpCostPerUpdate && ValheimLegends.isChanneling && Mathf.Max(0f, altitude - player.transform.position.y) <= 2f)
            {
                warpCount++;
                player.UseStamina(VL_Utility.GetWarpCostPerUpdate);
                //player.transform.rotation = Quaternion.LookRotation(player.GetLookDir());
                ValheimLegends.isChanneling = true;
                if (warpCount >= warpGrowthTrigger)
                {
                    warpCount = 0;
                    //Skill gain
                    UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("fx_VL_ParticleLightSuction"), player.transform.position, Quaternion.identity);                    
                    player.RaiseSkill(ValheimLegends.EvocationSkill, .04f);
                    warpDistance += 5f; 
                }
            }
            else if (((VL_Utility.Ability3_Input_Up || player.GetStamina() <= VL_Utility.GetWarpCostPerUpdate || player.GetStamina() <= 2f) && ValheimLegends.isChanneling))// || Mathf.Max(0f, altitude - player.transform.position.y) > 2f)
            {
                //player.Message(MessageHud.MessageType.Center, "root - deactivate");
                float sLevel = player.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == ValheimLegends.EvocationSkillDef).m_level;
                warpDistance = warpDistance * (1f + (.02f * sLevel));
                //ZLog.Log("triggering warp with  distance of " + warpDistance);
                ValheimLegends.isChanneling = false;
                RaycastHit hitInfo = default(RaycastHit);
                //((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer)).SetTrigger("unarmed_attack0");
                Vector3 position = player.GetEyePoint();
                Vector3 target = (!Physics.Raycast(player.GetEyePoint(), player.GetLookDir(), out hitInfo, float.PositiveInfinity, Warp_Layermask) || !(bool)hitInfo.collider) ? (position + player.GetLookDir() * 1000f) : hitInfo.point;

                Vector3 a = Vector3.MoveTowards(position, target, 1f);
                float distanceMagnitude = (hitInfo.point - position).magnitude;
                float warpMagnitude = (warpDistance * player.GetLookDir()).magnitude;
                //ZLog.Log("distance mag: " + distanceMagnitude + " warp mag: " + warpMagnitude);
                //ZLog.Log("hitinfo distance " + hitInfo.distance);
                float flagDamage = 0f;
                if(warpMagnitude > distanceMagnitude)
                {
                    flagDamage = warpMagnitude - distanceMagnitude;
                    warpMagnitude = distanceMagnitude;
                }
                bool flagFarWarp = warpMagnitude >= 200 ? true : false;

                Vector3 moveVec = Vector3.MoveTowards(player.transform.position, target, (float)warpMagnitude);
                //moveVec.y = ((ZoneSystem.instance.GetSolidHeight(moveVec) - ZoneSystem.instance.GetGroundHeight(moveVec) <= 1f) ? ZoneSystem.instance.GetSolidHeight(moveVec) : ZoneSystem.instance.GetGroundHeight(moveVec));
                Vector3 effectVec = (moveVec + (player.GetLookDir() * -10f));
                
                UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("fx_VL_ParticleLightburst"), player.GetEyePoint(), Quaternion.LookRotation(player.GetLookDir()));
                if (warpMagnitude > 0f)
                {
                    //ZLog.Log("damage magnitude is " + flagDamage);
                    
                    UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("fx_eikthyr_forwardshockwave"), effectVec, Quaternion.LookRotation(player.GetLookDir()));
                    //Apply effects
                    List<Character> allCharacters = Character.GetAllCharacters();
                    foreach (Character ch in allCharacters)
                    {
                        if ((BaseAI.IsEnemy(player, ch) && (ch.transform.position - moveVec).magnitude <= 8f + (.02f * sLevel)))
                        {
                            Vector3 direction = (ch.transform.position - player.transform.position);
                            HitData hitData = new HitData();
                            hitData.m_damage.m_lightning = UnityEngine.Random.Range(flagDamage * (sLevel/30f), flagDamage * (sLevel/20f)) * VL_GlobalConfigs.g_DamageModifer;
                            hitData.m_pushForce = flagDamage + (.1f * sLevel);
                            hitData.m_point = ch.GetEyePoint();
                            hitData.m_dir = (player.transform.position - ch.transform.position);
                            hitData.m_skill = ValheimLegends.EvocationSkill;
                            ch.ApplyDamage(hitData, true, true, HitData.DamageModifier.Normal);
                        }
                    }
                }
                else
                {
                    UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("fx_eikthyr_forwardshockwave"), effectVec, player.transform.rotation);
                    
                }
                if (flagFarWarp)
                {
                    player.TeleportTo(moveVec, player.transform.rotation, false);
                }
                else
                {
                    //ZLog.Log("zone loaded?" + ZoneSystem.instance.IsZoneLoaded(moveVec));
                    player.transform.position = moveVec;
                }
                
                
                //player.TeleportTo(moveVec, player.transform.rotation, false);
                altitude = 0f;
                
            }
            else if(VL_Utility.Ability2_Input_Down)
            {
                if (!player.GetSEMan().HaveStatusEffect("SE_VL_Ability2_CD"))
                {
                    //player.Message(MessageHud.MessageType.Center, "Plant defenders");
                    if (player.GetStamina() >= VL_Utility.GetReplicaCost)
                    {
                        Vector3 lookVec = player.GetLookDir();
                        lookVec.y = 0f;
                        player.transform.rotation = Quaternion.LookRotation(lookVec);

                        ValheimLegends.shouldUseGuardianPower = false;
                        //Ability Cooldown
                        StatusEffect se_cd = (SE_Ability2_CD)ScriptableObject.CreateInstance(typeof(SE_Ability2_CD));
                        se_cd.m_ttl = VL_Utility.GetReplicaCooldownTime;
                        player.GetSEMan().AddStatusEffect(se_cd);

                        //Ability Cost
                        player.UseStamina(VL_Utility.GetReplicaCost);

                        //Skill influence
                        float sLevel = player.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == ValheimLegends.IllusionSkillDef).m_level;

                        //Effects, animations, and sounds
                        ((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer)).SetTrigger("gpower");
                        //((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer)).SetSpeed(.7f);
                        UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("vfx_WishbonePing"), player.transform.position, Quaternion.identity);

                        //Lingering effects

                        //Apply effects

                        //Apply effects
                        List<Character> allCharacters = new List<Character>();
                        foreach(Character chr in Character.GetAllCharacters())
                        {
                            allCharacters.Add(chr);
                        }
                        for(int i = 0; i < allCharacters.Count; i++)
                        {
                            Character ch = allCharacters[i];
                            if ((BaseAI.IsEnemy(player, ch) && (ch.transform.position - player.transform.position).magnitude <= 30f + (.3f * sLevel)))
                            {
                                string name = ch.name.Substring(0, ch.name.IndexOf('('));
                                GameObject original = ZNetScene.instance.GetPrefab(name);
                                if (original != null)
                                {
                                    original.AddComponent<CharacterTimedDestruction>();
                                    original.GetComponent<CharacterTimedDestruction>().m_timeoutMin = 8f + (.2f * sLevel);
                                    original.GetComponent<CharacterTimedDestruction>().m_timeoutMax = 8f + (.2f * sLevel); 
                                    Vector3 rootVec = ch.transform.position;
                                    rootVec.x += (5f * UnityEngine.Random.Range(-1f, 1f));
                                    GameObject replica = UnityEngine.Object.Instantiate(original, rootVec, Quaternion.Inverse(ch.transform.rotation));
                                    CharacterTimedDestruction td = replica.GetComponent<CharacterTimedDestruction>();
                                    if (td != null)
                                    {
                                        //ZLog.Log("td valid: " + td.isActiveAndEnabled + " timeout min " + td.m_timeoutMin + " timeout max " + td.m_timeoutMax);
                                        td.m_timeoutMin = 8f + (.2f * sLevel);
                                        td.m_timeoutMax = td.m_timeoutMin;
                                    }
                                    Character repCh = replica.GetComponent<Character>();
                                    repCh.SetMaxHealth(1f + sLevel);
                                    SE_Companion se_companion = (SE_Companion)ScriptableObject.CreateInstance(typeof(SE_Companion));
                                    se_companion.m_ttl = 8f + (.2f * sLevel);
                                    se_companion.damageModifier = .05f + (.0075f * sLevel) * VL_GlobalConfigs.g_DamageModifer;
                                    se_companion.summoner = player;
                                    repCh.GetSEMan().AddStatusEffect(se_companion);
                                    repCh.m_faction = Character.Faction.Players;
                                    CharacterDrop comp = repCh.GetComponent<CharacterDrop>();
                                    if (comp != null)
                                    {
                                        comp.m_drops.Clear();
                                    }
                                    repCh.name = "VL_" + repCh.name;
                                    repCh.m_name = "(" + repCh.m_name + ")";
                                }
                                else
                                {
                                    //ZLog.Log("replica failed for " + ch.name);
                                }
                                //replica.SetTamed(true);
                            }
                        }

                        //Skill gain
                        player.RaiseSkill(ValheimLegends.IllusionSkill, VL_Utility.GetReplicaCost);
                    }
                    else
                    {
                        player.Message(MessageHud.MessageType.TopLeft, "Not enough stamina to create illusions: (" + player.GetStamina().ToString("#.#") + "/" + VL_Utility.GetReplicaCost + ")");
                    }
                }
                else
                {
                    player.Message(MessageHud.MessageType.TopLeft, "Ability not ready");
                }
            }
            else if (VL_Utility.Ability1_Input_Down)
            {
                if(P_Light != null && (P_Light.transform.position - player.GetEyePoint()).magnitude < 2f)
                {
                    float sLevel = player.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == ValheimLegends.IllusionSkillDef).m_level;
                    P_Light.m_ttl = .05f;
                    
                    HitData hitData = new HitData();
                    hitData.m_skill = ValheimLegends.EvocationSkill;
                    //P_Light.Setup(player, new Vector3(0, -1000, 0), -1, hitData, null);
                    //Traverse.Create(root: P_Light).Field("m_skill").SetValue(ValheimLegends.IllusionSkill);
                    //UnityEngine.Object.Destroy(P_Light.gameObject);
                    //if (P_Light != null)
                    //{
                    //    P_Light = null;
                    //}

                    Vector3 vector = player.GetEyePoint() + player.transform.up * .5f + player.transform.right * -1f;
                    GameObject prefab = ZNetScene.instance.GetPrefab("VL_Light");
                    GameObject GO_LL = UnityEngine.Object.Instantiate(prefab, vector, Quaternion.identity);
                    Projectile P_LL = GO_LL.GetComponent<Projectile>();
                    P_LL.m_respawnItemOnHit = false;
                    P_LL.m_spawnOnHit = null;
                    P_LL.m_ttl = 4f;
                    P_LL.m_gravity = .1f;
                    P_LL.m_rayRadius = .1f;
                    P_LL.transform.localRotation = Quaternion.LookRotation(player.GetAimDir(vector));
                    //P_Light.m_respawnItemOnHit = false;
                    //P_Light.m_spawnOnHit = null;
                    //P_Light.m_ttl = 3f;
                    //P_Light.m_gravity = .1f;
                    //P_Light.m_rayRadius = .1f;
                    //P_Light.transform.localRotation = Quaternion.LookRotation(player.GetAimDir(vector));
                    GO_LL.transform.localScale = Vector3.zero;

                    RaycastHit hitInfo = default(RaycastHit);
                    Vector3 position = player.transform.position;
                    Vector3 target = (!Physics.Raycast(player.GetEyePoint(), player.GetLookDir(), out hitInfo, float.PositiveInfinity, Light_Layermask) || !(bool)hitInfo.collider) ? (position + player.GetLookDir() * 1000f) : hitInfo.point;
                    hitData.m_damage.m_lightning = UnityEngine.Random.Range(2f + (.25f * sLevel), 5f + (.5f*sLevel)) * VL_GlobalConfigs.g_DamageModifer;
                    hitData.m_damage.m_pierce = UnityEngine.Random.Range(2f + (.25f * sLevel), 5f + (.5f*sLevel)) * VL_GlobalConfigs.g_DamageModifer;
                    hitData.m_pushForce = 100f + sLevel;                    
                    Vector3 a = Vector3.MoveTowards(GO_LL.transform.position, target, 1f);
                    P_LL.Setup(player, (a - GO_LL.transform.position) * 80f, -1f, hitData, null);
                    Traverse.Create(root: P_LL).Field("m_skill").SetValue(ValheimLegends.IllusionSkill);
                    //P_Light.Setup(player, (a - GO_Light.transform.position) * 80f, -1f, hitData, null);
                    //Traverse.Create(root: P_Light).Field("m_skill").SetValue(ValheimLegends.IllusionSkill);
                    GO_LL = null;
                    GO_Light = null;

                }
                else if (!player.GetSEMan().HaveStatusEffect("SE_VL_Ability1_CD"))
                {
                    //player.Message(MessageHud.MessageType.Center, "Light");
                    if (player.GetStamina() >= VL_Utility.GetLightCost)
                    {
                        //Ability Cooldown
                        StatusEffect se_cd = (SE_Ability1_CD)ScriptableObject.CreateInstance(typeof(SE_Ability1_CD));
                        se_cd.m_ttl = VL_Utility.GetLightCooldownTime;
                        player.GetSEMan().AddStatusEffect(se_cd);

                        //Ability Cost
                        player.UseStamina(VL_Utility.GetLightCost);

                        //Skill influence
                        float sLevel = player.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == ValheimLegends.IllusionSkillDef).m_level;

                        //Effects, animations, and sounds
                        //((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer)).SetTrigger("gpower");
                        player.StartEmote("cheer");
                        //GO_CastFX = UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("fx_guardstone_permitted_add"), player.GetCenterPoint(), Quaternion.identity);


                        //Lingering effects


                        //Apply effects

                        Vector3 vector = player.GetEyePoint() + player.transform.up * .4f + player.transform.right * -.8f;
                        UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("fx_VL_Lightburst"), vector, Quaternion.identity);
                        GameObject prefab = ZNetScene.instance.GetPrefab("VL_Light");
                        GO_Light = UnityEngine.Object.Instantiate(prefab, vector, Quaternion.identity);
                        P_Light = GO_Light.GetComponent<Projectile>();
                        P_Light.m_respawnItemOnHit = false;
                        P_Light.m_spawnOnHit = null;
                        P_Light.m_ttl = 300f;
                        P_Light.m_gravity = 0f;
                        P_Light.m_rayRadius = .1f;
                        P_Light.transform.localRotation = Quaternion.LookRotation(player.GetAimDir(vector));
                        GO_Light.transform.localScale = Vector3.zero;

                        RaycastHit hitInfo = default(RaycastHit);
                        Vector3 position = player.transform.position;
                        Vector3 target = (!Physics.Raycast(vector, player.GetLookDir(), out hitInfo, float.PositiveInfinity, Light_Layermask) || !(bool)hitInfo.collider) ? (position + player.GetLookDir() * 1000f) : hitInfo.point;
                        HitData hitData = new HitData();
                        //hitData.m_damage.m_fire = UnityEngine.Random.Range(10f + (2f * sLevel), 40f + (2f * sLevel)) * VL_GlobalConfigs.g_DamageModifer;
                        //hitData.m_damage.m_blunt = UnityEngine.Random.Range(5f + (1f * sLevel), 20f + (1f * sLevel)) * VL_GlobalConfigs.g_DamageModifer;
                        //hitData.m_pushForce = 2f;
                        hitData.m_skill = ValheimLegends.EvocationSkill;
                        Vector3 a = Vector3.MoveTowards(GO_Light.transform.position, target, 1f);                        
                        P_Light.Setup(player, Vector3.zero, -1f, hitData, null);
                        Traverse.Create(root: P_Light).Field("m_skill").SetValue(ValheimLegends.IllusionSkill);
                        //GO_Light = null;

                        //Skill gain
                        player.RaiseSkill(ValheimLegends.IllusionSkill, VL_Utility.GetLightSkillGain);
                    }
                    else
                    {
                        player.Message(MessageHud.MessageType.TopLeft, "Not enough stamina to for Light: (" + player.GetStamina().ToString("#.#") + "/" + VL_Utility.GetLightCost + ")");
                    }                    
                }
                else
                {
                    player.Message(MessageHud.MessageType.TopLeft, "Ability not ready");
                }
            }
            else
            {
                ValheimLegends.isChanneling = false;
            }
        }
    }
}
