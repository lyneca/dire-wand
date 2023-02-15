using ExtensionMethods;
using ThunderRoad;

namespace Wand; 

public class DebugToggle : WandModule {
    public override void OnInit() {
        base.OnInit();
        wand.button.Then(() => wand.item.isGripped && wand.Triggering)
            .Do(() => {
                wand.debug = !wand.debug;
                Viz.enabled = wand.debug;
                DisplayMessage.instance.ShowMessage(new DisplayMessage.MessageData(
                    $"Wand debug mode {GetVerb(wand.debug)}. To {GetVerb(!wand.debug)}, hold the wand in one hand and grip it with another. Hold button and tap trigger.",
                    "", "", "", 1));
            });
    }

    public string GetVerb(bool enabled) => enabled ? "enabled" : "disabled";
}
