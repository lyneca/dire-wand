using System.Collections;
using ExtensionMethods;
using GestureEngine;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Expelliarmus : WandModule {
    public override void OnInit() {
        base.OnInit();

        wand.targetedEnemy
            .Then(wand.Offhand.Gripping.Moving(Direction.Backward, wand.module.gestureVelocitySmall).Palm(Direction.Up))
            .Do(DisarmEntity, "Disarm Entity");
        wand.targetedItem
            .Then(wand.Offhand.Gripping.Moving(Direction.Backward, wand.module.gestureVelocitySmall).Palm(Direction.Up))
            .Do(DisarmEntity, "Pull Item");
    }

    public void DisarmEntity() {
        MarkCasted();
        var heldItem = wand.target?.creature is Creature creature
            ? creature.handRight?.grabbedHandle?.item ?? wand.target.creature.handLeft?.grabbedHandle?.item
            : wand.target?.handler?.item;
        if (heldItem == null) {
            wand.Reset();
            return;
        }

        Ragdoll holder = heldItem.mainHandler?.ragdoll;
        if (holder != null) {
            heldItem.mainHandler?.UnGrab(true);
            heldItem.IgnoreRagdollCollision(holder);
        }

        wand.StartCoroutine(ArcToHand(heldItem, item.mainHandler.otherHand));
    }

    public IEnumerator ArcToHand(Item item, RagdollHand hand) {
        if (item == null) yield break;
        wand.target = null;
        item.physicBody.AddForce(Vector3.up * 10, ForceMode.VelocityChange);

        float startTime = Time.time;
        bool wasGripping = hand.IsGripping();
        while (Time.time - startTime < 2f) {
            if (item.gameObject == null
                || item.mainHandler != null
                || item.isTelekinesisGrabbed) break;
            var itemToHand = hand.transform.position - item.transform.position;
            item.physicBody.AddForce(itemToHand * wand.module.disarmHandForceMultiplier,
                ForceMode.Acceleration);
            item.physicBody.velocity = Vector3.Lerp(item.physicBody.velocity,
                itemToHand.normalized * 5,
                (Time.time - startTime) / 2 * Time.deltaTime * 10);
            if (!wasGripping
                && hand.Gripping()) {
                wasGripping = true;
                if (Vector3.Distance(item.transform.position, hand.transform.position) < 0.5f
                    && item.GetMainHandle(hand.side) != null) {
                    item.ResetRagdollCollision();
                    hand.Grab(item.GetMainHandle(hand.side), true);
                    yield break;
                }
            } else if (!hand.Gripping()) {
                wasGripping = false;
            }

            yield return 0;
        }

        item.ResetRagdollCollision();
    }


}