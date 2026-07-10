using System.Collections.Generic;
using GestureEngine;
using ThunderRoad;
using UnityEngine;

namespace Wand;

public class Spawn : WandSkill
{
    public List<string> itemIDs = [
        "Crate",
        "StatueSphere",
        "Barrel2",
        "Rock_Boulder_01",
        "StoneCitadel1",
        "Chair1",
        "Stool_Khartib",
        "SmallColonne",
        "SwordDalgarSmall",
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

    public override void Register()
    {
        base.Register();
        var spawnState = wand.button
            .Then(Gesture.Both.Moving(Direction.Together).Palm(Direction.Together))
            .Then(Gesture.Both.Moving(Direction.Apart, 1).Palm(Direction.Down))
            .Do(StartSpawn);
        spawnState.Then(wand.Swirl(SwirlDirection.Clockwise)).Do(NextItem).ThenResetTo(spawnState);
        spawnState.Then(wand.Swirl(SwirlDirection.CounterClockwise)).Do(PrevItem).ThenResetTo(spawnState);
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
        data.SpawnAsync(OnItemSpawn, Player.local.head.transform.position + Player.local.head.transform.forward * 5,
            Quaternion.identity);
        wand.ResetSwirl();
    }

    public int currentIndex;

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
        spawnedItem.OnDespawnEvent += OnItemDespawn;
        pid = new PIDController(spawnedItem.physicBody.rigidBody, forceMode: ForceMode.Acceleration, maxForce: 5000)
            .Position(50, 0, 5).Rotation(50, 0, 5);
    }

    private void OnItemDespawn(EventTime eventTime)
    {
        if (eventTime is EventTime.OnEnd)
        {
            spawnedItem.OnDespawnEvent -= OnItemDespawn;
            spawnedItem = null;
            pid = null;
        }
    }

    public void StartSpawn()
    {
        SetItem(items[currentIndex]);
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

    public override void OnReset()
    {
        base.OnReset();
        if (spawnedItem)
        {
            spawnedItem.OnDespawnEvent -= OnItemDespawn;
            pid = null;
            spawnedItem = null;
        }
    }
}