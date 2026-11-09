# 轻截图标

为 LightCapture 单独构思的青绿色取景框与金色光点，使用内置 `image_gen` 生成（2026-11-09）。没有导入或指定模仿第三方品牌图标；未进行全面的商标近似性检索，不作绝对不侵权保证。

- `LightCapture.png`：1254 × 1254，透明 PNG，保留最终生成结果的 alpha。
- `LightCapture.ico`：16、20、24、32、40、48、64、96、128、256 像素，32 位透明图像；由 PNG 使用 System.Drawing 高质量双三次缩放并封装为多尺寸 ICO，未改变图案。
- 使用位置：EXE 应用图标、WPF 窗口和系统托盘。

## 完整生成提示词

```text
Use case: logo-brand
Asset type: Original Windows desktop application icon for LightCapture (轻截), a lightweight screenshot, annotation and GIF capture utility.
Primary request: Design ONE original, highly polished app icon with a simple memorable silhouette. A deep teal rounded-square tile contains a bold warm-white geometric capture mark: two offset rounded L-shaped strokes suggest a cropped frame and a small stacked sheet, with one small golden rounded-square light point in the open upper-right corner. Keep the mark visually unified and minimal, with generous negative space. Capture the feeling of quick, light, precise editing.
Style/medium: Crisp flat vector-like artwork, carefully balanced curves, clean edges. No scene, no mockup, no text.
Color palette: teal #079D83 to #087F75 very subtle tile gradient, warm white mark #FFFFFF, a single restrained warm gold accent #FFC866. Nothing else.
Composition/framing: Square 1024 by 1024 canvas, one centered icon, front-on, tile fills about 86 percent of canvas with even transparent margins. Rounded corners. Bold strokes remain clearly legible at 16 and 32 pixels.
Constraints: Fully transparent background outside the tile, preserve alpha. An independently designed abstract symbol, not adapted from any existing logo. No typography, initials, letters, speech bubbles, scissors, camera lens, brand references, stock symbols, watermark, surrounding shadows, glass effects or 3D rendering. Only one icon, no variants, no grid, no explanatory text.
```

## 透明背景修订提示词

```text
Use case: background-extraction
Asset type: Windows app icon with true alpha transparency.
Input image 1 is the edit target. Remove ONLY the black background surrounding the teal rounded-square icon. Replace all that outside black area, including rounded-corner cutouts, with genuinely transparent pixels (alpha 0), not black, white, or a checkerboard pattern. Preserve the teal tile, its outline, the white geometric capture symbol, the gold light point, all internal colors, exact proportions, position, and artwork unchanged. Preserve clean antialiased edges around the tile. Transparent PNG output. Do not redesign, add text, change layout, add a shadow, or include a mockup.
```

