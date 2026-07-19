# Sensen AI Nature Design System

This document is the design source of truth for the Sensen endangered-animal science app.

## Product Character

The app is an ecological science experience for teenagers, teachers, judges, and exhibition visitors. It should feel like a real mobile product: calm, polished, warm, and trustworthy, with enough wonder to make the AR animal encounter memorable.

Use a premium ecological style: deep forest atmosphere, soft glass panels, warm cream text, leaf-green actions, and restrained gold rewards.

Do not make the app look like a default Unity demo, a cheap casual game, a cyberpunk interface, or a generic chatbot.

## Design Principles

1. Forest First
   - Use for: Home, Discover, Learn, Chat, card share screens.
   - Rule: every screen should feel connected to a quiet forest environment.
   - Do not: use flat blue system backgrounds or unrelated abstract gradients.

2. Sensen Is A Character
   - Use for: chat, success, mission, card generation.
   - Rule: UI copy should sound like Sensen is gently guiding the user.
   - Do not: use assistant-like wording such as "I am an AI model".

3. One Primary Action
   - Use for: scan, send, save, generate card.
   - Rule: one leaf-green primary button per view.
   - Do not: make every button bright or equally dominant.

4. Calm Premium Texture
   - Use for: icons, backgrounds, cards.
   - Rule: prefer soft gradients, subtle glow, clean edges, and low saturation.
   - Do not: use thick black outlines, neon colors, or noisy decorations.

5. Clear Exhibition Flow
   - Use for: full app route.
   - Rule: a viewer should understand the app path in 3 minutes: scan, interact, ask Sensen, learn, mission, card.
   - Do not: hide core functions behind unexplained menus.

6. Camera Is The Primary Tab
   - Use for: bottom navigation and first-run flow.
   - Rule: the camera/discover action sits in the center of the bottom bar and is larger than side tabs.
   - Do not: give chat a separate bottom-nav tab; chat belongs inside the unlocked model experience.

## Color System

| Token | Hex | Use | Do Not |
| --- | --- | --- | --- |
| Forest950 | `#061411` | Page background, immersive forest base | Use as card text background without contrast |
| Forest900 | `#071D16` | Main panels and navigation | Overuse as every component fill |
| Forest800 | `#0D2A20` | Cards, chat surfaces | Use for text |
| Moss650 | `#2E5C40` | Secondary buttons, inactive tabs | Use for destructive actions |
| Leaf500 | `#5EB873` | Primary buttons, active state, success | Use as full-screen background |
| Leaf300 | `#C7E6C7` | Highlights and soft icon glow | Use for primary text on light panels |
| Cream100 | `#EBF2DB` | Main text on dark forest | Use for disabled text |
| Cream50 | `#F5F8E9` | Share card surface | Use over bright images without contrast |
| Gold500 | `#FFE06B` | Badge and reward accent | Use as default button color |
| Sky500 | `#458ED4` | Rare system/secondary utility accent | Reintroduce default Unity button blue broadly |
| DangerSoft | `#D96B5D` | Wrong food feedback | Use as primary brand red |

## Typography

Use one Chinese-capable sans-serif family. In Unity, prefer `ArialUnicode.ttf` when available, otherwise the bundled legacy runtime font. Use bold only for page titles and badge labels.

| Token | Size | Use | Do Not |
| --- | ---: | --- | --- |
| Display56 | 56 | Learn/Chat major page titles | Use inside compact cards |
| Hero54 | 54 | Home title | Use for body copy |
| Section46 | 46 | Mission and card titles | Use for small button labels |
| Body30 | 30 | Main readable text and button labels | Use for dense metadata |
| Caption24 | 24 | Hints and secondary copy | Use for primary CTA |
| Meta21 | 21 | Save path and small status | Use below 18 px |

Line height should be 1.25 to 1.4. Chinese text should keep natural spacing and avoid negative letter spacing.

## Spacing And Grid

Primary frame: portrait mobile, 1080 x 1920.

Use an 8 px base grid. Main horizontal safe margin is 64 px. Main card width is 820 px. Use 24-32 px between related elements and 48-64 px between sections.

Do not place runtime buttons over existing question buttons. Keep the model action row above the question row with at least 48 px vertical separation.

## Shape, Border, Shadow

Use 8-20 px radius depending on component size:

- 8 px: small chips, icon slots.
- 12 px: regular buttons.
- 20 px: large cards and glass panels.
- Full pill only for compact status tags.

Use subtle borders: cream or leaf at 8-16% opacity. Use soft shadows with low opacity and broad blur. Avoid harsh black shadows.

## Core Components

### Primary Button

Use for scan, send, save, generate card.

- Fill: Leaf500.
- Text: white or Cream50.
- Height: 82-96 px.
- Radius: 12 px.
- State: pressed darkens by 10%; disabled uses Forest800 with muted text.

Do not use default Unity blue for primary action.

### Secondary Button

Use for back, secondary tab actions, optional shortcuts.

- Fill: Moss650.
- Text: Cream100.
- Height: 74-82 px.

Do not make secondary buttons brighter than the primary action.

### Glass Panel

Use for mission, card, learning modules, chat surface.

- Fill: Forest900 or Cream50 with 48-90% opacity depending on contrast.
- Border: subtle cream/leaf line.

Do not stack multiple heavy panels inside each other.

### Chat Bubble

Use inside the post-scan model view. The model should feel present, with Sensen's answer appearing next to the animal instead of on a separate chat page.

- Sensen: light cream glass bubble beside the model, dark forest text, small pointer toward the model.
- User: bottom input bar with send action; avoid persistent preset question buttons.
- Thinking state: "森森正在想一想..." with soft pulsing icon.

Do not show raw network errors to the user.

### Knowledge Card

Use for share/export.

- Header: clear title "今日认识了森森".
- Model/character area: top-middle visual zone.
- Content: learning summary, eco fact, badge.
- Action row: save PNG and return.

Do not let decorative surfaces cover the title or model area.

## States

- Idle: calm forest background, clear next action.
- Active: leaf-green highlight, subtle glow.
- Loading: thinking icon and Sensen wording.
- Success: leaf + gold badge, encouraging copy.
- Error/fallback: warm local fallback, no technical error text.
- Locked: low-contrast forest lock icon and short unlock hint.

## Icon And Illustration Style

Icons should be transparent PNG, 512 x 512. Use soft dimensional forms, low-saturation green, cream highlights, and gentle shadows inside the transparent canvas.

Backgrounds should be portrait 1080 x 1920 PNG, readable behind UI, with quiet depth and soft light.

Do not use icons with white boxes, thick black outlines, busy details, or mismatched cartoon colors.

## Copy Voice

Sensen is lively, gentle, curious, slightly childlike, and protective of the forest.

Use:

- "你找到我啦！"
- "谢谢你愿意了解我的森林。"
- "我们一起守护这片家吧。"

Do not use:

- "作为一个 AI..."
- "发生错误：HTTP 500"
- "点击此处执行下一步操作"

## Unity Tokens

The implementation source is `Assets/Scripts/UI/SensenDesignTokens.cs`. Keep this Markdown block in sync when adding tokens.

```csharp
public static class SensenDesignTokens
{
    public static readonly Color Forest950 = Hex("061411");
    public static readonly Color Forest900 = Hex("071D16");
    public static readonly Color Forest800 = Hex("0D2A20");
    public static readonly Color Moss650 = Hex("2E5C40");
    public static readonly Color Leaf500 = Hex("5EB873");
    public static readonly Color Leaf300 = Hex("C7E6C7");
    public static readonly Color Cream100 = Hex("EBF2DB");
    public static readonly Color Cream50 = Hex("F5F8E9");
    public static readonly Color Gold500 = Hex("FFE06B");
    public static readonly Color Sky500 = Hex("458ED4");
    public static readonly Color DangerSoft = Hex("D96B5D");

    public const int Display56 = 56;
    public const int Hero54 = 54;
    public const int Section46 = 46;
    public const int Body30 = 30;
    public const int Caption24 = 24;
    public const int Meta21 = 21;

    public const float Space8 = 8f;
    public const float Space16 = 16f;
    public const float Space24 = 24f;
    public const float Space32 = 32f;
    public const float Space48 = 48f;
    public const float ScreenMargin64 = 64f;

    public const float Radius8 = 8f;
    public const float Radius12 = 12f;
    public const float Radius20 = 20f;

    public const float ButtonHeight74 = 74f;
    public const float ButtonHeight82 = 82f;
    public const float ButtonHeight88 = 88f;
    public const float PrimaryButtonHeight96 = 96f;
    public const float CardWidth820 = 820f;
}
```

## Agent Implementation Guide

When changing UI, read this file first and treat the Unity token class as the runtime authority. Prefer editing shared helpers such as `StyleButton`, `CreateButton`, `CreatePanel`, and `CreateText` before touching every screen individually.

Apply changes in this order:

1. Choose the screen state: home, scan, model, learn, chat, mission, card, or profile.
2. Select semantic tokens from `SensenDesignTokens`; do not introduce one-off colors unless the token table is missing a real role.
3. Keep one visible primary action per view. Other actions should use Moss650 or dark forest surfaces.
4. Use Chinese navigation and user-facing copy throughout.
5. Verify the 1080 x 1920 portrait layout: no overlapping buttons, no text clipped inside buttons, no UI covering the model interaction zone.
6. For exported cards, preserve a top-middle visual safe zone before placing content panels or action buttons.

## Implementation Status

- Done: runtime and editor-generated UI now share `SensenDesignTokens`.
- Done: scene builder no longer creates default blue buttons as its baseline style.
- Done: bottom navigation now uses 学习 / centered larger 扫描 / 我的.
- Done: model view owns the chat bubble and bottom input bar after scan unlock.
- Done: the model view no longer exposes two preset question buttons.
- Keep watching: card layout should preserve the visual safe zone when new content is added.
- Keep watching: button heights should stay tied to semantic roles instead of per-screen convenience.
