using System.Net.Security;
using ExtensionMethods;
using GestureEngine;

namespace Wand {
    public class Crush : WandModule {
        public float throwForce = 20f;

        public override void OnInit() {
            base.OnInit();
            wand.targetedEnemy
                .Then(wand.Offhand.Palm(Direction.Backward).Point(Direction.Up).Fist)
                .And("Grip", () => wand.otherHand.Gripping() && wand.otherHand.Triggering())
                .Do(CrushEnemy, "Crush");
        }

        public void CrushEnemy() {
            wand.target.creature?.ragdoll.SliceAll(throwForce);
            wand.target.creature?.Kill();
        }
    }
}
