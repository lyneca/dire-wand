using System.Collections;
using ExtensionMethods;
using ThunderRoad;
using ThunderRoad.Skill.Spell;
using UnityEngine;

namespace Wand; 

public class Lightning : WandModule {
    public override void OnInit() {
        base.OnInit();
        wand.button.Then(() => wand.localTipVelocity.MostlyZ()
                               && wand.localTipVelocity.z > wand.module.gestureVelocityNormal, "Stab Forwards")
            .Do(LightningBolt, "Lightning Bolt");
    }

    public void LightningBolt() {
        MarkCasted();
        wand.StartCoroutine(LightningBoltRoutine());
    }

    public IEnumerator LightningBoltRoutine() {
        var spell = Catalog.GetData<SpellCastLightning>("Lightning");
        var arcStaffEffectData = Catalog.GetData<EffectData>("SpellLightningArcLoop");
        var skill = Catalog.GetData<SkillArcwire>("Arcwire");
        var prevNode = LightningTrailNode.New(wand.tip.position, skill, null, Player.currentCreature);
        var nextNode
            = LightningTrailNode.New(wand.tip.position, skill, null, Player.currentCreature, wand.tip, prevNode);
        var arcStaffEffect = arcStaffEffectData.Spawn(wand.tip);
        float arcWhooshIntensity = 0;
        arcStaffEffect.Play();
        Vector3 force = wand.tip.forward
                        * (Mathf.Clamp(item.physicBody.GetPointVelocity(wand.tip.position).magnitude, 0.0f, 8) * 2);
        item.Haptic(0.5f);
        prevNode.rb.AddForce(force, ForceMode.VelocityChange);

        while (item.mainHandler?.playerHand?.controlHand.alternateUsePressed == true && nextNode != null) {
            if (prevNode == null || (nextNode.transform.position - prevNode.transform.position).magnitude
                > 0.3f) {
                nextNode.transform.SetParent(null);
                prevNode = nextNode;
                nextNode = LightningTrailNode.New(wand.tip.position, skill, null, Player.currentCreature, wand.tip, prevNode);
                force = wand.tip.forward
                        * (Mathf.Clamp(item.physicBody.GetPointVelocity(wand.tip.position).magnitude, 0, 8) * 2);
                prevNode.rb.AddForce(force, ForceMode.VelocityChange);
            }

            if (!item.physicBody.IsSleeping()) {
                Vector3 pointVelocity = item.physicBody.GetPointVelocity(wand.tip.position);
                arcWhooshIntensity = Mathf.Lerp(arcWhooshIntensity,
                    Mathf.InverseLerp(5, 12, pointVelocity.magnitude), 0.1f);
                arcStaffEffect.SetSpeed(arcWhooshIntensity);
                if (item) arcStaffEffect.source = item;
                if (arcWhooshIntensity > 0 && !arcStaffEffect.isPlaying) {
                    arcStaffEffect.Play();
                }
            } else if (arcWhooshIntensity > 0) {
                arcWhooshIntensity = Mathf.Lerp(arcWhooshIntensity, 0, 0.1f);
            }

            yield return 0;
        }

        arcStaffEffect.End();
        if (!nextNode) yield break;
        nextNode.transform.SetParent(null);
        nextNode.EndTrail();
        item.Haptic(0.5f);
        nextNode.rb.AddForce(
            wand.tip.forward
            * (Mathf.Clamp(item.physicBody.GetPointVelocity(wand.tip.position).magnitude, 0.0f, 8) * 2),
            ForceMode.VelocityChange);
    }

}