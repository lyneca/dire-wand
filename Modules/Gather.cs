using System.Collections;
using System.Collections.Generic;
using ExtensionMethods;
using GestureEngine;
using ThunderRoad;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Wand; 

public class Gather : WandSkill {
    public override void Register() {
        base.Register();
        wand.trigger
            .Then(Gesture.Both.Moving(Direction.Together))
            .Do(Collect);
    }

    private void Collect() {
        MarkCasted();
        wand.StartCoroutine(CollectRoutine());
    }

    private IEnumerator CollectRoutine() {
        var collectPoint = wand.tip.position + Vector3.Slerp(wand.tipRay.direction, Player.local.head.transform.forward, 0.5f).normalized * 5;

        Dictionary<Item, PIDController> entities = new();
        var items = ThunderEntity.InRadiusNaive(collectPoint, 5, Filter.FreeItems);
        for (int i = 0; i < 7; i++)
        {
            if (!items.RandomFilteredSelectInPlace(each => each is Item eachItem && !entities.ContainsKey(eachItem),
                    out var entity) || entity is not Item item) break;
            entities[item] = new PIDController(item.physicBody.rigidBody, forceMode: ForceMode.Acceleration, maxForce: 5000)
                .Position(50, 0, 5).Rotation(50, 0, 5);
        }

        Dictionary<ThunderEntity, (Vector3 offset, Vector3 axis)> offsets = new();

        foreach (var kvp in entities)
        {
            var entity = kvp.Key;
            entity.AddForce(
                (Vector3.up + (wand.tip.position - entity.Center).normalized * 0.3f)
                * ((wandItem?.GetMassModifier().Randomize(0.6f) ?? 1f) * 6f), ForceMode.Impulse);
            entity.physicBody.AddTorque(
                Utils.RandomVector() * ((wandItem?.GetMassModifier().Randomize(0.6f) ?? 1f) * 3f),
                ForceMode.Impulse);

            offsets[entity] = (Random.insideUnitSphere.ClampMagnitude(0.2f, 0.8f) * 1.5f, Random.onUnitSphere);
        }

        while (wand.item.mainHandler?.playerHand?.controlHand.usePressed == true) {
            var target = wand.tip.transform.position
                         + wand.tip.transform.forward * 2
                         + wand.tipVelocity.normalized
                         * Mathf.Clamp(wand.tipVelocity.magnitude, 0.0f, 8f)
                         / 5f;

            foreach (var kvp in entities)
            {
                var entity = kvp.Key;
                var pid = kvp.Value;
                pid.UpdateVelocity(target
                                  + wand.tip.rotation
                                  * Quaternion.AngleAxis(Time.time * 60, offsets[entity].axis)
                                  * offsets[entity].offset);
            }

            yield return 0;
        }

        yield return 0;
    }
}
