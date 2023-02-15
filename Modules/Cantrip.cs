using System.Linq;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Cantrip : WandModule {
    public override void OnInit() {
        wand.targetedItem
            .Then(() => wand.Buttoning, "Tap button")
            .Repeatable()
            .Do(Boop, "Boop")
            .Then(() => !wand.Buttoning, "Release button")
            .Do(UnBoop, "Un-boop");
    }

    public void Boop() {
        Boop(wand.target.item, wand);
        MarkCasted();
    }

    public void UnBoop() {
        UnBoop(wand.target.item, wand);
    }
    
    public static void Boop(Item targetItem, WandBehaviour wand) {
        if (targetItem == null) return;
        wand.item.Haptic(0.8f);
        if (targetItem.GetComponent<HingeLinker>()?.linkedDrive is HingeDrive drive) {
            switch (drive.currentState) {
                case HingeDrive.HingeDriveState.Unlocked:
                    drive.AutoClose(drive.autoOpenFallBackVelocity * 3, drive.autoOpenFallBackForce * 3);
                    break;
                case HingeDrive.HingeDriveState.LatchLocked:
                    drive.AutoOpenMax(drive.autoOpenFallBackVelocity * 3, drive.autoOpenFallBackForce * 3, bypassLatch: true);
                    break;
            }
        }

        if (targetItem.data.GetModule<ItemModuleEdible>() is ItemModuleEdible module) {
            module.OnMouthTouch(targetItem, Player.currentCreature.mouthRelay);
        }

        if (!string.IsNullOrEmpty(targetItem.colliderGroups.FirstOrDefault()?.imbueCustomSpellID)) {
            var imbue = targetItem.colliderGroups.FirstOrDefault()?.imbue;
            imbue?.Transfer(Catalog.GetData<SpellCastCharge>(targetItem.colliderGroups.FirstOrDefault()?.imbueCustomSpellID), imbue.maxEnergy * 2);
        }

        if (targetItem.gameObject.GetComponentInChildren<LiquidContainer>() is LiquidContainer container) {
            if (container.GetField<Holder>("corkHolder")?.UnSnapOne() is Item cork) {
                cork.IgnoreObjectCollision(targetItem);
                cork.RunAfter(() => cork.ResetObjectCollision());
                cork.physicBody.AddForce((cork.transform.position - targetItem.transform.position).normalized * 2f, ForceMode.VelocityChange);
            }
        }
        
        if (!targetItem.handlers.Contains(wand.holdingHand))
            targetItem.handlers.Add(wand.holdingHand);
        targetItem.OnHeldAction(wand.item.mainHandler, targetItem.GetMainHandle(wand.item.mainHandler.side), Interactable.Action.AlternateUseStart);
        
        var line = Catalog.GetData<EffectData>("WandLine").Spawn(targetItem.transform);
        line.SetSource(wand.tip);
        line.SetTarget(targetItem.transform);
        line.SetMainGradient(wand.module.targetArgs.gradient);
        line.Play();
    }

    public override void OnReset() {
        base.OnReset();
        if (wand.target?.item)
            wand.target?.item?.handlers.RemoveAll(handler => handler.grabbedHandle.item != wand.target.item);
    }

    public static void UnBoop(Item targetItem, WandBehaviour wand) {
        if (targetItem == null) return;
        wand.item.Haptic(0.8f);
        targetItem.OnHeldAction(wand.item.mainHandler, targetItem.GetMainHandle(wand.item.mainHandler.side), Interactable.Action.AlternateUseStop);
    }
}
