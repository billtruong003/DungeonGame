using UnityEngine.UIElements;
using BillGameCore;

namespace RPGModular.UI
{
    public class DeathPanel : BasePanel
    {
        protected override void Build(VisualElement root)
        {
            root.style.backgroundColor = new StyleColor(new UnityEngine.Color(0, 0, 0, 0.85f));
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;

            var container = new VisualElement();
            container.style.alignItems = Align.Center;
            root.Add(container);

            var title = new Label(Loc.Get("msg.death"));
            title.style.fontSize = 36;
            title.style.color = new StyleColor(new UnityEngine.Color(0.9f, 0.2f, 0.2f));
            title.style.marginBottom = 20;
            container.Add(title);

            int penalty = DeathSystem.Instance?.GoldPenalty ?? 0;
            var penaltyLabel = new Label($"-{penalty} G");
            penaltyLabel.style.fontSize = 20;
            penaltyLabel.style.color = new StyleColor(new UnityEngine.Color(1f, 0.8f, 0.2f));
            penaltyLabel.style.marginBottom = 30;
            container.Add(penaltyLabel);

            var townBtn = new Button(() =>
            {
                DeathSystem.Instance?.Respawn(RespawnOption.Town);
                Bill.UI.Close<DeathPanel>();
            });
            townBtn.text = Loc.Get("msg.death.respawn_town");
            townBtn.style.width = 250; townBtn.style.height = 40;
            townBtn.style.fontSize = 16;
            townBtn.style.marginBottom = 10;
            container.Add(townBtn);

            var hereBtn = new Button(() =>
            {
                DeathSystem.Instance?.Respawn(RespawnOption.InPlace);
                Bill.UI.Close<DeathPanel>();
            });
            hereBtn.text = Loc.Get("msg.death.respawn_here");
            hereBtn.style.width = 250; hereBtn.style.height = 40;
            hereBtn.style.fontSize = 16;
            container.Add(hereBtn);
        }
    }
}
