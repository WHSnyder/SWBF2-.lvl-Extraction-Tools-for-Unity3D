using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;
using LibSWBF2.Utils;


public class AnimationLoader : Loader {

    public static AnimationLoader Instance { get; private set; } = null;


    private Dictionary<uint, AnimationClip> animDatabase = new Dictionary<uint, AnimationClip>();

    public void ResetDB()
    {
        animDatabase.Clear();
    }


    private static string[] ComponentPaths = {  "localRotation.x",
                                                "localRotation.y",
                                                "localRotation.z",
                                                "localRotation.w",
                                                "localPosition.x",
                                                "localPosition.y",
                                                "localPosition.z"  };
    //In case space conversions are needed
    private static float[] ComponentMultipliers = {  -1.0f,
                                                      1.0f,
                                                     1.0f,
                                                     -1.0f,
                                                      -1.0f,
                                                      1.0f,
                                                     1.0f  };


    private static Dictionary<uint, string> crcsToCommonNames;
 

    public static string TryFindAnimationName(uint nameCrc)
    {
        if (crcsToCommonNames.ContainsKey(nameCrc))
        {
            return crcsToCommonNames[nameCrc];
        }
        return String.Format("0x{0:x}", nameCrc);
    }


    private void WalkSkeletonAndCreateCurves(ref AnimationClip clip, AnimationBank animBank,
    										Transform bone, string curPath, uint animHash)
    {
    	uint boneHash = HashUtils.GetCRC(bone.name);
		
    	string relPath = curPath + bone.name.ToLower();


    	animBank.GetAnimationMetadata(animHash, out int frameCap, out int numBones);

    	for (int i = 0; i < 7; i++)
    	{
            float mult = ComponentMultipliers[i];

			if (animBank.GetCurve(animHash, boneHash, (uint) i,
	                    out ushort[] inds, out float[] values))
			{
				Keyframe[] frames = new Keyframe[values.Length];
				for (int j = 0; j < values.Length; j++)
				{
					int index = (int) inds[j];
					frames[j] = new Keyframe(index < frameCap ? index / 30.0f : frameCap / 30.0f, mult * values[j]);
				}
				var curve = new AnimationCurve(frames);
				clip.SetCurve(relPath, typeof(Transform), ComponentPaths[i], curve);
			}
		}

		for (int i = 0; i < bone.childCount; i++)
		{
			WalkSkeletonAndCreateCurves(ref clip, animBank, bone.GetChild(i), relPath + "/", animHash);
		}
    }

    
    public List<AnimationClip> LoadAnimationBank(string animBankName, Transform tran)
    {
        List<AnimationClip> clips = new List<AnimationClip>();

        var bank = container.Get<AnimationBank>(animBankName);

        if (bank == null)
        {
            return clips;
        }

        var animCRCs = bank.GetAnimationCRCs();
        foreach (uint animCRC in animCRCs)
        {
            var clip = LoadAnimationClip(animBankName, animCRC, tran);
            clip.name = TryFindAnimationName(animCRC);
            clips.Add(clip);
        }

        return clips;
    }    


    public AnimationClip LoadAnimationClip(string animBankName, string animationName, Transform objectTransform)
    {   
        //uint animBankCRC = HashUtils.GetCRC(animBankName);
        uint animCRC = HashUtils.GetCRC(animationName);
        return LoadAnimationClip(animBankName, animCRC, objectTransform);
    }


    public AnimationClip LoadAnimationClip(string animBankName, uint animationName, Transform objectTransform)
    {
    	uint animID = HashUtils.GetCRC(animBankName) * animationName;//HashUtils.GetCRC(animBankName + "/" + animationName);

    	if (animDatabase.ContainsKey(animID))
    	{
    		return animDatabase[animID];
    	}

    	var animBank = container.Get<AnimationBank>(animBankName);

    	if (animBank == null)
    	{
    		Debug.LogWarningFormat("AnimationBank {0} failed to load!", animBankName);
    		return null;
    	}

    	uint animCRC = animationName; //HashUtils.GetCRC(animationName);

    	if (objectTransform != null && animBank.GetAnimationMetadata(animCRC, out int numFrames, out int numBones))
    	{
    		var clip = new AnimationClip();

            if (SaveAssets)
            {
                string bankPath = SaveDirectory + "/" + animBankName;
                if (!AssetDatabase.IsValidFolder(bankPath))
                {
                    AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(SaveDirectory, animBankName));
                }

                AssetDatabase.CreateAsset(clip, bankPath + "/" + TryFindAnimationName(animationName) + ".anim"); 
            }


    		clip.legacy = true;

    		for (int i = 0; i < objectTransform.childCount; i++)
    		{
	    		WalkSkeletonAndCreateCurves(ref clip, animBank, objectTransform.GetChild(i), "", animCRC);
    		}

    		animDatabase[animID] = clip;

    		return clip;
    	}
    	else 
    	{
    		Debug.LogWarningFormat("AnimationBank {0} does contain the animation {1}!", animBankName, animationName);
    		return null;
    	}
    }



    static AnimationLoader()
    {
        Instance = new AnimationLoader();

        crcsToCommonNames = new Dictionary<uint, string>();

        string[] CommonNames = {
            "aalya_sabre_sprint_full", "aalya_sabre_stand_attack1a_end_full",
            "aalya_sabre_stand_attack1a_full", "aalya_sabre_stand_attack1b_end_full",
            "aalya_sabre_stand_attack1b_full", "aalya_sabre_stand_attack1c_full",
            "aalya_sabre_stand_attack_backwards", "aalya_sabre_stand_block_idle_full",
            "aalya_sabre_stand_block_left1_full", "aalya_sabre_stand_block_left2_full",
            "aalya_sabre_stand_block_right1_full", "aalya_sabre_stand_block_right2_full",
            "aalya_sabre_stand_dashattack_full", "aalya_sabre_stand_idle_emote_full",
            "aalya_sabre_stand_runforward_full", "acklay_sabre_fall_full",
            "acklay_sabre_landhard_full", "acklay_sabre_landsoft_full",
            "acklay_sabre_stand_attack1a_full", "acklay_sabre_stand_attack1b_full",
            "acklay_sabre_stand_attack1c_full", "acklay_sabre_stand_death_backward_full",
            "acklay_sabre_stand_hitback", "acklay_sabre_stand_hitfront",
            "acklay_sabre_stand_hitleft", "acklay_sabre_stand_hitright",
            "acklay_sabre_stand_idle_emote_full", "acklay_sabre_stand_runbackward_full",
            "acklay_sabre_stand_runforward_full", "acklay_sabre_stand_turnleft_full",
            "acklay_sabre_stand_turnright_full", "acklay_sabre_stand_walkbackward_full",
            "acklay_sabre_stand_walkforward_full", "acklay_sabre_standalert_idle_emote_full",
            "acklay_sabre_standalert_runbackward_full", "acklay_sabre_standalert_runforward_full",
            "acklay_sabre_standalert_walkbackward_full", "acklay_sabre_standalert_walkforward_full",
            "acklay_walk_backwards", "acklaylz_rifle_stand_death_backward",
            "acklaylz_rifle_stand_idle_emote", "acklaylz_rifle_stand_runforward",
            "activate", "all_snowspeeder_9pose",
            "attack1", "attack2",
            "attack3", "barcspeeder_9pose",
            "basepose", "bdroid_pistol_sprint_full",
            "bdroid_rifle_sprint_full", "bdroid_rifle_stand_idle_emote",
            "bdroid_rifle_stand_reload", "bdroid_rifle_stand_runforward",
            "bdroid_rifle_stand_shoot", "bdroid_rifle_stand_walkforward",
            "bdroid_rifle_standalert_idle_emote", "bdroid_rifle_standalert_runforward",
            "bdroid_rifle_standalert_walkforward", "bdroid_stap_ride",
            "bothanspy_rifle_stand_shoot_secondary2_full", "bothanspy_tool_stand_shoot_secondary2_full",
            "clonecommander_bazooka_stand_shoot_secondary2_full", "clonecommander_pistol_stand_shoot_secondary2_full",
            "dag1_prop_treemoss_sway", "death",
            "death01", "destroy",
            "dooku_sabre_jump_full", "dooku_sabre_jumpattack_fall_full",
            "dooku_sabre_jumpattack_land_full", "dooku_sabre_jumpattack_recover_full",
            "dooku_sabre_stand_attack1a_end_full", "dooku_sabre_stand_attack1a_full",
            "dooku_sabre_stand_attack1b_full", "dooku_sabre_stand_attack1c_full",
            "dooku_sabre_stand_attack_backwards", "dooku_sabre_stand_dashattack_upper",
            "dooku_sabre_stand_idle_emote_full", "droidekafp_rifle_idle",
            "droidekafp_rifle_reload", "droidekafp_rifle_shoot",
            "droidekafp_rifle_walk", "dropoff",
            "ewok_speederbike_9pose", "ewok_speederbike_sit",
            "expand", "fambaa_death",
            "fambaa_idle", "fett_pistol_crouch_shoot_secondary2_full",
            "fett_pistol_stand_shoot_secondary2_full", "fett_rifle_crouch_shoot_secondary2_full",
            "fett_rifle_stand_shoot_secondary2_full", "fire",
            "fold", "gam_sabre_diveforward",
            "gam_sabre_stand_attack1a", "gam_sabre_stand_death_backward",
            "gam_sabre_stand_death_forward", "gam_sabre_stand_death_left",
            "gam_sabre_stand_death_right", "gam_sabre_stand_getupback",
            "gam_sabre_stand_getupfront", "gam_sabre_stand_hitback",
            "gam_sabre_stand_hitfront", "gam_sabre_stand_hitleft",
            "gam_sabre_stand_hitright", "gam_sabre_stand_idle_emote",
            "gam_sabre_stand_landhard", "gam_sabre_stand_landsoft",
            "gam_sabre_stand_runbackward", "gam_sabre_stand_runforward",
            "gam_sabre_stand_walkforward", "gamlz_rifle_diveforward",
            "gamlz_rifle_stand_death_backward", "gamlz_rifle_stand_idle_emote",
            "gamlz_rifle_stand_runforward", "grab",
            "grab1", "grab2",
            "grevious_barc_9pose", "grevious_minigun_9pose",
            "grevious_sabre_crouch_idle_emote_full", "grevious_sabre_crouch_turnleft_full",
            "grevious_sabre_crouch_turnright_full", "grevious_sabre_crouch_walkbackward_full",
            "grevious_sabre_crouch_walkforward_full", "grevious_sabre_diveforward_full",
            "grevious_sabre_fall_full", "grevious_sabre_jump_back_full",
            "grevious_sabre_jump_full", "grevious_sabre_jumpattack_end_full",
            "grevious_sabre_jumpattack_fall_full", "grevious_sabre_jumpattack_land_full",
            "grevious_sabre_landhard_full", "grevious_sabre_landsoft_full",
            "grevious_sabre_sprint_full", "grevious_sabre_stand_attack1a_end_full",
            "grevious_sabre_stand_attack1a_full", "grevious_sabre_stand_attack1b_end_full",
            "grevious_sabre_stand_attack1b_full", "grevious_sabre_stand_attack1c_full",
            "grevious_sabre_stand_block_front1_full", "grevious_sabre_stand_block_front2_full",
            "grevious_sabre_stand_block_idle_full", "grevious_sabre_stand_block_left1_full",
            "grevious_sabre_stand_block_left2_full", "grevious_sabre_stand_block_right1_full",
            "grevious_sabre_stand_block_right2_full", "grevious_sabre_stand_dashattack_full",
            "grevious_sabre_stand_deadhero_full", "grevious_sabre_stand_getupback_full",
            "grevious_sabre_stand_getupfront_full", "grevious_sabre_stand_hitback",
            "grevious_sabre_stand_hitfront", "grevious_sabre_stand_hitleft",
            "grevious_sabre_stand_hitright", "grevious_sabre_stand_idle_emote_full",
            "grevious_sabre_stand_runbackward_full", "grevious_sabre_stand_runforward_full",
            "grevious_sabre_stand_turnleft_full", "grevious_sabre_stand_turnright_full",
            "grevious_sabre_stand_walkbackward", "grevious_sabre_stand_walkforward",
            "grevious_sabre_thrown_bouncebacksoft_full", "grevious_sabre_thrown_bouncefrontsoft_full",
            "grevious_sabre_thrown_flail_full", "grevious_sabre_thrown_flyingback_full",
            "grevious_sabre_thrown_flyingfront_full", "grevious_sabre_thrown_flyingleft_full",
            "grevious_sabre_thrown_flyingright_full", "grevious_sabre_thrown_landbacksoft_full",
            "grevious_sabre_thrown_landfrontsoft_full", "grevious_sabre_thrown_tumbleback_full",
            "grevious_sabre_thrown_tumblefront_full", "grevious_stap_ride",
            "greviouslz_sabre_stand_attack1a", "greviouslz_sabre_stand_attack1a_end",
            "greviouslz_sabre_stand_attack1b", "greviouslz_sabre_stand_attack1b_end",
            "greviouslz_sabre_stand_attack1c", "greviouslz_sabre_stand_idle_emote",
            "greviouslz_sabre_stand_runforward", "hansolo_pistol_stand_idle_emote",
            "hansolo_pistol_stand_shoot", "hansolo_pistol_standalert_idle_emote",
            "hansolo_pistol_standalert_runforward", "hansolo_pistol_standalert_walkforward",
            "human_barc_9pose", "human_bazooka_crouch_idle_emote",
            "human_bazooka_crouch_reload", "human_bazooka_crouch_shoot",
            "human_bazooka_crouch_walkbackward", "human_bazooka_crouch_walkforward",
            "human_bazooka_crouchalert_idle_emote", "human_bazooka_crouchalert_walkbackward",
            "human_bazooka_crouchalert_walkforward", "human_bazooka_fall",
            "human_bazooka_jump", "human_bazooka_landhard",
            "human_bazooka_landsoft", "human_bazooka_sprint",
            "human_bazooka_stand_diveroll_forward", "human_bazooka_stand_idle_checkweapon_full",
            "human_bazooka_stand_idle_emote", "human_bazooka_stand_idle_lookaround",
            "human_bazooka_stand_reload_full", "human_bazooka_stand_runbackward",
            "human_bazooka_stand_runforward", "human_bazooka_stand_shoot_full",
            "human_bazooka_stand_shoot_secondary", "human_bazooka_stand_shoot_secondary2",
            "human_bazooka_stand_walkforward", "human_bazooka_standalert_idle_emote",
            "human_bazooka_standalert_runbackward", "human_bazooka_standalert_runforward",
            "human_bazooka_standalert_walkforward", "human_choking",
            "human_dishcannon_9pose", "human_drive",
            "human_drive_1manatst", "human_drivesnowspeeder",
            "human_drivesnowspeedergunner", "human_gallop",
            "human_lascannon_stand_runforward", "human_man_gun",
            "human_man_minigun", "human_melee_stand_attack1a",
            "human_melee_stand_attack1b", "human_melee_stand_block_front1",
            "human_melee_stand_block_front2", "human_melee_stand_block_left1",
            "human_melee_stand_block_left2", "human_melee_stand_block_rear1",
            "human_melee_stand_block_rear2", "human_melee_stand_block_right1",
            "human_melee_stand_block_right2", "human_minigun_9pose",
            "human_observeinstruments", "human_pistol_crouch_reload",
            "human_pistol_crouch_shoot", "human_pistol_crouch_walkforward",
            "human_pistol_crouchalert_idle_emote", "human_pistol_crouchalert_walkbackward",
            "human_pistol_crouchalert_walkforward", "human_pistol_jetpack_hover",
            "human_pistol_sprint", "human_pistol_stand_reload",
            "human_pistol_stand_runforward", "human_pistol_stand_shoot",
            "human_pistol_stand_walkforward", "human_pistol_standalert_idle_emote",
            "human_pistol_standalert_runbackward", "human_pistol_standalert_runforward_full",
            "human_pistol_standalert_walkforward_full", "human_ride",
            "human_ride_shoot", "human_rifle_crouch_hitfront",
            "human_rifle_crouch_hitleft", "human_rifle_crouch_hitright",
            "human_rifle_crouch_idle_emote_full", "human_rifle_crouch_idle_takeknee_lower",
            "human_rifle_crouch_reload_full", "human_rifle_crouch_shoot_full",
            "human_rifle_crouch_turnleft", "human_rifle_crouch_turnright",
            "human_rifle_crouch_walkbackward", "human_rifle_crouch_walkforward",
            "human_rifle_crouchalert_idle_emote_full", "human_rifle_crouchalert_walkbackward",
            "human_rifle_crouchalert_walkforward", "human_rifle_die",
            "human_rifle_dive2prone", "human_rifle_diveforward",
            "human_rifle_fall", "human_rifle_jetpack_hover",
            "human_rifle_jump", "human_rifle_landhard",
            "human_rifle_landsoft", "human_rifle_prone_hitfront",
            "human_rifle_prone_hitleft", "human_rifle_prone_hitright",
            "human_rifle_prone_toss_lefthand", "human_rifle_sprint_full",
            "human_rifle_stand_deadhero_full", "human_rifle_stand_death_backward",
            "human_rifle_stand_death_forward", "human_rifle_stand_death_left",
            "human_rifle_stand_death_right", "human_rifle_stand_getupback",
            "human_rifle_stand_getupfront", "human_rifle_stand_hitback",
            "human_rifle_stand_hitfront", "human_rifle_stand_hitleft",
            "human_rifle_stand_hitright", "human_rifle_stand_idle_checkweapon_full",
            "human_rifle_stand_idle_emote_full", "human_rifle_stand_idle_lookaround_full",
            "human_rifle_stand_reload_full", "human_rifle_stand_runbackward",
            "human_rifle_stand_runforward", "human_rifle_stand_shoot_full",
            "human_rifle_stand_shoot_secondary2_full", "human_rifle_stand_shoot_secondary_full",
            "human_rifle_stand_turnleft", "human_rifle_stand_turnright",
            "human_rifle_stand_walkforward", "human_rifle_standalert_idle_emote_full",
            "human_rifle_standalert_runbackward", "human_rifle_standalert_runforward",
            "human_rifle_standalert_walkforward", "human_rifle_thrown_bouncebackhard",
            "human_rifle_thrown_bouncebackmedium", "human_rifle_thrown_bouncebacksoft",
            "human_rifle_thrown_bouncefronthard", "human_rifle_thrown_bouncefrontmedium",
            "human_rifle_thrown_bouncefrontsoft", "human_rifle_thrown_flail",
            "human_rifle_thrown_flyingback", "human_rifle_thrown_flyingfront",
            "human_rifle_thrown_flyingleft", "human_rifle_thrown_flyingright",
            "human_rifle_thrown_landbackhard", "human_rifle_thrown_landbackmedium",
            "human_rifle_thrown_landbacksoft", "human_rifle_thrown_landfronthard",
            "human_rifle_thrown_landfrontmedium", "human_rifle_thrown_landfrontsoft",
            "human_rifle_thrown_restbacksoft", "human_rifle_thrown_restfrontsoft",
            "human_rifle_thrown_tumbleback", "human_rifle_thrown_tumblefront",
            "human_sabre_crouch_idle_emote", "human_sabre_crouch_walkbackward",
            "human_sabre_crouch_walkforward", "human_sabre_fall_full",
            "human_sabre_jetpack_hover_full", "human_sabre_jump_backward_full",
            "human_sabre_jump_full", "human_sabre_jump_left_full",
            "human_sabre_jump_right_full", "human_sabre_jumpattack_end_full",
            "human_sabre_jumpattack_fall_full", "human_sabre_jumpattack_land_full",
            "human_sabre_landhard_full", "human_sabre_landsoft_full",
            "human_sabre_sprint_full", "human_sabre_stand_attack1a_end_full",
            "human_sabre_stand_attack1a_full", "human_sabre_stand_attack1b_end_full",
            "human_sabre_stand_attack1b_full", "human_sabre_stand_attack1c_full",
            "human_sabre_stand_attack_backwards", "human_sabre_stand_block_front1_full",
            "human_sabre_stand_block_front2_full", "human_sabre_stand_block_idle_full",
            "human_sabre_stand_block_left1_full", "human_sabre_stand_block_left2_full",
            "human_sabre_stand_block_right1_full", "human_sabre_stand_block_right2_full",
            "human_sabre_stand_catch_full", "human_sabre_stand_dashattack_full",
            "human_sabre_stand_dashattack_lower", "human_sabre_stand_getupback_full",
            "human_sabre_stand_getupfront_full", "human_sabre_stand_idle_emote_full",
            "human_sabre_stand_runbackward_full", "human_sabre_stand_runforward_full",
            "human_sabre_stand_throw_full", "human_sabre_stand_useforce_full",
            "human_sabre_stand_walkbackward_full", "human_sabre_stand_walkforward_full",
            "human_speederbike_9pose", "human_speederbike_sit",
            "human_stand_rifle_toss_lefthanded", "human_standing",
            "human_stap_ride", "human_tool_crouch_idle_emote",
            "human_tool_crouch_shoot", "human_tool_crouch_walkbackward",
            "human_tool_crouch_walkforward", "human_tool_diveforward",
            "human_tool_fall", "human_tool_jump",
            "human_tool_jumpfall", "human_tool_landhard",
            "human_tool_landsoft", "human_tool_sprint",
            "human_tool_stand_idle_checkweapon_full", "human_tool_stand_idle_emote",
            "human_tool_stand_idle_lookaround", "human_tool_stand_runbackward",
            "human_tool_stand_runforward", "human_tool_stand_shoot",
            "human_tool_stand_shoot_secondary", "human_tool_stand_shoot_secondary2",
            "human_tool_stand_walkforward", "humanfp_bazooka_flail",
            "humanfp_bazooka_idle", "humanfp_bazooka_jump",
            "humanfp_bazooka_jump_land", "humanfp_bazooka_reload",
            "humanfp_bazooka_run", "humanfp_bazooka_shoot",
            "humanfp_grenade_charge", "humanfp_grenade_shoot",
            "humanfp_grenade_shoot2", "humanfp_handsdown",
            "humanfp_lascannon_flail", "humanfp_lascannon_idle",
            "humanfp_lascannon_jump", "humanfp_lascannon_run",
            "humanfp_lascannon_shoot", "humanfp_rifle_flail",
            "humanfp_rifle_idle", "humanfp_rifle_jump",
            "humanfp_rifle_jump_land", "humanfp_rifle_reload",
            "humanfp_rifle_run", "humanfp_rifle_shoot",
            "humanfp_tool_flail", "humanfp_tool_idle",
            "humanfp_tool_jump", "humanfp_tool_jump_land",
            "humanfp_tool_reload", "humanfp_tool_repair",
            "humanfp_tool_run", "humanfp_tool_shoot",
            "humanlz_barc_9pose", "humanlz_drive",
            "humanlz_drive_1manatst", "humanlz_ride",
            "humanlz_rifle_crouch_idle_emote", "humanlz_rifle_crouch_idle_takeknee",
            "humanlz_rifle_diveforward", "humanlz_rifle_jetpack_hover",
            "humanlz_rifle_prone_idle_emote", "humanlz_rifle_stand_death01",
            "humanlz_rifle_stand_death_backward", "humanlz_rifle_stand_deathforward",
            "humanlz_rifle_stand_deathleft", "humanlz_rifle_stand_deathright",
            "humanlz_rifle_stand_idle_emote", "humanlz_rifle_stand_runforward",
            "humanlz_rifle_stand_shoot", "humanlz_rifle_thrown_flail",
            "humanlz_speederbike_9pose", "humanlz_stap_ride",
            "idle", "idle_leftup",
            "idle_rightup", "idle_to_leftfoot",
            "idle_to_leftfoot_leftup", "idle_to_leftfoot_rightup",
            "idle_to_rightfoot", "imp_speederbike_9pose",
            "impofficer_pistol_stand_shoot_secondary2_full", "impofficer_rifle_stand_shoot_secondary2_full",
            "kiadimundi_sabre_jumpattack_end_full", "kiadimundi_sabre_jumpattack_fall_full",
            "kiadimundi_sabre_jumpattack_land_full", "kiadimundi_sabre_stand_dashattack_end_full",
            "kiadimundi_sabre_stand_dashflip2_full", "kiadimundi_sabre_stand_dashflip_full",
            "leftfoot_to_idle", "leftfoot_to_idle_leftup",
            "leftfoot_to_idle_rightup", "leftfoot_up",
            "mace_sabre_jumpattack_fall_full", "mace_sabre_jumpattack_land_full",
            "mace_sabre_landhard_full", "mace_sabre_landsoft_full",
            "mace_sabre_stand_attack1a_end_full", "mace_sabre_stand_attack1a_full",
            "mace_sabre_stand_attack1b_end_full", "mace_sabre_stand_attack1b_full",
            "mace_sabre_stand_attack1c_full", "mace_sabre_stand_dashattack_full",
            "mace_sabre_stand_idle_emote_full", "mace_sabre_stand_runforward_full",
            "mace_sabre_stand_walkforward_full", "magnaguard_pistol_stand_shoot_secondary2_full",
            "magnaguard_rifle_stand_shoot_secondary2_full", "maul_sabre_jump_backward_full",
            "maul_sabre_jump_left_full", "maul_sabre_jump_right_full",
            "maul_sabre_jumpattack_end_full", "maul_sabre_jumpattack_fall_full",
            "maul_sabre_jumpattack_land_full", "maul_sabre_sprint_upper",
            "maul_sabre_stand_attack1a_end_full", "maul_sabre_stand_attack1a_full",
            "maul_sabre_stand_attack1b_end_full", "maul_sabre_stand_attack1b_full",
            "maul_sabre_stand_attack1c_full", "maul_sabre_stand_attack2c_full",
            "maul_sabre_stand_attack_backwards", "maul_sabre_stand_block_front1_full",
            "maul_sabre_stand_block_front2_full", "maul_sabre_stand_block_idle_full",
            "maul_sabre_stand_block_left1_full", "maul_sabre_stand_block_left2_full",
            "maul_sabre_stand_block_right1_full", "maul_sabre_stand_block_right2_full",
            "maul_sabre_stand_dashattack_full", "maul_sabre_stand_idle_emote_full",
            "maul_sabre_stand_walkbackwards_full", "obiwan_sabre_jumpattack_end_full",
            "obiwan_sabre_jumpattack_fall_full", "obiwan_sabre_jumpattack_land_full",
            "obiwan_sabre_stand_attack1a_end_full", "obiwan_sabre_stand_attack1a_full",
            "obiwan_sabre_stand_attack1b_end_full", "obiwan_sabre_stand_attack1b_full",
            "obiwan_sabre_stand_attack1c_full", "obiwan_sabre_stand_dashattack_full",
            "open", "rep_fightertank_9pose",
            "reset", "rightfoot_to_idle",
            "rightfoot_to_idle_leftup", "rightfoot_to_idle_rightup",
            "rightfoot_up", "roll",
            "roll_pose", "run",
            "runforward", "sbdroid_rifle_crouch_idle_emote",
            "sbdroid_rifle_crouch_reload", "sbdroid_rifle_crouch_shoot",
            "sbdroid_rifle_crouch_shoot_secondary_full", "sbdroid_rifle_crouch_walkbackward",
            "sbdroid_rifle_crouch_walkforward", "sbdroid_rifle_crouchalert_idle_emote",
            "sbdroid_rifle_crouchalert_walkbackward", "sbdroid_rifle_crouchalert_walkforward",
            "sbdroid_rifle_diveforward", "sbdroid_rifle_jump",
            "sbdroid_rifle_jumpfall", "sbdroid_rifle_landhard",
            "sbdroid_rifle_landsoft", "sbdroid_rifle_sprint_full",
            "sbdroid_rifle_stand_idle_emote", "sbdroid_rifle_stand_reload",
            "sbdroid_rifle_stand_runbackward", "sbdroid_rifle_stand_runforward",
            "sbdroid_rifle_stand_shoot", "sbdroid_rifle_stand_shoot_secondary_full",
            "sbdroid_rifle_stand_walkforward", "sbdroid_rifle_standalert_idle_emote",
            "sbdroid_rifle_standalert_runbackward", "sbdroid_rifle_standalert_runforward",
            "sbdroid_rifle_standalert_walkforward", "sbdroid_stap_ride",
            "sbdroidfp_rifle_idle", "sbdroidfp_rifle_run",
            "sbdroidfp_rifle_shoot", "sidious_sabre_jump_fall_full",
            "sidious_sabre_jump_full", "sidious_sabre_jumpattack_fall_full",
            "sidious_sabre_jumpattack_land_full", "sidious_sabre_landhard_full",
            "sidious_sabre_landsoft_full", "sidious_sabre_sprint_full",
            "sidious_sabre_stand_attack_backwards", "sidious_sabre_stand_dashattack_full",
            "sidious_sabre_stand_gam_guard_full", "sidious_sabre_stand_idle_emote_full",
            "sidious_sabre_stand_obiwan1b_end_full", "sidious_sabre_stand_obiwan1b_full",
            "sidious_sabre_stand_runbackward_full", "sidious_sabre_stand_runforward_full",
            "sidious_sabre_stand_turnleft_full", "sidious_sabre_stand_turnright_full",
            "sidious_sabre_stand_useforce_full", "sidious_sabre_stand_vaderdash",
            "sidious_sabre_stand_walkbackward_full", "sidious_sabre_stand_walkforward_full",
            "sidiouslz_sabre_stand_idle_emote", "sidiouslz_sabre_stand_runforward",
            "sidiouslz_sabre_stand_useforce", "takeoff",
            "tat_skiff_9pose", "trigger",
            "turnl", "turnl_leftup",
            "turnl_rightup", "turnr",
            "turnr_leftup", "turnr_rightup",
            "unfold", "vader_sabre_jumpattack_fall_full",
            "vader_sabre_jumpattack_land_full", "vader_sabre_jumpattack_recover_full",
            "vader_sabre_sprint_full", "vader_sabre_stand_attack1a_full",
            "vader_sabre_stand_attack1b_full", "vader_sabre_stand_attack1c_full",
            "vader_sabre_stand_attack_backwards", "vader_sabre_stand_dashattack_full",
            "vader_sabre_stand_dashattackend_full", "vader_sabre_stand_idle_emote_full",
            "vader_sabre_stand_runbackward_full", "vader_sabre_stand_runforward_full",
            "vader_sabre_stand_useforce_full", "vader_sabre_stand_walkforward_full",
            "walk_leftfoot_rightfoot", "walk_leftfoot_rightfoot_leftup",
            "walk_leftfoot_rightfoot_rightup", "walk_rightfoot_leftfoot",
            "walk_rightfoot_leftfoot_leftup", "walk_rightfoot_leftfoot_rightup",
            "walkloop", "wampa_sabre_fall_full",
            "wampa_sabre_jump_full", "wampa_sabre_jumpattack_end_full",
            "wampa_sabre_jumpattack_fall_full", "wampa_sabre_jumpattack_land_full",
            "wampa_sabre_landhard_full", "wampa_sabre_landsoft_full",
            "wampa_sabre_sprint_full", "wampa_sabre_stand_alternate_attack_full",
            "wampa_sabre_stand_attack1a_end_full", "wampa_sabre_stand_attack1a_full",
            "wampa_sabre_stand_attack1b_end_full", "wampa_sabre_stand_attack1b_full",
            "wampa_sabre_stand_attack1c_full", "wampa_sabre_stand_dashattack_full",
            "wampa_sabre_stand_idle_emote_full", "wampa_sabre_stand_runbackward_full",
            "wampa_sabre_stand_runforward_full", "wampa_sabre_stand_walkbackward_full",
            "wampa_sabre_stand_walkforward_full", "wampa_sabre_standalert_idle_emote_full",
            "yoda_barc_9pose", "yoda_minigun_9pose",
            "yoda_sabre_jump_backward_full", "yoda_sabre_jump_fall_full",
            "yoda_sabre_jump_full", "yoda_sabre_jump_left_full",
            "yoda_sabre_jump_right_full", "yoda_sabre_jumpattack_end_full",
            "yoda_sabre_jumpattack_fall_full", "yoda_sabre_jumpattack_land_full",
            "yoda_sabre_stand_attack1a_end_full", "yoda_sabre_stand_attack1a_full",
            "yoda_sabre_stand_attack1b_end_full", "yoda_sabre_stand_attack1b_full",
            "yoda_sabre_stand_attack1c_full", "yoda_sabre_stand_block_front1_full",
            "yoda_sabre_stand_block_front2_full", "yoda_sabre_stand_block_idle_full",
            "yoda_sabre_stand_block_left1_full", "yoda_sabre_stand_block_left2_full",
            "yoda_sabre_stand_block_right1_full", "yoda_sabre_stand_block_right2_full",
            "yoda_sabre_stand_dashattack_full", "yoda_sabre_stand_idle_emote_full",
            "yoda_sabre_stand_runbackward_full", "yoda_sabre_stand_runforward_full",
            "yoda_sabre_stand_walkforward_full", "yoda_speederbike_9pose",
            "yoda_stand_sprint_full", "yoda_stap_ride",
        }; 

        foreach (string name in CommonNames)
        {
            crcsToCommonNames[HashUtils.GetCRC(name)] = name;
        }
    }
}