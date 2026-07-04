using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Repair : WandSkill {
}

public class BreakableTracker : ThunderBehaviour {
    public Breakable breakable;
    public ItemData itemData;
    public Vector3 position;
    public Quaternion rotation;
    public PhysicBody[] otherPieces;

    public void Start() { itemData = breakable.LinkedItem.data; }
}