using UnityEngine;
using NowUI;

[AddComponentMenu("NowUI/Examples/Now World Graphic Example")]
public sealed class NowWorldGraphicExample : NowWorldGraphic
{
    [SerializeField] string _title = "Player";
    [SerializeField] string _detail = "Hover for details";
    [SerializeField] Color _accent = new Color(0.18f, 0.58f, 1f, 1f);

    protected override void DrawNowUI(NowRect rect)
    {
        int id = NowControls.GetControlId(1);
        var interaction = NowInput.Interact(id, rect);
        float hover = NowControlState.Transition(NowInput.GetId(id, "hover"), interaction.hovered || interaction.held, 10f);

        var background = Color.Lerp(new Color(0.04f, 0.06f, 0.09f, 0.84f), new Color(0.06f, 0.09f, 0.14f, 0.94f), hover);

        Now.Rectangle(rect)
            .SetColor(background)
            .SetRadius(12f)
            .SetOutline(Mathf.Lerp(1f, 2f, hover))
            .SetOutlineColor(Color.Lerp(new Color(1f, 1f, 1f, 0.16f), _accent, hover))
            .Draw();

        Now.Rectangle(new NowRect(12f, 12f, 8f, rect.height - 24f))
            .SetColor(_accent)
            .SetRadius(4f)
            .Draw();

        Now.Text(new NowRect(30f, 12f, rect.width - 42f, 24f))
            .SetFontSize(20f)
            .SetColor(Color.white)
            .SetMask(rect)
            .Draw(_title);

        Now.Text(new NowRect(30f, 38f, rect.width - 42f, 20f))
            .SetFontSize(13f)
            .SetColor(new Color(0.76f, 0.82f, 0.9f, Mathf.Lerp(0.7f, 1f, hover)))
            .SetMask(rect)
            .Draw(interaction.hovered || interaction.held ? _detail : "Nameplate");

        if (interaction.clicked)
            Debug.Log($"Clicked NowUI world label: {_title}", this);
    }
}
