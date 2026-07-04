using ExtensionMethods;
using GestureEngine;
using ThunderRoad;

namespace Wand; 

public class Crush : WandSkill {
    public float throwForce = 20f;

    public override void OnInit() {
        base.OnInit();
        wand.profane
            .Then(wand.Offhand.Palm(Direction.Backward).Point(Direction.Up).Fist)
            .And("Grip", () => wand.otherHand.Gripping() && wand.otherHand.Triggering())
            .Do(CrushEnemy, "Crush");
    }

    public void CrushEnemy() {
        MarkCasted();
        if (wand.target is not Creature creature) return;
        creature.ragdoll.SliceAll(throwForce);
        creature.Kill();
    }
}