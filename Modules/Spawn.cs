using System.Collections.Generic;
using GestureEngine;
using ThunderRoad;
using UnityEngine;

namespace Wand;

public class Spawn : WandSkill
{
    public List<string> itemIDs = [
        "Boulder",
        "Crate",
    ];

    protected List<ItemData> items;

    public override void OnCatalogRefresh()
    {
        base.OnCatalogRefresh();
        items = new List<ItemData>();
        if (itemIDs is not { Count: > 0 }) return;
        for (var i = 0; i < itemIDs.Count; i++)
        {
            items.Add(Catalog.GetData<ItemData>(itemIDs[i]));
        }
    }

    public override void Init()
    {
        base.Init();
        wand.button
            .Then(Gesture.Both.Moving(Direction.Together).Palm(Direction.Together))
            .Then(Gesture.Both.Moving(Direction.Apart).Palm(Direction.Down))
            .Do(StartSpawn);
    }

    public Item spawnedItem;

    public Vector3 TargetPosition => wand.tip.transform.position
                                     + wand.tip.transform.forward * 2
                                     + wand.tipVelocity.normalized
                                     * Mathf.Clamp(wand.tipVelocity.magnitude, 0.0f, 8f)
                                     / 5f;
    public Quaternion TargetRotation => wand.tip.transform.rotation * Quaternion.AngleAxis(180, Vector3.forward);

    public void SetItem(ItemData data)
    {
        spawnedItem?.Despawn();
        spawnedItem = null;
        pid = null;
        data.SpawnAsync(OnItemSpawn, TargetPosition, Quaternion.identity);
    }

    public int currentIndex = 0;

    public void PrevItem()
    {
        currentIndex--;
        if (currentIndex < 0) currentIndex = 0;
        SetItem(items[currentIndex]);
    }

    public void NextItem()
    {
        currentIndex++;
        if (currentIndex >= items.Count) currentIndex = 0;
        SetItem(items[currentIndex]);
    }

    public PIDController pid;

    private void OnItemSpawn(Item item)
    {
        spawnedItem = item;
        pid = new PIDController(spawnedItem.physicBody.rigidBody, forceMode: ForceMode.Acceleration, maxForce: 5000)
            .Position(50, 0, 5).Rotation(50, 0, 5);
    }

    public void StartSpawn()
    {
        SetItem(items[currentIndex]);
        if (wand.swirling)
        {
            
        }
    }

    public override void OnUpdate()
    {
        base.OnUpdate();
        if (!wand.active) return;
        if (spawnedItem && pid != null)
        {
            pid.Update(TargetPosition, TargetRotation);
        }
    }
}