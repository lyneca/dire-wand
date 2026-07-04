using System.Linq;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Cantrip : WandSkill {
    public override void OnInit() {
        wand.targetedItem
            .Then(() => wand.Buttoning, "Tap button")
            .Repeatable()
            .Do(Boop, "Boop")
            .Then(() => !wand.Buttoning, "Release button")
            .Do(UnBoop, "Un-boop");
    }

    public void Boop()
    {
        if (wand.target is not Item item) return;
        Boop(item, wand);
        MarkCasted();
    }

    public void UnBoop() {
        if (wand.target is Item item)
            UnBoop(item, wand);
    }
    
    public static void Boop(Item targetItem, WandBehaviour wand) {
        if (targetItem == null) return;
        
        Catalog.GetData<EffectData>("WandCantrip").Spawn(targetItem.transform).Play();
        wand.item.Haptic(0.8f);

        bool shouldGrab = true;
        
        if (targetItem.GetComponent<HingeLinker>()?.linkedDrive is HingeDrive drive) {
            shouldGrab = false;
            switch (drive.currentState) {
                case HingeDrive.HingeDriveState.Unlocked:
                    drive.AutoClose(drive.autoOpenFallBackVelocity * 3, drive.autoOpenFallBackForce * 3);
                    break;
                case HingeDrive.HingeDriveState.LatchLocked:
                    drive.AutoOpenMax(drive.autoOpenFallBackVelocity * 3, drive.autoOpenFallBackForce * 3, bypassLatch: true);
                    break;
            }
        }

        if (targetItem.GetComponent<Breakable>() is Breakable breakable) {
            breakable.Break();
        }

        if (targetItem.data.GetModule<ItemModuleEdible>() is ItemModuleEdible module) {
            shouldGrab = false;
            module.OnMouthTouch(targetItem, Player.currentCreature.mouthRelay);
        }

        if (!string.IsNullOrEmpty(targetItem.colliderGroups.FirstOrDefault()?.imbueCustomSpellID)) {
            shouldGrab = false;
            var imbue = targetItem.colliderGroups.FirstOrDefault()?.imbue;
            imbue?.Transfer(Catalog.GetData<SpellCastCharge>(targetItem.colliderGroups.FirstOrDefault()?.imbueCustomSpellID), imbue.maxEnergy * 2);
        }

        if (targetItem.gameObject.GetComponentInChildren<LiquidContainer>() is LiquidContainer container) {
            shouldGrab = false;
            if (container.GetField<Holder>("corkHolder")?.UnSnapOne() is Item cork) {
                cork.IgnoreObjectCollision(targetItem);
                cork.RunAfter(() => cork.ResetObjectCollision(), 0);
                cork.physicBody.AddForce((cork.transform.position - targetItem.transform.position).normalized * 2f, ForceMode.VelocityChange);
            }
        }

        if (shouldGrab && !targetItem.handlers.Contains(wand.holdingHand)) targetItem.handlers.Add(wand.holdingHand);
        targetItem.OnHeldAction(wand.item.mainHandler, targetItem.GetMainHandle(wand.item.mainHandler.side), Interactable.Action.AlternateUseStart);
        
        var line = Catalog.GetData<EffectData>("WandLine").Spawn(targetItem.transform);
        line.SetSource(wand.tip);
        line.SetTarget(targetItem.transform);
        line.SetMainGradient(wand.module.targetArgs.gradient);
        line.Play();
    }

    public override void OnReset() {
        base.OnReset();
        if (wand.target is Item item)
            item.handlers.RemoveAll(handler => handler.grabbedHandle.item != wand.target);
    }

    public static void UnBoop(Item targetItem, WandBehaviour wand) {
        if (targetItem == null) return;
        wand.item.Haptic(0.8f);
        targetItem.OnHeldAction(wand.item.mainHandler, targetItem.GetMainHandle(wand.item.mainHandler.side), Interactable.Action.AlternateUseStop);
    }
}
