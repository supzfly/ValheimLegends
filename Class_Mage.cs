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
using Unity;

namespace ValheimLegends
{
    public class Class_Mage
    {
        private static int Script_Layermask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece_nonsolid", "terrain", "vehicle", "piece", "viewblock");        

        private static GameObject GO_CastFX;

        private static GameObject GO_Fireball;        
        private static Projectile P_Fireball;
        private static StatusEffect SE_Fireball;

        private static GameObject GO_Meteor;
        private static Projectile P_Meteor;
        private static StatusEffect SE_Meteor;       
        private static bool meteorCharging = false;
        private static int meteorCount;
        private static int meteorChargeAmount;
        private static int meteorChargeAmountMax;

        private static float meteorSkillGain = 0f;

        public static void Process_Input(Player player, float altitude)
        {
            System.Random rnd = new System.Random();
            if (Input.GetKeyDown(ValheimLegends.Ability3_Hotkey.Value.ToLower()) && !meteorCharging)
            {
                if (!player.GetSEMan().HaveStatusEffect("SE_VL_Ability3_CD"))
                {
                    //player.Message(MessageHud.MessageType.Center, "Meteor - starting");
                    if (player.GetStamina() >= VL_Utility.GetMeteorCost)
                    {
                        ValheimLegends.shouldUseGuardianPower = false;
                        ValheimLegends.isChanneling = true;
                        meteorSkillGain = 0;
                        //Ability Cooldown
                        StatusEffect se_cd = (SE_Ability3_CD)ScriptableObject.CreateInstance(typeof(SE_Ability3_CD));
                        se_cd.m_ttl = VL_Utility.GetMeteorCooldownTime;
                        player.GetSEMan().AddStatusEffect(se_cd);

                        //Ability Cost
                        player.UseStamina(VL_Utility.GetMeteorCost);

                        //Skill influence
                        float sLevel = player.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == ValheimLegends.EvocationSkillDef).m_level;

                        //Effects, animations, and sounds
                        ((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer)).SetTrigger("gpower");                        
                        UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("fx_GP_Stone"), player.GetEyePoint(), Quaternion.identity);

                        //Lingering effects
                        meteorCharging = true;
                        meteorChargeAmount = 0;
                        meteorChargeAmountMax = Mathf.RoundToInt(60f * (1f - (sLevel/200f))); // modified by skill
                        meteorCount = 0;

                        //Apply effects


                        //Skill gain
                        meteorSkillGain += VL_Utility.GetMeteorSkillGain;
                    }
                    else
                    {
                        player.Message(MessageHud.MessageType.TopLeft, "Not enough stamina to begin Meteor : (" + player.GetStamina().ToString("#.#") + "/" + VL_Utility.GetMeteorCost + ")");
                    }
                }
                else
                {
                    player.Message(MessageHud.MessageType.TopLeft, "Ability not ready");
                }
            }
            else if (Input.GetKey(ValheimLegends.Ability3_Hotkey.Value.ToLower()) && meteorCharging && player.GetStamina() > 1 && Mathf.Max(0f, altitude - player.transform.position.y) <= .5f)
            {
                meteorChargeAmount++;                
                player.UseStamina(VL_Utility.GetMeteorCostPerUpdate);
                ValheimLegends.isChanneling = true;
                if (meteorChargeAmount >= meteorChargeAmountMax)
                {
                    meteorCount++;
                    meteorChargeAmount = 0;                    
                    ((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer)).SetTrigger("gpower");
                    //((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer)).SetSpeed(1.5f);                    
                    GO_CastFX = UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("fx_GP_Stone"), player.GetEyePoint(), Quaternion.identity);
                    //player.Message(MessageHud.MessageType.Center, "Meteor - charging " + meteorCount);

                    //Skill gain
                    meteorSkillGain += .2f;
                }
            }
            else if(((Input.GetKeyUp(ValheimLegends.Ability3_Hotkey.Value.ToLower()) || player.GetStamina() <= 1) && meteorCharging) || Mathf.Max(0f, altitude - player.transform.position.y) > .5f)
            { 
                //player.Message(MessageHud.MessageType.Center, "Meteor - activate");               
                
                Vector3 vector = player.transform.position + player.transform.up * 2f + player.GetLookDir() * 1f;
                GameObject prefab = ZNetScene.instance.GetPrefab("projectile_meteor");                
                meteorCharging = false;
                float sLevel = player.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == ValheimLegends.EvocationSkillDef).m_level;
                for (int i = 0; i < meteorCount; i++)
                {
                    GO_Meteor = UnityEngine.Object.Instantiate(prefab, new Vector3(vector.x + rnd.Next(-100, 100), vector.y + 250f, vector.z + rnd.Next(-100, 100)), Quaternion.identity);
                    P_Meteor = GO_Meteor.GetComponent<Projectile>();
                    P_Meteor.name = "Meteor"+i;
                    P_Meteor.m_respawnItemOnHit = false;
                    P_Meteor.m_spawnOnHit = null;
                    P_Meteor.m_ttl = 60f;
                    P_Meteor.m_gravity = 0f;
                    P_Meteor.m_rayRadius = .1f;
                    P_Meteor.m_aoe = 8f + (.04f * sLevel);                    
                    P_Meteor.transform.localRotation = Quaternion.LookRotation(player.GetAimDir(vector));
                    GO_Meteor.transform.localScale = Vector3.zero;
                    RaycastHit hitInfo = default(RaycastHit);
                    Vector3 position = player.transform.position;
                    Vector3 target = (!Physics.Raycast(vector, player.GetLookDir(), out hitInfo, 1000f, Script_Layermask) || !(bool)hitInfo.collider) ? (position + player.GetLookDir() * 1000f) : hitInfo.point;
                    target.x += rnd.Next(-10, 10);
                    target.y += rnd.Next(-10, 10);
                    target.z += rnd.Next(-10, 10);
                    HitData hitData = new HitData();
                    hitData.m_damage.m_fire = UnityEngine.Random.Range(30 + (.5f * sLevel), 50 + sLevel) * ValheimLegends.abilityDamageMultiplier.Value;
                    hitData.m_damage.m_blunt = UnityEngine.Random.Range(15 + (.25f * sLevel), 30 + (.5f * sLevel)) * ValheimLegends.abilityDamageMultiplier.Value;
                    hitData.m_pushForce = 10f;
                    Vector3 a = Vector3.MoveTowards(GO_Meteor.transform.position, target, 1f);
                    P_Meteor.Setup(null, (a - GO_Meteor.transform.position) * UnityEngine.Random.Range(45f, 55f), -1f, hitData, null);
                    GO_CastFX = UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("fx_guardstone_permitted_removed"), player.transform.position + player.transform.right * UnityEngine.Random.Range(-1f, 1f) + player.transform.up * UnityEngine.Random.Range(0, 1.5f), Quaternion.identity);
                }

                //((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer)).SetTrigger("unarmed_attack0");                
                meteorCount = 0;
                meteorChargeAmount = 0;
                GO_Meteor = null;
                //Skill gain
                player.RaiseSkill(ValheimLegends.EvocationSkill, meteorSkillGain);
                ValheimLegends.isChanneling = false;
                //GO_CastFX.transform.position = GO_CastFX.transform.position + FireCastFx.transform.up * 1.5f;
                //if ((bool)FireCastFx && FireCastFx.activeSelf)
                //{
                //    FireCastFx.SetActive(value: false);
                //}
                //FireCastFx = null;
            }
            else if(Input.GetKeyDown(ValheimLegends.Ability2_Hotkey.Value.ToLower()))
            {
                if (!player.GetSEMan().HaveStatusEffect("SE_VL_Ability2_CD"))
                {
                    //player.Message(MessageHud.MessageType.Center, "Frost Nova");
                    if (player.GetStamina() >= VL_Utility.GetFrostNovaCost)
                    {
                        //Ability Cooldown
                        StatusEffect se_cd = (SE_Ability2_CD)ScriptableObject.CreateInstance(typeof(SE_Ability2_CD));
                        se_cd.m_ttl = VL_Utility.GetFrostNovaCooldownTime;
                        player.GetSEMan().AddStatusEffect(se_cd);

                        //Ability Cost
                        player.UseStamina(VL_Utility.GetFrostNovaCost);

                        //Skill influence
                        float sLevel = player.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == ValheimLegends.EvocationSkillDef).m_level;

                        //Effects, animations, and sounds
                        //player.StartEmote("cheer");
                        ((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer)).SetTrigger("swing_axe1");                        
                        UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("fx_guardstone_activate"), player.transform.position, Quaternion.identity);

                        //Lingering effects

                        //Apply effects

                        List<Character> allCharacters = Character.GetAllCharacters();                        
                        foreach (Character ch in allCharacters)
                        {
                            if (BaseAI.IsEnemy(player, ch) && ((ch.transform.position - player.transform.position).magnitude <= (8f + (.05f * sLevel))))
                            {
                                Vector3 direction = (ch.transform.position - player.transform.position);
                                HitData hitData = new HitData();
                                hitData.m_damage.m_frost = UnityEngine.Random.Range(10 + (.5f * sLevel), 20 + sLevel) * ValheimLegends.abilityDamageMultiplier.Value;
                                hitData.m_pushForce = 50f;
                                hitData.m_point = ch.GetEyePoint();
                                hitData.m_dir = (player.transform.position - ch.transform.position);
                                ch.ApplyDamage(hitData, true, true, HitData.DamageModifier.Normal);
                            }
                        }

                        //Skill gain
                        player.RaiseSkill(ValheimLegends.EvocationSkill, VL_Utility.GetFrostNovaSkillGain);
                    }
                    else
                    {
                        player.Message(MessageHud.MessageType.TopLeft, "Not enough stamina for Frost Nova: (" + player.GetStamina().ToString("#.#") + "/" + VL_Utility.GetDefenderCost + ")");
                    }
                }
                else
                {
                    player.Message(MessageHud.MessageType.TopLeft, "Ability not ready");
                }

            }
            else if (Input.GetKeyDown(ValheimLegends.Ability1_Hotkey.Value.ToLower()))
            {
                if (!player.GetSEMan().HaveStatusEffect("SE_VL_Ability1_CD"))
                {
                    //player.Message(MessageHud.MessageType.Center, "Fireball");
                    if (player.GetStamina() >= VL_Utility.GetFireballCost)
                    {
                        ValheimLegends.shouldUseGuardianPower = false;
                        //Skill influence
                        float sLevel = player.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == ValheimLegends.EvocationSkillDef).m_level;

                        //Ability Cooldown
                        StatusEffect se_cd = (SE_Ability1_CD)ScriptableObject.CreateInstance(typeof(SE_Ability1_CD));
                        se_cd.m_ttl = VL_Utility.GetFireballCooldownTime - (.02f * sLevel);
                        player.GetSEMan().AddStatusEffect(se_cd);

                        //Ability Cost
                        player.UseStamina(VL_Utility.GetFireballCost);

                        //Effects, animations, and sounds
                        ((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer)).SetTrigger("gpower");
                        ((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer)).SetSpeed(2f);
                        ValheimLegends.isChanneling = true;
                        //player.StartEmote("point");
                        GO_CastFX = UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("fx_GP_Stone"), player.transform.position, Quaternion.identity);

                        //Lingering effects

                        //Apply effects

                        Vector3 vector = player.transform.position + player.transform.up * 1.25f + player.GetLookDir() * 1f;
                        GameObject prefab = ZNetScene.instance.GetPrefab("Imp_fireball_projectile");
                        GO_Fireball = UnityEngine.Object.Instantiate(prefab, new Vector3(vector.x, vector.y + 2.5f, vector.z), Quaternion.identity);
                        P_Fireball = GO_Fireball.GetComponent<Projectile>();
                        P_Fireball.name = "Fireball";
                        P_Fireball.m_respawnItemOnHit = false;
                        P_Fireball.m_spawnOnHit = null;
                        P_Fireball.m_ttl = 60f;
                        P_Fireball.m_gravity = 2.5f;
                        P_Fireball.m_rayRadius = .1f;
                        P_Fireball.m_aoe = 3f + (.001f * sLevel);
                        P_Fireball.transform.localRotation = Quaternion.LookRotation(player.GetAimDir(vector));
                        GO_Fireball.transform.localScale = Vector3.zero;

                        RaycastHit hitInfo = default(RaycastHit);
                        Vector3 position = player.transform.position;
                        Vector3 target = (!Physics.Raycast(vector, player.GetLookDir(), out hitInfo, float.PositiveInfinity, Script_Layermask) || !(bool)hitInfo.collider) ? (position + player.GetLookDir() * 1000f) : hitInfo.point;
                        HitData hitData = new HitData();
                        hitData.m_damage.m_fire = UnityEngine.Random.Range(10f + sLevel, 40f + sLevel) * ValheimLegends.abilityDamageMultiplier.Value;
                        hitData.m_damage.m_blunt = UnityEngine.Random.Range(5f + (.5f *sLevel), 20f + (.5f * sLevel)) * ValheimLegends.abilityDamageMultiplier.Value;
                        hitData.m_pushForce = 2f;
                        Vector3 a = Vector3.MoveTowards(GO_Fireball.transform.position, target, 1f);
                        P_Fireball.Setup(null, (a - GO_Fireball.transform.position) * 25f, -1f, hitData, null);
                        GO_Fireball = null;

                        //Skill gain
                        player.RaiseSkill(ValheimLegends.EvocationSkill, VL_Utility.GetFireballSkillGain);
                    }
                    else
                    {
                        player.Message(MessageHud.MessageType.TopLeft, "Not enough stamina to for Fireball: (" + player.GetStamina().ToString("#.#") + "/" + VL_Utility.GetFireballCost + ")");
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