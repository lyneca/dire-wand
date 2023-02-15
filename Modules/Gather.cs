using System.Collections;
using System.Collections.Generic;
using ExtensionMethods;
using GestureEngine;
using ThunderRoad;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Wand; 

public class Gather : WandModule {
    public override void OnInit() {
        base.OnInit();
        wand.trigger.Then(Gesture.Both
                // .Palm(Direction.Together)
                .Moving(Direction.Together))
            .Do(Collect);
    }

    private void Collect() {
        MarkCasted();
        wand.StartCoroutine(CollectRoutine());
    }

    private IEnumerator CollectRoutine() {
        var collectPoint = wand.tipRay.GetPoint(5);

        int index = 0;
        List<Entity> entities = new();
        foreach (var item in Utils.AllItemsInRadius(collectPoint, 5)) {
            if (index > 7) break;
            if (item.Free()) {
                entities.Add(item.gameObject.GetOrAddComponent<Entity>());
            }

            index++;
        }

        Dictionary<Entity, (Vector3 offset, Vector3 axis)> offsets = new();

        for (var i = 0; i < entities.Count; i++) {
            var entity = entities[i];
            entity.Grab();

            entity.Rigidbody().AddForce(
                (Vector3.up + (wand.tip.position - entity.WorldCenter).normalized * 0.3f)
                * ((item?.GetMassModifier().Randomize(0.6f) ?? 1f) * 6f), ForceMode.Impulse);
            entity.Rigidbody().AddTorque(
                Utils.RandomVector() * ((item?.GetMassModifier().Randomize(0.6f) ?? 1f) * 3f),
                ForceMode.Impulse);

            offsets[entity] = (Random.insideUnitSphere.ClampMagnitude(0.2f, 0.8f) * 1.5f, Random.onUnitSphere);
        }

        while (wand.item.mainHandler?.playerHand?.controlHand.usePressed == true) {
            var target = wand.tip.transform.position
                         + wand.tip.transform.forward * 2
                         + wand.tipVelocity.normalized
                         * Mathf.Clamp(wand.tipVelocity.magnitude, 0.0f, 8f)
                         / 5f;

            for (var i = 0; i < entities.Count; i++) {
                var entity = entities[i];
                entity.UpdatePull(target
                                  + wand.tip.rotation
                                  * Quaternion.AngleAxis(Time.time * 60, offsets[entity].axis)
                                  * offsets[entity].offset);
            }

            yield return 0;
        }

        for (var i = 0; i < entities.Count; i++) {
            entities[i].Release();
        }

        yield return 0;
    }
}
