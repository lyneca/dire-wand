using System.Collections.Generic;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand;

public class SpellStriker : WandSkill
{
    public override void Register()
    {
        base.Register();
        wand.button.Then(() => Vector3.Distance(wand.tip.position, wand.otherHand.caster.Orb.position) < 0.03f
                               && wand.otherHand.caster.isFiring
                               && wand.otherHand.caster.spellInstance is SpellCastCharge spell
                               && spell.HasModule<SpellModuleMusket>()
                               && spell.Ready, "Touch Spell Orb")
            .Do(FireSpell, "Orb Striker");
    }

    public void FireSpell()
    {
        var spell = wand.otherHand.caster.spellInstance as SpellCastCharge;
        spell!.currentCharge = 0;
        int count = spell.TryGetSpellModules(out List<SpellModuleMusket> modules);
        var fakeImbue = wand.item.colliderGroups[0].gameObject.AddComponent<Imbue>();
        spell.imbue = fakeImbue;
        for (int i = 0; i < count; i++)
        {
            modules[i].Shoot(spell);
        }
        spell.imbue = null;
        Object.Destroy(fakeImbue);
    }
}