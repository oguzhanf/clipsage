# Zune Design System — Claude Code handoff

A WinUI 3 / Windows App SDK design system based on the **Zune HD (2009)** design language. Drop it into your clipboard productivity app.

## What's in this package

```
handoff/
├── README.md                    ← this file
├── App.xaml                     ← wires the system in (RequestedTheme=Dark + merged dict)
├── Themes/
│   └── Zune.xaml                ← THE design system. ~470 lines, one ResourceDictionary.
└── Views/
    └── ClipboardPage.xaml       ← reference page showing every component in real use
```

The matching **visual specification** is in `Zune Design System.html` (in the project root). Open it in a browser to see exactly how every component should render — every XAML style in this package has a corresponding live demo there.

## Quick install

1. Copy `Themes/Zune.xaml` into your project at `/Themes/Zune.xaml`.
2. Replace your `App.xaml` with the one in this package (or copy the `RequestedTheme="Dark"` attribute and the `MergedDictionaries` block into yours).
3. Reference styles by key in any page: `Style="{StaticResource ZuneButtonPrimary}"`.
4. Use `ClipboardPage.xaml` as a reference for how to compose pages.

## Non-negotiable design rules

These are the things that make Zune look like Zune. Do not deviate.

- **Background is `#000000`.** Always. No gradients, no surfaces brighter than `#1C1C1C`.
- **One accent color: `#EB7100`.** Use sparingly. Voice, never decoration.
- **All headers lowercase.** Author the strings in lowercase — the style enforces weight only.
- **Big titles bleed off the right edge.** That's the signature move (`ZuneTitleBleed` has a `-200px` right margin).
- **No rounded corners.** `CornerRadius = 0` on every surface.
- **No drop shadows.** Depth comes from absence of decoration.
- **Border thickness is 2px.** Never 1px (except hairline dividers which use `ZuneLine` at 1px).
- **Type:** Segoe UI Light for big stuff, Segoe UI Regular for body.

## Token quick reference

| Color brush         | Hex       | Use                              |
|---------------------|-----------|----------------------------------|
| `ZuneBackground`    | `#000000` | the canvas                       |
| `ZuneSurface1/2/3`  | `#0A` → `#1C` | hover / pressed / selected   |
| `ZuneForeground`    | `#FFFFFF` | primary text                     |
| `ZuneForeground2`   | `#B8B8B8` | body                             |
| `ZuneForeground3`   | `#7A7A7A` | secondary labels                 |
| `ZuneForeground4`   | `#555555` | inactive pivot                   |
| `ZuneForeground5`   | `#333333` | "ghost" next pivot               |
| `ZuneAccent`        | `#EB7100` | the only accent                  |
| `ZuneDanger`        | `#E51400` | destructive only                 |

| Text style          | Family + size              | Use                       |
|---------------------|----------------------------|---------------------------|
| `ZuneTitleBleed`    | Segoe UI Light · 200px     | pivot title, bleeds off   |
| `ZunePageTitle`     | Segoe UI Light · 96px      | page title                |
| `ZuneSubhead`       | Segoe UI Light · 48px      | subhead                   |
| `ZunePivotHeader`   | Segoe UI Light · 44px      | pivot item header         |
| `ZuneListItem`      | Segoe UI Regular · 22px    | settings row label        |
| `ZuneBody`          | Segoe UI Regular · 15px    | paragraph                 |
| `ZuneCaption`       | Segoe UI Regular · 12px    | row sub-label             |
| `ZuneEyebrow`       | Segoe UI Semibold · 11px   | section eyebrow (UPPER)   |

## Component styles (all in `Themes/Zune.xaml`)

| Key                          | TargetType                |
|------------------------------|---------------------------|
| `ZuneButtonPrimary`          | `Button` (filled accent)  |
| `ZuneButton`                 | `Button` (white outline)  |
| `ZuneButtonGhost`            | `Button` (text only)      |
| `ZuneButtonDanger`           | `Button` (red fill)       |
| `ZuneTextBox`                | `TextBox`                 |
| `ZuneToggleSwitch`           | `ToggleSwitch` (the iconic Zune slider) |
| `ZuneListView`               | `ListView`                |
| `ZunePivot`                  | `Pivot`                   |
| `ZuneSlider`                 | `Slider`                  |
| `ZuneContentDialog`          | `ContentDialog`           |
| `ZuneMenuFlyoutPresenter`    | `MenuFlyoutPresenter`     |
| `ZuneMenuFlyoutItem`         | `MenuFlyoutItem`          |
| `ZuneScrollBar`              | `ScrollBar`               |

## Motion

| Easing                                 | Use                                    |
|----------------------------------------|----------------------------------------|
| `cubic-bezier(0.1, 0.9, 0.2, 1)`       | default. the signature Zune curve.    |
| `cubic-bezier(0.2, 0.8, 0.2, 1)`       | general ease-out                       |
| `cubic-bezier(0.6, 0, 1, 0.4)`         | exit (rare)                            |

| Duration | Use                                 |
|----------|-------------------------------------|
| 200ms    | hover, focus                        |
| 400ms    | toggle state, content slide         |
| 650ms    | pivot twist, page title             |

The `ZunePageEnter` storyboard in `Themes/Zune.xaml` is ready to apply to a Page's root `Grid` on `Loaded` for the parallax twist feel.

## Voice (copy guidelines)

- Labels: one word when possible (`save`, `cancel`, `pin`, `remove`).
- All UI text lowercase except `ZuneEyebrow` (uppercase, 0.18em tracking).
- No exclamation points. No emojis. No "Welcome!".
- Errors and danger zones: direct and short ("clear all clipboard items. this action can't be undone.").

— black is canvas. type is content. orange is voice.
